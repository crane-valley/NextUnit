using System.Runtime.ExceptionServices;
using NextUnit.Internal;

namespace NextUnit.Platform;

/// <summary>
/// Runs the session-scoped <c>[Before]</c>/<c>[After]</c> hooks and turns whatever they throw into a
/// reportable outcome instead of letting it escape the platform callback that invoked them.
/// </summary>
/// <remarks>
/// <para>
/// Assembly- and class-scoped hooks are already caught, classified, and attributed by
/// <see cref="TestExecutionEngine"/>. Session hooks run outside the engine, in
/// <see cref="NextUnitFramework.CreateTestSessionAsync"/> and
/// <see cref="NextUnitFramework.CloseTestSessionAsync"/>, so the same treatment has to live here.
/// </para>
/// <para>
/// The reporting channels differ per phase. Setup has a test sink available later in the run, so a
/// requested skip is recorded and replayed onto every test; a setup failure has no sink yet and is
/// surfaced through the session result. Close has no sink at all, which is why teardown failures are
/// aggregated into the session result rather than onto a synthetic node the way assembly teardown
/// failures are.
/// </para>
/// <para>
/// Genuine run cancellation is never turned into a failure in either phase: it propagates as the
/// <see cref="OperationCanceledException"/> the platform expects, which is how the engine surfaces
/// the cancellation its own teardown observes.
/// </para>
/// </remarks>
internal sealed class SessionLifecycleRunner
{
    // An async gate rather than a lock, because session setup awaits user hooks.
    private readonly AsyncOnceGate _setupGate = new();
    private readonly List<LifecycleMethodDelegate> _beforeMethods = new();
    private readonly List<LifecycleMethodDelegate> _afterMethods = new();
    private string? _skipReason;

    /// <summary>
    /// Gets the reason a session setup hook gave for skipping the session, or <see langword="null"/>
    /// when no hook requested a skip.
    /// </summary>
    public string? SkipReason => Volatile.Read(ref _skipReason);

    /// <summary>
    /// Adds the session hooks discovered in the generated registry.
    /// </summary>
    public void AddMethods(
        IReadOnlyList<LifecycleMethodDelegate>? beforeMethods,
        IReadOnlyList<LifecycleMethodDelegate>? afterMethods)
    {
        if (beforeMethods is not null)
        {
            _beforeMethods.AddRange(beforeMethods);
        }

        if (afterMethods is not null)
        {
            _afterMethods.AddRange(afterMethods);
        }
    }

    /// <summary>
    /// Runs the session setup hooks at most once.
    /// </summary>
    /// <returns>
    /// An error message describing the failure, or <see langword="null"/> when setup succeeded or a
    /// hook requested a skip (a skip is reported per test, not as a failed session).
    /// </returns>
    /// <exception cref="OperationCanceledException">
    /// Thrown when the run itself was cancelled, which the platform handles as a normal outcome.
    /// </exception>
    public async Task<string?> RunSetupOnceAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _setupGate.RunOnceAsync(ExecuteSetupAsync, cancellationToken).ConfigureAwait(false);
            return null;
        }
        catch (OperationCanceledException ex) when (RunCancellationClassifier.IsRunCancellation(ex, cancellationToken))
        {
            // Genuine run cancellation is the platform's business, not a framework failure.
            throw;
        }
        catch (Exception ex) when (!ExceptionHelper.IsCriticalException(ex))
        {
            // Deliberately caught outside the gate so the gate still sees a throw and stays open: the
            // session result is failed anyway, and a later caller retrying setup is preferable to it
            // silently proceeding as if the hooks had run.
            var failure = RunCancellationClassifier.ToFailure(
                ex,
                "A session setup method threw OperationCanceledException that does not represent run cancellation.");

            return $"Session setup failed: {failure}";
        }
    }

    /// <summary>
    /// Runs the session teardown hooks in reverse order, running every remaining hook even after one
    /// of them fails.
    /// </summary>
    /// <returns>
    /// An error message describing the failures, or <see langword="null"/> when teardown succeeded.
    /// </returns>
    /// <exception cref="OperationCanceledException">
    /// Thrown when the run itself was cancelled and no hook failed, which the platform handles as a
    /// normal outcome.
    /// </exception>
    public async Task<string?> RunTeardownAsync(CancellationToken cancellationToken)
    {
        OperationCanceledException? cancellation = null;
        var failures = new List<Exception>();

        // Session lifecycle methods MUST be static (enforced by generator/runtime). The null! instance
        // parameter is safe because generated delegates for static methods do not use it - they call
        // TypeName.Method() directly.
        // Catch per hook so a hook observing cancellation (or failing) still runs every remaining hook.
        for (var i = _afterMethods.Count - 1; i >= 0; i--)
        {
            try
            {
                await _afterMethods[i](null!, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException ex) when (RunCancellationClassifier.IsRunCancellation(ex, cancellationToken))
            {
                // Genuine run cancellation (the OCE carries the run token). Held rather than returned
                // at once so the remaining hooks still release their resources, and rethrown below:
                // dropping it would let a cancelled close report as a clean session, which is what
                // assembly teardown avoids by handing its cancellation back to the run.
                cancellation ??= ex;
            }
            catch (OperationCanceledException ex)
            {
                // An OCE carrying a different token (or none) is the hook's own unrelated cancellation,
                // so it is wrapped in a non-OCE and surfaced as a teardown failure.
                failures.Add(RunCancellationClassifier.ToFailure(
                    ex,
                    "A session teardown method threw OperationCanceledException that does not represent run cancellation."));
            }
            catch (Exception ex) when (!ExceptionHelper.IsCriticalException(ex))
            {
                failures.Add(ex);
            }
        }

        if (failures.Count == 0)
        {
            if (cancellation is not null)
            {
                ExceptionDispatchInfo.Capture(cancellation).Throw();
            }

            return null;
        }

        Exception error = failures.Count == 1
            ? failures[0]
            : new AggregateException("One or more session teardown methods failed.", failures);

        // A hook failure outranks cancellation as the reported outcome: adapters swallow cancellation,
        // so rethrowing it instead of reporting would lose the failure it coexists with.
        return cancellation is null
            ? $"Session teardown failed: {error}"
            : $"The test run was cancelled and session teardown failed: {error}";
    }

    /// <summary>
    /// Reports every test case as skipped when a session setup hook requested a skip.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> when the session was skipped and the caller must not run the tests;
    /// otherwise <see langword="false"/>.
    /// </returns>
    /// <remarks>
    /// Mirrors the assembly-scope skip in <see cref="TestExecutionEngine"/>, where a setup hook
    /// throwing <see cref="TestSkippedException"/> makes every test report as skipped with that reason.
    /// </remarks>
    public async Task<bool> TryReportSessionSkipAsync(
        IReadOnlyList<TestCaseDescriptor> testCases,
        ITestExecutionSink sink,
        CancellationToken cancellationToken)
    {
        var skipReason = SkipReason;
        if (skipReason is null)
        {
            return false;
        }

        foreach (var testCase in testCases)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await sink.ReportSkippedAsync(testCase.WithSkipReason(skipReason)).ConfigureAwait(false);
        }

        return true;
    }

    private async Task ExecuteSetupAsync(CancellationToken cancellationToken)
    {
        // Session lifecycle methods MUST be static (enforced by generator/runtime). The null! instance
        // parameter is safe because generated delegates for static methods do not use it - they call
        // TypeName.Method() directly.
        try
        {
            foreach (var beforeMethod in _beforeMethods)
            {
                await beforeMethod(null!, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (TestSkippedException ex)
        {
            // Swallowed inside the gate's operation on purpose: a requested skip is a decision, not a
            // failure, so the gate records setup as completed and no later caller re-runs the hooks.
            // The reason is replayed onto every test by TryReportSessionSkipAsync.
            Volatile.Write(ref _skipReason, ex.Message);
        }
    }
}
