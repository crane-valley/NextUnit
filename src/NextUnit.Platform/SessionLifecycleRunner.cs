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
/// <see cref="NextUnitFramework.CreateTestSessionAsync(CancellationToken)"/> and
/// <see cref="NextUnitFramework.CloseTestSessionAsync(CancellationToken)"/>, so the same treatment
/// has to live here.
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
/// <para>
/// The two phases are paired: <c>[After(Session)]</c> runs only when session setup was entered. A run
/// whose filter selects no test returns from
/// <see cref="NextUnitFramework.CreateTestSessionAsync(CancellationToken)"/> before setup, so its
/// close skips the hooks rather than tearing down a session that was never set up - the same rule
/// <see cref="TestExecutionEngine"/> applies to class scope, and what assembly scope already did by
/// returning early from both phases on an empty test list. Setup counts as entered the moment the
/// phase is reached, before any hook runs, so a <c>[Before(Session)]</c> that throws halfway still
/// runs every <c>[After(Session)]</c> hook: what it had already acquired still has to be released.
/// </para>
/// <para>
/// Unwinding only part of the <c>[After(Session)]</c> list after a partial setup was rejected. Class
/// scope can do that because its levels are the base chain and each level knows its own hook counts;
/// session hooks are deliberately not inherited, so the whole scope is a single level, and the
/// before- and after-lists the registry emits carry no pairing that a prefix could be measured
/// against. Cutting the list positionally would skip an <c>[After(Session)]</c> whose
/// <c>[Before(Session)]</c> did run, which is the failure this pairing exists to prevent.
/// </para>
/// <para>
/// Session end is also where the shared data source instances are released, because the session is
/// the widest scope <c>[ClassDataSource]</c> and <c>[ValuesFrom]</c> can share an instance across.
/// Releasing them is not part of the pairing and happens on every close: expansion runs before the
/// row-level filter, so a run that ends up selecting no test can still have constructed a
/// session-shared instance that nothing else would ever dispose.
/// </para>
/// <para>
/// One instance serves exactly one session, and that is enforced rather than assumed:
/// <see cref="ThrowIfSessionClosed"/>, <see cref="RunSetupOnceAsync"/>, and
/// <see cref="RunTeardownAsync"/> all throw once teardown has run. Microsoft.Testing.Platform builds
/// a framework per session - once per run in console mode and once per request in server mode - so a
/// second session on one instance is a host that reused what it should have rebuilt. Serving it would
/// be worse than refusing it: the setup gate has already closed, so the <c>[Before(Session)]</c>
/// hooks would be skipped and the first session's skip reason replayed onto the second session's
/// tests, while <see cref="NextUnitFramework"/> would hand back memoized test cases whose
/// session-shared instances teardown has already disposed.
/// </para>
/// <para>
/// What that refusal covers is sequential reuse, which is the shape a host reusing an instance
/// actually takes. Two things are deliberately left alone. A second
/// <see cref="NextUnitFramework.CreateTestSessionAsync(CancellationToken)"/> before the session closes
/// is not a second session, and the gate answers it with the setup that already ran, which is what
/// once-per-session means. A setup racing a teardown is not arbitrated either: the two would share one
/// lock, held across user hooks, so a <c>[Before(Session)]</c> hook that never returns would hang
/// session close instead of letting it release the shared instances - and the platform awaits each
/// phase before starting the next, so nothing on the supported path interleaves them.
/// </para>
/// </remarks>
internal sealed class SessionLifecycleRunner
{
    // An async gate rather than a lock, because session setup awaits user hooks.
    private readonly AsyncOnceGate _setupGate = new();
    private readonly List<LifecycleMethodDelegate> _beforeMethods = new();
    private readonly List<LifecycleMethodDelegate> _afterMethods = new();
    private readonly Func<ValueTask> _disposeSharedInstances;
    private int _teardownClaimed;
    private int _setupEntered;
    private string? _skipReason;

    /// <summary>
    /// Initializes a new instance of the <see cref="SessionLifecycleRunner"/> class.
    /// </summary>
    /// <param name="disposeSharedInstances">
    /// Releases the shared data source instances once teardown has run every hook. Defaults to the
    /// process-wide <see cref="SharedInstanceStore"/>; a test passes its own to observe the ordering
    /// without emptying the store the rest of the process is using.
    /// </param>
    public SessionLifecycleRunner(Func<ValueTask>? disposeSharedInstances = null) =>
        _disposeSharedInstances = disposeSharedInstances ?? SharedInstanceStore.DisposeAllAsync;

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
    /// Throws when this instance has already torn its session down.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when session teardown has already run on this instance.
    /// </exception>
    /// <remarks>
    /// Exposed for <see cref="NextUnitFramework.CreateTestSessionAsync(CancellationToken)"/>, which has to refuse a
    /// reused instance before it builds the test cases: a session whose filter matches nothing never
    /// reaches <see cref="RunSetupOnceAsync"/>, so the refusal would otherwise surface at the next
    /// close rather than where the second session was opened.
    /// </remarks>
    public void ThrowIfSessionClosed()
    {
        if (Volatile.Read(ref _teardownClaimed) != 0)
        {
            throw new InvalidOperationException(
                "This SessionLifecycleRunner already tore its session down. One instance serves a " +
                "single test session, whose [After(Session)] hooks have run and whose session-shared " +
                "data source instances are disposed; a second session needs a new instance.");
        }
    }

    /// <summary>
    /// Runs the session setup hooks at most once.
    /// </summary>
    /// <returns>
    /// An error message describing the failure, or <see langword="null"/> when setup succeeded or a
    /// hook requested a skip (a skip is reported per test, not as a failed session).
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when this instance has already torn its session down.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// Thrown when the run itself was cancelled, which the platform handles as a normal outcome.
    /// </exception>
    public async Task<string?> RunSetupOnceAsync(CancellationToken cancellationToken)
    {
        // Outside the try, so the reuse is not filed away as a failing setup hook: a hook failure is
        // the user's to fix and is reported through the session result, whereas this is the host
        // reusing a spent instance and has no reported shape that would make the run meaningful.
        ThrowIfSessionClosed();

        try
        {
            await _setupGate.RunOnceAsync(ExecuteSetupAsync, cancellationToken).ConfigureAwait(false);
            return null;
        }
        // Genuine run cancellation is the platform's business, not a framework failure, so the filter
        // leaves it (and any critical exception) uncaught rather than catching it only to rethrow:
        // an exception no frame catches keeps its original first-chance debugger behavior.
        catch (Exception ex) when (!IsRunCancellation(ex, cancellationToken) && !ExceptionHelper.IsCriticalFailure(ex))
        {
            // Deliberately caught outside the gate so the gate still sees a throw and stays open: the
            // session result is failed anyway, and a later caller retrying setup is preferable to it
            // silently proceeding as if the hooks had run.
            // ToFailure wraps only an OperationCanceledException, so the message names that case and
            // every ordinary exception passes through untouched.
            var failure = RunCancellationClassifier.ToFailure(
                ex,
                "A session setup method threw OperationCanceledException that does not represent run cancellation.");

            return $"Session setup failed: {failure}";
        }
    }

    /// <summary>
    /// Runs the session teardown hooks in reverse order when session setup was entered, running every
    /// remaining hook even after one of them fails, then releases the shared data source instances.
    /// </summary>
    /// <remarks>
    /// The hooks are skipped, with one diagnostic line, when setup was never entered: a run whose
    /// filter selects no test closes a session it never opened, and running <c>[After(Session)]</c>
    /// there would tear down what no <c>[Before(Session)]</c> ever set up. The shared data source
    /// instances are released either way.
    /// </remarks>
    /// <returns>
    /// An error message describing the failures, or <see langword="null"/> when teardown succeeded.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when this instance has already torn its session down.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// Thrown when the run itself was cancelled and no hook failed, which the platform handles as a
    /// normal outcome.
    /// </exception>
    public async Task<string?> RunTeardownAsync(CancellationToken cancellationToken)
    {
        // Claimed before the hooks run, and never released: a teardown that failed or was cancelled
        // still ended the session, and re-running the hooks would tear down what is already gone.
        if (Interlocked.CompareExchange(ref _teardownClaimed, 1, 0) != 0)
        {
            throw new InvalidOperationException(
                "Session teardown has already run on this SessionLifecycleRunner instance. One " +
                "instance serves a single test session, so running the [After(Session)] hooks again " +
                "would tear down a session that is already closed; a second session needs a new " +
                "instance.");
        }

        OperationCanceledException? cancellation = null;
        var failures = new List<Exception>();

        if (Volatile.Read(ref _setupEntered) == 0)
        {
            ReportSkippedTeardown();
        }
        else
        {
            // Session lifecycle methods MUST be static (enforced by generator/runtime). The null! instance
            // parameter is safe because generated delegates for static methods do not use it - they call
            // TypeName.Method() directly.
            // Catch per hook so a hook observing cancellation (or failing) still runs every remaining hook.
            for (var i = _afterMethods.Count - 1; i >= 0; i--)
            {
                var afterMethod = _afterMethods[i];

                await RunGuardedAsync(
                    () => afterMethod(null!, cancellationToken),
                    "A session teardown method threw OperationCanceledException that does not represent run cancellation.")
                    .ConfigureAwait(false);
            }
        }

        // Disposal follows the hooks rather than preceding them: an [After(Session)] hook is entitled
        // to read whatever a session-shared data source is still holding, and releasing the instances
        // first would hand that hook an already-disposed object.
        await RunGuardedAsync(
            () => _disposeSharedInstances().AsTask(),
            "Disposing a shared data source instance threw OperationCanceledException that does not represent run cancellation.")
            .ConfigureAwait(false);

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

        // Every step of teardown is classified the same way, so the classification lives here once
        // rather than beside each step: cancellation carrying the run token is held for the caller,
        // any other failure joins the aggregate, and the remaining steps still run.
        async Task RunGuardedAsync(Func<Task> operation, string cancellationMessage)
        {
            try
            {
                await operation().ConfigureAwait(false);
            }
            // The critical test is repeated on both cancellation filters rather than left to the last
            // catch: cancellation is classified first, so an OperationCanceledException that carries a
            // critical exception would otherwise be held or reported as cancellation and never reach
            // it. Nothing here may catch a critical failure, whatever shape it arrives in.
            catch (OperationCanceledException ex) when (
                !ExceptionHelper.IsCriticalFailure(ex)
                && RunCancellationClassifier.IsRunCancellation(ex, cancellationToken))
            {
                // Genuine run cancellation (the OCE carries the run token). Held rather than returned
                // at once so the remaining hooks still release their resources, and rethrown above:
                // dropping it would let a cancelled close report as a clean session, which is what
                // assembly teardown avoids by handing its cancellation back to the run.
                cancellation ??= ex;
            }
            catch (OperationCanceledException ex) when (!ExceptionHelper.IsCriticalFailure(ex))
            {
                // An OCE carrying a different token (or none) is the step's own unrelated cancellation,
                // so it is wrapped in a non-OCE and surfaced as a teardown failure.
                failures.Add(RunCancellationClassifier.ToFailure(ex, cancellationMessage));
            }
            // IsCriticalFailure rather than IsCriticalException: the shared instance step reports
            // several disposal failures as one AggregateException, and a critical exception inside it
            // must not be filed away as an ordinary teardown failure.
            catch (Exception ex) when (!ExceptionHelper.IsCriticalFailure(ex))
            {
                failures.Add(ex);
            }
        }
    }

    /// <summary>
    /// Says once that the session teardown hooks were skipped because setup never ran.
    /// </summary>
    /// <remarks>
    /// Silence is the one outcome this must not have: a suite whose <c>[After(Session)]</c> hooks stop
    /// running would otherwise look identical to one whose hooks succeeded. Teardown is claimed once
    /// per instance, so the line is written at most once per session. A session that declares no
    /// <c>[After(Session)]</c> hook at all has nothing to report and stays quiet.
    /// </remarks>
    private void ReportSkippedTeardown()
    {
        if (_afterMethods.Count == 0)
        {
            return;
        }

        Diagnostics.SafeWriteError(
            $"[NextUnit] Skipped {_afterMethods.Count} [After(LifecycleScope.Session)] hook(s) because " +
            "session setup never ran; a run whose filter selects no test never reaches it.");
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

        // The last (or only) report may complete normally after cancellation was requested, leaving no
        // further loop iteration to observe it. Returning true here would send the caller home without
        // ever reaching the engine, which is where a run would otherwise notice; the engine guards its
        // own batch loop the same way.
        cancellationToken.ThrowIfCancellationRequested();

        return true;
    }

    /// <summary>
    /// Classifies an arbitrary exception, so an exception filter can exclude genuine run cancellation
    /// without first narrowing the catch to <see cref="OperationCanceledException"/>.
    /// </summary>
    private static bool IsRunCancellation(Exception exception, CancellationToken cancellationToken) =>
        exception is OperationCanceledException canceled
        && RunCancellationClassifier.IsRunCancellation(canceled, cancellationToken);

    private async Task ExecuteSetupAsync(CancellationToken cancellationToken)
    {
        // Marked before the hooks run, and never cleared, because this is what pairs the two phases:
        // a [Before(Session)] that threw halfway may already hold what an [After(Session)] releases,
        // and a session declaring only [After(Session)] hooks has no first [Before(Session)] to start.
        //
        // Inside the gate's operation rather than at the top of RunSetupOnceAsync, because the gate's
        // WaitAsync throws on an already-cancelled token before the operation starts: marking outside
        // would call a setup entered that never reached a hook, and hand its [After(Session)] hooks a
        // session with nothing to release.
        Volatile.Write(ref _setupEntered, 1);

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
        // The critical test guards this catch too, for the same reason it guards the cancellation
        // filters: a skip is the most swallowing branch there is, and TestSkippedException accepts an
        // inner exception, so a hook can hand one a critical failure to hide behind.
        catch (TestSkippedException ex) when (!ExceptionHelper.IsCriticalFailure(ex))
        {
            // Swallowed inside the gate's operation on purpose: a requested skip is a decision, not a
            // failure, so the gate records setup as completed and no later caller re-runs the hooks.
            // The reason is replayed onto every test by TryReportSessionSkipAsync.
            Volatile.Write(ref _skipReason, ex.Message);
        }
    }
}
