using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using NextUnit.Core;

namespace NextUnit.Internal;

/// <summary>
/// Defines a sink for reporting test execution results.
/// </summary>
public interface ITestExecutionSink
{
    /// <summary>
    /// Reports that a test has passed successfully.
    /// </summary>
    /// <param name="test">The test case that passed.</param>
    /// <param name="output">The test output captured during execution, or null if no output.</param>
    /// <param name="artifacts">The artifacts attached to the test, or null if none.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public Task ReportPassedAsync(TestCaseDescriptor test, string? output = null, IReadOnlyList<Artifact>? artifacts = null);

    /// <summary>
    /// Reports that a test has failed due to an assertion failure.
    /// </summary>
    /// <param name="test">The test case that failed.</param>
    /// <param name="ex">The assertion exception that caused the failure.</param>
    /// <param name="output">The test output captured during execution, or null if no output.</param>
    /// <param name="artifacts">The artifacts attached to the test, or null if none.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public Task ReportFailedAsync(TestCaseDescriptor test, AssertionFailedException ex, string? output = null, IReadOnlyList<Artifact>? artifacts = null);

    /// <summary>
    /// Reports that a test encountered an unexpected error.
    /// </summary>
    /// <param name="test">The test case that encountered an error.</param>
    /// <param name="ex">The exception that was thrown.</param>
    /// <param name="output">The test output captured during execution, or null if no output.</param>
    /// <param name="artifacts">The artifacts attached to the test, or null if none.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public Task ReportErrorAsync(TestCaseDescriptor test, Exception ex, string? output = null, IReadOnlyList<Artifact>? artifacts = null);

    /// <summary>
    /// Reports that a test was skipped.
    /// </summary>
    /// <param name="test">The test case that was skipped.</param>
    /// <param name="artifacts">The artifacts attached to the test before skipping, or null if none.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public Task ReportSkippedAsync(TestCaseDescriptor test, IReadOnlyList<Artifact>? artifacts = null);
}

/// <summary>
/// Orchestrates the execution of test cases with support for dependencies, parallelism, and lifecycle hooks.
/// </summary>
public sealed class TestExecutionEngine
{
    private static readonly ConditionalWeakTable<Assembly, string> _assemblyNames = new();
    private readonly ConcurrentDictionary<Type, ClassExecutionContext> _classContexts = new();
    private readonly SemaphoreSlim _assemblySetupLock = new(1, 1);
    private bool _assemblySetupExecuted;
    private string? _assemblySkipReason;
    private readonly List<LifecycleMethodDelegate> _assemblyBeforeMethods = new();
    private readonly List<LifecycleMethodDelegate> _assemblyAfterMethods = new();

    /// <summary>
    /// Sets global lifecycle methods for Assembly scope.
    /// These methods are collected globally across all test classes and should be called
    /// before RunAsync to ensure proper Assembly lifecycle execution.
    /// </summary>
    /// <param name="beforeMethods">Methods to run before any test in the assembly.</param>
    /// <param name="afterMethods">Methods to run after all tests in the assembly.</param>
    public void SetGlobalAssemblyLifecycle(
        IReadOnlyList<LifecycleMethodDelegate>? beforeMethods,
        IReadOnlyList<LifecycleMethodDelegate>? afterMethods)
    {
        if (beforeMethods is not null)
        {
            _assemblyBeforeMethods.AddRange(beforeMethods);
        }

        if (afterMethods is not null)
        {
            _assemblyAfterMethods.AddRange(afterMethods);
        }
    }

    /// <summary>
    /// Runs a collection of test cases asynchronously.
    /// </summary>
    /// <param name="testCases">The test cases to execute.</param>
    /// <param name="sink">The sink for reporting test results.</param>
    /// <param name="cancellationToken">A cancellation token to observe while waiting for the task to complete.</param>
    /// <returns>A task that represents the asynchronous execution operation.</returns>
    /// <remarks>
    /// One engine supports many sequential runs, but not overlapping ones: assembly setup state is
    /// shared across the instance, so a run starting while another is still executing would see setup
    /// already done and then tear the assembly down a second time. Callers must await a run before
    /// starting the next on the same engine.
    /// </remarks>
    public async Task RunAsync(
        IEnumerable<TestCaseDescriptor> testCases,
        ITestExecutionSink sink,
        CancellationToken cancellationToken)
    {
        var testCasesList = testCases.ToList();

        // Note: Assembly lifecycle methods should be set via SetGlobalAssemblyLifecycle before calling RunAsync.
        // This ensures global lifecycle methods from all test classes are properly collected.

        var graph = DependencyGraph.Build(testCasesList);
        var scheduler = new ParallelScheduler(graph);

        // Wrap sink to track outcomes for ProceedOnFailure support
        var trackingSink = new OutcomeTrackingSink(sink, scheduler);

        // Capture the run-body exception rather than letting it propagate directly, so cleanup failures
        // can be surfaced alongside it. Throwing from the finally would overwrite an in-flight exception
        // (including critical ones); merging happens after the finally instead.
        ExceptionDispatchInfo? bodyFailure = null;
        ExceptionDispatchInfo? cleanupCritical = null;
        OperationCanceledException? cleanupCancellation = null;
        var cleanupFailures = new List<Exception>();

        try
        {
            // Execute assembly-level setup
            await ExecuteAssemblySetupAsync(testCasesList, cancellationToken).ConfigureAwait(false);

            // Execute tests in batches with parallel constraints
            await foreach (var batch in scheduler.GetExecutionBatchesAsync(cancellationToken).ConfigureAwait(false))
            {
                await ExecuteBatchAsync(batch, trackingSink, cancellationToken).ConfigureAwait(false);
            }

            // The last (or only) test may ignore the token and complete normally, leaving no further
            // loop iteration to observe cancellation; surface it here so a cancelled run does not pass.
            cancellationToken.ThrowIfCancellationRequested();
        }
        catch (Exception ex)
        {
            bodyFailure = ExceptionDispatchInfo.Capture(ex);
        }
        finally
        {
            // Run each cleanup phase under its own guard: a critical exception escaping one phase must
            // not skip the next, nor mask a critical run-body exception (resolved after the finally).
            try
            {
                var (classCancellation, classFailures) = await CleanupClassInstancesAsync(trackingSink, cancellationToken).ConfigureAwait(false);
                cleanupCancellation = classCancellation;
                cleanupFailures.AddRange(classFailures);
            }
            catch (Exception ex)
            {
                cleanupCritical ??= ExceptionDispatchInfo.Capture(ex);
            }

            try
            {
                var (assemblyCancellation, assemblyFailures) = await ExecuteAssemblyTeardownAsync(testCasesList, trackingSink, cancellationToken).ConfigureAwait(false);
                cleanupCancellation ??= assemblyCancellation;
                cleanupFailures.AddRange(assemblyFailures);
            }
            catch (Exception ex)
            {
                cleanupCritical ??= ExceptionDispatchInfo.Capture(ex);
            }

            // Assembly setup is guarded by a flag while teardown is unguarded and runs at the end of
            // every run, so the flag has to be released here: a reused engine would otherwise run
            // teardown a second time with no matching setup. Reset outside the guard above so a failing
            // teardown cannot strand the flag and silently skip setup for every later run.
            await ResetAssemblyScopeStateAsync().ConfigureAwait(false);
        }

        // A critical run-body exception (OOM, stack overflow, ...) must propagate alone and unmasked.
        if (bodyFailure is not null && ExceptionHelper.IsCriticalException(bodyFailure.SourceException))
        {
            bodyFailure.Throw();
        }

        // The body was non-critical (or absent), so a critical exception escaping cleanup surfaces next.
        cleanupCritical?.Throw();

        // Cancellation may first fire during cleanup itself (e.g. the final teardown hook cancels the
        // token and returns normally). No hook threw, so nothing was recorded and the pre-cleanup check
        // ran too early; synthesize the cancellation so the cancelled run does not complete successfully.
        var bodyIsRunCancellation = bodyFailure?.SourceException is OperationCanceledException bodyOce
            && IsRunCancellation(bodyOce, cancellationToken);
        if (cancellationToken.IsCancellationRequested && cleanupCancellation is null && !bodyIsRunCancellation)
        {
            cleanupCancellation = new OperationCanceledException(cancellationToken);
        }

        ThrowCombinedFailure(bodyFailure?.SourceException, cleanupCancellation, cleanupFailures, cancellationToken);
    }

    private static bool IsRunCancellation(OperationCanceledException exception, CancellationToken cancellationToken) =>
        RunCancellationClassifier.IsRunCancellation(exception, cancellationToken);

    /// <summary>
    /// Surfaces the run-body exception together with any cleanup failures so that neither run
    /// cancellation nor a coexisting teardown/report failure is silently lost.
    /// </summary>
    private static void ThrowCombinedFailure(
        Exception? bodyException,
        OperationCanceledException? cleanupCancellation,
        List<Exception> cleanupFailures,
        CancellationToken cancellationToken)
    {
        // A body OCE counts as run cancellation only when it is genuine run cancellation; an OCE carrying
        // a foreign token (e.g. a setup hook or sink throwing its own) is a normal failure. Represent
        // cancellation once, whether observed by the run body or during cleanup.
        OperationCanceledException? bodyCancellation =
            bodyException is OperationCanceledException oce && IsRunCancellation(oce, cancellationToken)
                ? oce
                : null;
        var cancellation = bodyCancellation ?? cleanupCancellation;

        var failures = new List<Exception>();
        if (bodyException is not null && !ReferenceEquals(bodyException, bodyCancellation))
        {
            // Wrap a non-run OCE so it is not mistaken for run cancellation (which adapters swallow).
            failures.Add(RunCancellationClassifier.ToFailure(
                bodyException,
                "A run operation threw OperationCanceledException that does not represent run cancellation."));
        }

        failures.AddRange(cleanupFailures);

        if (failures.Count == 0)
        {
            // Pure cancellation, or a completely clean run.
            if (cancellation is not null)
            {
                ExceptionDispatchInfo.Capture(cancellation).Throw();
            }

            return;
        }

        if (cancellation is null && failures.Count == 1)
        {
            ExceptionDispatchInfo.Capture(failures[0]).Throw();
            return;
        }

        var all = new List<Exception>(failures.Count + 1);
        if (cancellation is not null)
        {
            all.Add(cancellation);
        }

        all.AddRange(failures);

        throw new AggregateException(
            cancellation is not null
                ? "The test run was cancelled and one or more cleanup steps failed."
                : "One or more cleanup steps failed.",
            all);
    }

    /// <summary>
    /// Executes a batch of tests in parallel with the specified degree of parallelism.
    /// </summary>
    /// <param name="batch">The batch of tests to execute.</param>
    /// <param name="sink">The sink for reporting test results.</param>
    /// <param name="cancellationToken">A cancellation token to observe.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    private async Task ExecuteBatchAsync(
        TestBatch batch,
        ITestExecutionSink sink,
        CancellationToken cancellationToken)
    {
        // Handle skip batches - tests skipped due to failed dependencies.
        // These tests have already been marked with skip reasons via WithSkipReason("Dependency failed")
        // in ParallelScheduler.GetExecutionBatchesAsync before being yielded as a skip batch.
        if (batch.IsSkipBatch)
        {
            foreach (var test in batch.Tests)
            {
                await sink.ReportSkippedAsync(test).ConfigureAwait(false);
            }
            return;
        }

        if (batch.IsSerial || batch.MaxDegreeOfParallelism == 1)
        {
            // Execute serially
            foreach (var test in batch.Tests)
            {
                // Stop starting new tests once the run is cancelled, even if the previous test
                // ignored the token and returned normally.
                cancellationToken.ThrowIfCancellationRequested();
                await ExecuteSingleAsync(test, sink, cancellationToken).ConfigureAwait(false);
            }
        }
        else
        {
            // Execute in parallel with limit
            var options = new ParallelOptions
            {
                MaxDegreeOfParallelism = batch.MaxDegreeOfParallelism,
                CancellationToken = cancellationToken
            };

            await Parallel.ForEachAsync(batch.Tests, options, async (test, ct) =>
            {
                await ExecuteSingleAsync(test, sink, ct).ConfigureAwait(false);
            }).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Executes assembly-level setup methods.
    /// </summary>
    private async Task ExecuteAssemblySetupAsync(List<TestCaseDescriptor> testCases, CancellationToken cancellationToken)
    {
        if (testCases.Count == 0 || _assemblyBeforeMethods.Count == 0)
        {
            return;
        }

        // Use semaphore to ensure assembly setup runs only once even in parallel execution
        await _assemblySetupLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_assemblySetupExecuted)
            {
                return;
            }

            try
            {
                // Assembly lifecycle methods are always static (enforced by generator),
                // so the instance parameter is unused. We pass null for efficiency.
                foreach (var beforeMethod in _assemblyBeforeMethods)
                {
                    await beforeMethod(null!, cancellationToken).ConfigureAwait(false);
                }
            }
            catch (TestSkippedException ex)
            {
                // Assembly setup requested skip - all tests will be skipped
                _assemblySkipReason = ex.Message;
            }

            _assemblySetupExecuted = true;
        }
        finally
        {
            _assemblySetupLock.Release();
        }
    }

    /// <summary>
    /// Clears the assembly-scope state that belongs to a single run, so the next run on a reused engine
    /// starts from the same state a fresh engine would have.
    /// </summary>
    /// <remarks>
    /// Taken under the setup lock because that is where the flag is written; the run's own token is not
    /// used, since a cancelled run must still hand a clean engine to the next one.
    /// </remarks>
    private async Task ResetAssemblyScopeStateAsync()
    {
        await _assemblySetupLock.WaitAsync(CancellationToken.None).ConfigureAwait(false);
        try
        {
            _assemblySetupExecuted = false;
            _assemblySkipReason = null;
        }
        finally
        {
            _assemblySetupLock.Release();
        }
    }

    /// <summary>
    /// Executes assembly-level teardown methods.
    /// </summary>
    /// <returns>
    /// The run cancellation observed during teardown (if any) and every sink-reporting failure, so the
    /// caller can surface a teardown failure even when it could not be delivered to the sink.
    /// </returns>
    private async Task<(OperationCanceledException? Cancellation, List<Exception> Failures)> ExecuteAssemblyTeardownAsync(
        List<TestCaseDescriptor> testCases, ITestExecutionSink sink, CancellationToken cancellationToken)
    {
        if (_assemblyAfterMethods.Count == 0 || testCases.Count == 0)
        {
            return (null, new List<Exception>());
        }

        OperationCanceledException? cancellation = null;
        var failures = new List<Exception>();

        // Assembly lifecycle methods are always static (enforced by generator),
        // so the instance parameter is unused. We pass null for efficiency.
        // Catch per hook so a hook observing cancellation (or failing) still runs every remaining hook;
        // the first such failure is returned so the run does not silently complete as successful.
        foreach (var afterMethod in _assemblyAfterMethods)
        {
            try
            {
                await afterMethod(null!, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException ex) when (IsRunCancellation(ex, cancellationToken))
            {
                // Genuine run cancellation (the OCE carries the run token).
                cancellation ??= ex;
            }
            catch (OperationCanceledException ex)
            {
                // An OCE carrying a different token (or none) is the hook's own unrelated cancellation.
                // Wrap it in a non-OCE so it is surfaced as a teardown failure rather than being
                // mistaken for run cancellation (which downstream adapters swallow).
                failures.Add(RunCancellationClassifier.ToFailure(
                    ex,
                    "An assembly teardown method threw OperationCanceledException that does not represent run cancellation."));
            }
            catch (Exception ex) when (!ExceptionHelper.IsCriticalException(ex))
            {
                failures.Add(ex);
            }
        }

        if (failures.Count == 0)
        {
            return (cancellation, failures);
        }

        // Aggregate all hook failures into a single node so multiple failures do not collide on the
        // same "[AssemblyTeardown]" identity.
        var teardownNode = CreateAssemblyScopeTest(testCases[0], "AssemblyTeardown");
        var error = failures.Count == 1
            ? failures[0]
            : new AggregateException("One or more assembly teardown methods failed.", failures);

        try
        {
            await sink.ReportErrorAsync(teardownNode, error).ConfigureAwait(false);
            return (cancellation, new List<Exception>());
        }
        catch (Exception ex) when (!ExceptionHelper.IsCriticalException(ex))
        {
            Diagnostics.SafeWriteError($"[NextUnit] Failed to report assembly teardown error for '{teardownNode.Id.Value}'", ex);

            // Preserve BOTH the original teardown error and the sink failure; reporting the sink failure
            // alone would silently lose the teardown exception it was carrying.
            return (cancellation, new List<Exception>
            {
                new AggregateException(
                    $"Failed to report assembly teardown error for '{teardownNode.Id.Value}'.", error, ex)
            });
        }
    }

    /// <summary>
    /// Builds a synthetic node describing an assembly-scope cleanup failure.
    /// </summary>
    /// <remarks>
    /// Reporting through the sink makes the failure visible as a test result in every adapter, instead
    /// of only surfacing as an exception thrown out of <see cref="RunAsync"/> that adapters cannot
    /// attribute to anything.
    /// </remarks>
    private static TestCaseDescriptor CreateAssemblyScopeTest(
        TestCaseDescriptor representativeTest,
        string scope)
    {
        var assemblyName = _assemblyNames.GetValue(
            representativeTest.TestClass.Assembly,
            static assembly => assembly.GetName().Name ?? "");

        return new TestCaseDescriptor
        {
            Id = new TestCaseId($"{assemblyName}.[{scope}]"),
            DisplayName = $"{assemblyName} ({scope})",
            TestClass = representativeTest.TestClass,
            MethodName = scope
        };
    }

    /// <summary>
    /// Executes a single test case with its lifecycle hooks.
    /// </summary>
    /// <param name="testCase">The test case to execute.</param>
    /// <param name="sink">The sink for reporting the test result.</param>
    /// <param name="cancellationToken">A cancellation token to observe while waiting for the task to complete.</param>
    /// <returns>A task that represents the asynchronous execution operation.</returns>
    private async Task ExecuteSingleAsync(
        TestCaseDescriptor testCase,
        ITestExecutionSink sink,
        CancellationToken cancellationToken)
    {
        // Handle pre-execution skip conditions
        var skipResult = await CheckSkipConditionsAsync(testCase, sink, cancellationToken).ConfigureAwait(false);
        if (skipResult.ShouldReturn)
        {
            return;
        }

        try
        {
            await ExecuteWithRetryAsync(testCase, sink, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            // Always clear the test context, even if instance creation fails
            TestContext.SetCurrent(null);
        }
    }

    /// <summary>
    /// Checks pre-execution skip conditions for a test case.
    /// </summary>
    /// <returns>A result indicating whether the test should be skipped and execution should return.</returns>
    private async Task<SkipCheckResult> CheckSkipConditionsAsync(
        TestCaseDescriptor testCase,
        ITestExecutionSink sink,
        CancellationToken cancellationToken)
    {
        // Check if test is skipped (compile-time)
        if (testCase.IsSkipped)
        {
            await sink.ReportSkippedAsync(testCase).ConfigureAwait(false);
            return SkipCheckResult.Skip;
        }

        // Check if assembly setup requested skip
        if (_assemblySkipReason is not null)
        {
            await sink.ReportSkippedAsync(testCase.WithSkipReason(_assemblySkipReason)).ConfigureAwait(false);
            return SkipCheckResult.Skip;
        }

        if (testCase.TestMethod is null && testCase.TestMethodWithArguments is null)
        {
            await sink.ReportErrorAsync(
                testCase,
                new InvalidOperationException($"Test method delegate is null for test '{testCase.Id.Value}'")).ConfigureAwait(false);
            return SkipCheckResult.Skip;
        }

        // Execute class-level setup if not already done
        await EnsureClassSetupAsync(testCase, cancellationToken).ConfigureAwait(false);

        // Check if class setup requested skip
        if (_classContexts.TryGetValue(testCase.TestClass, out var classContext) && classContext.SkipReason is not null)
        {
            await sink.ReportSkippedAsync(testCase.WithSkipReason(classContext.SkipReason)).ConfigureAwait(false);
            return SkipCheckResult.Skip;
        }

        return SkipCheckResult.Continue;
    }

    /// <summary>
    /// Creates a new test context for the specified test case.
    /// </summary>
    private static TestContextCapture CreateTestContext(
        TestCaseDescriptor testCase,
        CancellationToken effectiveToken,
        TestOutputCapture testOutput)
    {
        return new TestContextCapture(
            testName: testCase.MethodName,
            className: testCase.TestClass.Name,
            assemblyName: _assemblyNames.GetValue(
                testCase.TestClass.Assembly,
                static assembly => assembly.GetName().Name ?? ""),
            fullyQualifiedName: testCase.Id.Value,
            categories: testCase.Categories,
            tags: testCase.Tags,
            arguments: testCase.Arguments,
            timeoutMs: testCase.TimeoutMs,
            repeatIndex: testCase.RepeatIndex,
            cancellationToken: effectiveToken,
            output: testOutput);
    }

    /// <summary>
    /// Executes a test case with retry logic.
    /// </summary>
    private async Task ExecuteWithRetryAsync(
        TestCaseDescriptor testCase,
        ITestExecutionSink sink,
        CancellationToken cancellationToken)
    {
        var maxAttempts = testCase.Retry.Count ?? 1;
        var retryDelayMs = testCase.Retry.DelayMs;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            // A fresh timeout source per attempt makes [Timeout] a per-attempt budget; a single
            // source shared across retries would leave later attempts starting already-cancelled.
            using var timeoutCts = testCase.TimeoutMs.HasValue
                ? new CancellationTokenSource(testCase.TimeoutMs.Value)
                : null;
            using var linkedCts = timeoutCts is not null
                ? CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token)
                : null;
            var effectiveToken = linkedCts?.Token ?? cancellationToken;

            var testOutput = new TestOutputCapture();
            var testContext = CreateTestContext(testCase, effectiveToken, testOutput);
            TestContext.SetCurrent(testContext);

            var attemptResult = await ExecuteSingleAttemptAsync(
                testCase, sink, effectiveToken, timeoutCts, testOutput, cancellationToken).ConfigureAwait(false);

            if (attemptResult.IsTerminal)
            {
                return;
            }

            // If the run was cancelled during this attempt, abort instead of retrying or reporting a
            // spurious error: a normal exception from a cancelled attempt is superseded by cancellation,
            // and later attempts would otherwise start on the already-cancelled token.
            cancellationToken.ThrowIfCancellationRequested();

            // Non-terminal failure - check if we should retry
            if (attempt < maxAttempts)
            {
                // Wait before retry if delay is specified
                if (retryDelayMs > 0)
                {
                    await Task.Delay(retryDelayMs, cancellationToken).ConfigureAwait(false);
                }
            }
            else
            {
                // Final attempt - report the exception
                // Exception is guaranteed non-null because AttemptResult.Retriable(Exception) requires non-null parameter
                if (attemptResult.Exception is null)
                {
                    throw new InvalidOperationException("Retriable attempt result must have a non-null exception.");
                }
                var artifacts = TestContext.Current?.Artifacts;
                await ReportFinalExceptionAsync(testCase, sink, attemptResult.Exception, testOutput.GetOutput(), artifacts).ConfigureAwait(false);
                return;
            }
        }

        // Reaching this point indicates a violation of the retry logic invariants and should be impossible.
        // Throwing here makes such logic errors immediately visible during development.
        throw new InvalidOperationException("Unreachable code path in ExecuteWithRetryAsync: no terminal attempt result was produced.");
    }

    /// <summary>
    /// Executes a single test attempt (one iteration of the retry loop).
    /// </summary>
    private async Task<AttemptResult> ExecuteSingleAttemptAsync(
        TestCaseDescriptor testCase,
        ITestExecutionSink sink,
        CancellationToken effectiveToken,
        CancellationTokenSource? timeoutCts,
        TestOutputCapture testOutput,
        CancellationToken cancellationToken)
    {
        // Create test instance (each test gets its own instance)
        // TestContext.Current is guaranteed non-null because SetCurrent() is called in ExecuteWithRetryAsync before this method
        var currentContext = TestContext.Current
            ?? throw new InvalidOperationException("TestContext.Current must be initialized before executing a test attempt.");
        var instance = TestInstanceActivator.Create(testCase, testOutput, currentContext);

        AttemptResult result;
        Exception? disposalFailure = null;
        try
        {
            result = await RunAttemptBodyAsync(testCase, instance, effectiveToken, timeoutCts, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            try
            {
                await DisposeInstanceAsync(instance).ConfigureAwait(false);
            }
            catch (Exception ex) when (!ExceptionHelper.IsCriticalException(ex))
            {
                // Disposal is not passed the run token, so an OCE from a disposer is its own cancellation
                // rather than run cancellation. The captured failure is consumed only on the normal path
                // below: when the body is already propagating run cancellation or a critical exception,
                // that exception wins and the disposal failure is intentionally dropped.
                disposalFailure = ex;
            }
        }

        if (disposalFailure is null)
        {
            await ReportAttemptOutcomeAsync(testCase, sink, result, testOutput, currentContext).ConfigureAwait(false);
            return result;
        }

        // The instance belongs to this test, so its disposal failure is reported on the test's own node
        // instead of a synthetic one (unlike class-scope disposal, whose instance is shared). Reporting
        // happens after disposal so a passing test is not first reported as passed and then failed.
        var reportedFailure = CombineWithDisposalFailure(result, disposalFailure);
        try
        {
            await ReportFinalExceptionAsync(
                testCase,
                sink,
                reportedFailure,
                testOutput.GetOutput(),
                currentContext.Artifacts).ConfigureAwait(false);
        }
        catch (Exception ex) when (!ExceptionHelper.IsCriticalException(ex))
        {
            Diagnostics.SafeWriteError($"[NextUnit] Failed to report test instance disposal failure for '{testCase.Id.Value}'", ex);

            // Preserve BOTH the disposal failure and the sink failure, as the class and assembly cleanup
            // paths do: propagating the sink failure alone would erase the cleanup error it was carrying.
            throw new AggregateException(
                $"Failed to report test instance disposal failure for '{testCase.Id.Value}'.", reportedFailure, ex);
        }

        // Terminal: a disposer that throws is not fixed by retrying, and a later passing attempt would
        // silently discard the failure already reported here.
        return AttemptResult.Reported;
    }

    /// <summary>
    /// Runs the lifecycle hooks and the test method for a single attempt, without reporting to the sink.
    /// </summary>
    /// <remarks>
    /// Reporting is deferred to the caller so that it happens after instance disposal, which lets a
    /// disposal failure change the reported outcome instead of arriving after the result was published.
    /// </remarks>
    private static async Task<AttemptResult> RunAttemptBodyAsync(
        TestCaseDescriptor testCase,
        object instance,
        CancellationToken effectiveToken,
        CancellationTokenSource? timeoutCts,
        CancellationToken cancellationToken)
    {
        try
        {
            // Execute before lifecycle methods (test-scoped)
            foreach (var beforeMethod in testCase.Lifecycle.BeforeTestMethods)
            {
                await beforeMethod(instance, effectiveToken).ConfigureAwait(false);
            }

            // Execute the test method
            // TestMethod is guaranteed non-null because CheckSkipConditionsAsync validates it before execution
            if (testCase.TestMethodWithArguments is not null)
            {
                await testCase.TestMethodWithArguments(
                    instance,
                    testCase.Arguments ?? Array.Empty<object?>(),
                    effectiveToken).ConfigureAwait(false);
            }
            else
            {
                await testCase.TestMethod!(instance, effectiveToken).ConfigureAwait(false);
            }

            // Execute after lifecycle methods (test-scoped)
            foreach (var afterMethod in testCase.Lifecycle.AfterTestMethods)
            {
                await afterMethod(instance, effectiveToken).ConfigureAwait(false);
            }

            return AttemptResult.Passed;
        }
        catch (OperationCanceledException ex) when (cancellationToken.IsCancellationRequested)
        {
            // The outer run token fired (Ctrl+C / host shutdown): propagate cancellation per
            // Microsoft.Testing.Platform guidance instead of misclassifying it as an error and
            // re-executing the in-flight test under [Retry].
            //
            // With [Timeout] the body observes the linked (timeout + run) token, so the OCE can carry
            // the linked token rather than the run token. Normalize it to the run token so downstream
            // classification recognizes genuine run cancellation instead of wrapping it as a failure.
            throw new OperationCanceledException(ex.Message, ex, cancellationToken);
        }
        catch (OperationCanceledException) when (timeoutCts?.IsCancellationRequested == true)
        {
            // Timeout occurred - do not retry timeouts
            return AttemptResult.TimedOut(new TestTimeoutException(testCase.TimeoutMs!.Value));
        }
        catch (TestSkippedException ex)
        {
            // Runtime skip - do not retry skips
            return AttemptResult.Skipped(ex);
        }
        catch (OutOfMemoryException)
        {
            // Rethrow to preserve fail-fast behavior for critical exception types.
            throw;
        }
        catch (StackOverflowException)
        {
            // Rethrow to preserve fail-fast behavior for critical exception types.
            throw;
        }
        catch (Exception ex)
        {
            return AttemptResult.Retriable(ex);
        }
    }

    /// <summary>
    /// Reports a completed attempt whose instance disposed successfully.
    /// </summary>
    /// <remarks>
    /// A retriable result is deliberately not reported here: the retry loop owns that decision and
    /// reports it only after the final attempt.
    /// </remarks>
    private static Task ReportAttemptOutcomeAsync(
        TestCaseDescriptor testCase,
        ITestExecutionSink sink,
        AttemptResult result,
        TestOutputCapture testOutput,
        ITestContext currentContext)
    {
        // Artifacts collected before a timeout or a runtime skip are preserved.
        var artifacts = currentContext.Artifacts;

        return result.Outcome switch
        {
            AttemptOutcome.Passed => sink.ReportPassedAsync(testCase, testOutput.GetOutput(), artifacts),
            AttemptOutcome.TimedOut => sink.ReportErrorAsync(testCase, result.Exception!, testOutput.GetOutput(), artifacts),
            AttemptOutcome.Skipped => sink.ReportSkippedAsync(testCase.WithSkipReason(result.Exception!.Message), artifacts),
            _ => Task.CompletedTask
        };
    }

    /// <summary>
    /// Builds the exception reported when disposing a per-test instance failed.
    /// </summary>
    /// <remarks>
    /// The attempt's own exception stays first so the disposal failure never masks the original
    /// failure, mirroring how coexisting cleanup failures are combined for the whole run.
    /// </remarks>
    private static Exception CombineWithDisposalFailure(AttemptResult result, Exception disposalFailure)
    {
        if (result.Exception is null)
        {
            return disposalFailure;
        }

        var message = result.Outcome switch
        {
            AttemptOutcome.Skipped => "The test was skipped at runtime and disposing the test instance failed.",
            AttemptOutcome.TimedOut => "The test timed out and disposing the test instance failed.",
            _ => "The test failed and disposing the test instance failed."
        };

        return new AggregateException(message, result.Exception, disposalFailure);
    }

    /// <summary>
    /// Reports the final exception after all retry attempts are exhausted.
    /// </summary>
    /// <remarks>
    /// Callers must ensure the exception is non-null before calling this method.
    /// </remarks>
    private static Task ReportFinalExceptionAsync(
        TestCaseDescriptor testCase,
        ITestExecutionSink sink,
        Exception exception,
        string? output,
        IReadOnlyList<Artifact>? artifacts = null)
    {
        return exception is AssertionFailedException assertionEx
            ? sink.ReportFailedAsync(testCase, assertionEx, output, artifacts)
            : sink.ReportErrorAsync(testCase, exception, output, artifacts);
    }

    /// <summary>
    /// Disposes a test instance if it implements IDisposable or IAsyncDisposable.
    /// </summary>
    private static ValueTask DisposeInstanceAsync(object instance) => DisposeHelper.DisposeAsync(instance);

    /// <summary>
    /// Result of checking skip conditions before test execution.
    /// </summary>
    private readonly struct SkipCheckResult
    {
        public bool ShouldReturn { get; init; }

        public static SkipCheckResult Skip => new() { ShouldReturn = true };
        public static SkipCheckResult Continue => new() { ShouldReturn = false };
    }

    /// <summary>
    /// Result of a single test attempt.
    /// </summary>
    private readonly struct AttemptResult
    {
        /// <summary>The outcome of the test attempt.</summary>
        public AttemptOutcome Outcome { get; init; }

        /// <summary>The exception that caused the failure, if any.</summary>
        public Exception? Exception { get; init; }

        /// <summary>Whether the result is terminal (should not retry).</summary>
        public bool IsTerminal => Outcome != AttemptOutcome.Retriable;

        /// <summary>Test passed successfully (terminal, no retry).</summary>
        public static AttemptResult Passed => new() { Outcome = AttemptOutcome.Passed };

        /// <summary>Test was skipped at runtime (terminal, no retry).</summary>
        public static AttemptResult Skipped(TestSkippedException ex) => new() { Outcome = AttemptOutcome.Skipped, Exception = ex };

        /// <summary>Test timed out (terminal, no retry).</summary>
        public static AttemptResult TimedOut(TestTimeoutException ex) => new() { Outcome = AttemptOutcome.TimedOut, Exception = ex };

        /// <summary>The attempt outcome was already reported to the sink (terminal, no retry).</summary>
        public static AttemptResult Reported => new() { Outcome = AttemptOutcome.Reported };

        /// <summary>Test failed with a retriable exception.</summary>
        public static AttemptResult Retriable(Exception ex) => new() { Outcome = AttemptOutcome.Retriable, Exception = ex };
    }

    /// <summary>
    /// Represents the outcome of a single test attempt.
    /// </summary>
    private enum AttemptOutcome
    {
        /// <summary>Test passed successfully.</summary>
        Passed,

        /// <summary>Test was skipped at runtime.</summary>
        Skipped,

        /// <summary>Test timed out.</summary>
        TimedOut,

        /// <summary>The result was already reported to the sink and needs no further reporting.</summary>
        Reported,

        /// <summary>Test failed and may be retried.</summary>
        Retriable
    }

    /// <summary>
    /// Ensures class-level setup methods have been executed for the test class.
    /// </summary>
    private async Task EnsureClassSetupAsync(TestCaseDescriptor testCase, CancellationToken cancellationToken)
    {
        if (testCase.Lifecycle.BeforeClassMethods.Count == 0 &&
            testCase.Lifecycle.AfterClassMethods.Count == 0)
        {
            return;
        }

        var testClass = testCase.TestClass;

        // Get or create class context (thread-safe)
        var context = _classContexts.GetOrAdd(testClass, _ =>
        {
            var instance = TestInstanceActivator.Create(testCase, NullTestOutput.Instance, NullTestContext.Instance);

            return new ClassExecutionContext
            {
                Instance = instance,
                Lifecycle = testCase.Lifecycle,
                RepresentativeTest = testCase,
                SetupLock = new SemaphoreSlim(1, 1)
            };
        });

        if (Volatile.Read(ref context.SetupExecuted))
        {
            return;
        }

        // Use semaphore to ensure class setup runs only once even in parallel execution
        await context.SetupLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // Execute BeforeClass methods if not already done
            if (!context.SetupExecuted)
            {
                try
                {
                    foreach (var beforeClassMethod in testCase.Lifecycle.BeforeClassMethods)
                    {
                        await beforeClassMethod(context.Instance, cancellationToken).ConfigureAwait(false);
                    }
                }
                catch (TestSkippedException ex)
                {
                    // Class setup requested skip - all tests in this class will be skipped
                    context.SkipReason = ex.Message;
                }
                Volatile.Write(ref context.SetupExecuted, true);
            }
        }
        finally
        {
            context.SetupLock.Release();
        }
    }

    /// <summary>
    /// Executes class-level teardown and disposes class instances.
    /// </summary>
    /// <returns>
    /// The run cancellation observed during cleanup (if any) and every sink-reporting failure, so the
    /// caller can surface a teardown failure even when it could not be delivered to the sink.
    /// </returns>
    private async Task<(OperationCanceledException? Cancellation, List<Exception> Failures)> CleanupClassInstancesAsync(
        ITestExecutionSink sink, CancellationToken cancellationToken)
    {
        OperationCanceledException? cancellation = null;

        // Collect failures while every hook and disposal runs; report only after all cleanup finishes,
        // so a sink failure cannot abort remaining disposal, classes, or assembly teardown.
        var reports = new List<(TestCaseDescriptor Test, Exception Error)>();

        foreach (var kvp in _classContexts)
        {
            var context = kvp.Value;

            // Catch per hook so that a hook observing cancellation (or failing) does not skip the
            // remaining AfterClass hooks of this class.
            var teardownErrors = new List<Exception>();
            foreach (var afterClassMethod in context.Lifecycle.AfterClassMethods)
            {
                try
                {
                    await afterClassMethod(context.Instance, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException ex) when (IsRunCancellation(ex, cancellationToken))
                {
                    // A cancelled run aborting teardown is not a teardown failure, but the cancellation
                    // must still surface: remember it, finish remaining cleanup, then let the caller rethrow.
                    cancellation ??= ex;
                }
                catch (Exception ex) when (!ExceptionHelper.IsCriticalException(ex))
                {
                    // A non-run-cancellation exception (including an OCE carrying a different token) is a
                    // teardown failure, not run cancellation.
                    teardownErrors.Add(ex);
                }
            }

            if (teardownErrors.Count > 0)
            {
                // Aggregate all hook failures for one class into a single node so multiple failures do
                // not collide on the same "[ClassTeardown]" identity.
                reports.Add((
                    CreateClassScopeTest(context.RepresentativeTest, "ClassTeardown"),
                    teardownErrors.Count == 1
                        ? teardownErrors[0]
                        : new AggregateException("One or more class teardown methods failed.", teardownErrors)));
            }

            try
            {
                // Dispose instance regardless of any AfterClass failure above.
                await DisposeHelper.DisposeAsync(context.Instance).ConfigureAwait(false);
            }
            catch (Exception ex) when (!ExceptionHelper.IsCriticalException(ex))
            {
                // Disposal is not passed the run token, so an OCE from a disposer is its own cancellation,
                // not run cancellation; treat every non-critical disposal failure as a cleanup failure.
                reports.Add((CreateClassScopeTest(context.RepresentativeTest, "ClassDispose"), ex));
            }
            finally
            {
                // Dispose semaphore
                context.SetupLock.Dispose();
            }
        }

        _classContexts.Clear();

        // _assemblySetupLock is deliberately NOT disposed here. Its lifetime is the engine's, not a
        // single run's: NextUnitFramework holds one engine in a readonly field and reuses it for every
        // request the platform issues, so disposing the lock at the end of a run would make the next
        // run throw ObjectDisposedException on the assembly-setup gate. Per-class SetupLock instances
        // above are different - they belong to the class contexts this cleanup discards.

        // Report only after all cleanup is complete; guard each report so a sink failure does not
        // prevent the remaining failures from being surfaced. A sink failure is collected (not merely
        // logged) so the caller can fail the run: otherwise a teardown failure whose report also failed
        // would be silently lost.
        var reportFailures = new List<Exception>();
        foreach (var (test, error) in reports)
        {
            try
            {
                await sink.ReportErrorAsync(test, error).ConfigureAwait(false);
            }
            catch (Exception ex) when (!ExceptionHelper.IsCriticalException(ex))
            {
                Diagnostics.SafeWriteError($"[NextUnit] Failed to report class cleanup error for '{test.Id.Value}'", ex);

                // Preserve BOTH the original cleanup error and the sink failure; reporting the sink
                // failure alone would silently lose the teardown/dispose exception it was carrying.
                reportFailures.Add(new AggregateException(
                    $"Failed to report class cleanup error for '{test.Id.Value}'.", error, ex));
            }
        }

        return (cancellation, reportFailures);
    }

    /// <summary>
    /// Builds a synthetic node describing a class-scope cleanup failure.
    /// </summary>
    /// <remarks>
    /// A dedicated node keeps an already-passed test from being retroactively failed while still
    /// surfacing the failure through the same sink used for per-test failures. Teardown and disposal
    /// use distinct node identities so two failures on the same class do not collide.
    /// </remarks>
    private static TestCaseDescriptor CreateClassScopeTest(
        TestCaseDescriptor representativeTest,
        string scope)
    {
        return new TestCaseDescriptor
        {
            Id = new TestCaseId($"{representativeTest.TestClass.FullName ?? representativeTest.TestClass.Name}.[{scope}]"),
            DisplayName = $"{representativeTest.TestClass.Name} ({scope})",
            TestClass = representativeTest.TestClass,
            MethodName = scope
        };
    }

    /// <summary>
    /// Holds execution context for a test class.
    /// </summary>
    private sealed class ClassExecutionContext
    {
        public object Instance { get; init; } = null!;
        public LifecycleInfo Lifecycle { get; init; } = null!;
        public TestCaseDescriptor RepresentativeTest { get; init; } = null!;
        public bool SetupExecuted;
        public string? SkipReason { get; set; }
        public SemaphoreSlim SetupLock { get; init; } = null!;
    }

    /// <summary>
    /// A sink wrapper that tracks test outcomes and reports them to the scheduler.
    /// </summary>
    private sealed class OutcomeTrackingSink : ITestExecutionSink
    {
        private readonly ITestExecutionSink _inner;
        private readonly ParallelScheduler _scheduler;

        public OutcomeTrackingSink(ITestExecutionSink inner, ParallelScheduler scheduler)
        {
            _inner = inner;
            _scheduler = scheduler;
        }

        public Task ReportPassedAsync(TestCaseDescriptor test, string? output = null, IReadOnlyList<Artifact>? artifacts = null)
        {
            _scheduler.ReportOutcome(test.Id, TestOutcome.Passed);
            return _inner.ReportPassedAsync(test, output, artifacts);
        }

        public Task ReportFailedAsync(TestCaseDescriptor test, AssertionFailedException ex, string? output = null, IReadOnlyList<Artifact>? artifacts = null)
        {
            _scheduler.ReportOutcome(test.Id, TestOutcome.Failed);
            return _inner.ReportFailedAsync(test, ex, output, artifacts);
        }

        public Task ReportErrorAsync(TestCaseDescriptor test, Exception ex, string? output = null, IReadOnlyList<Artifact>? artifacts = null)
        {
            _scheduler.ReportOutcome(test.Id, TestOutcome.Error);
            return _inner.ReportErrorAsync(test, ex, output, artifacts);
        }

        public Task ReportSkippedAsync(TestCaseDescriptor test, IReadOnlyList<Artifact>? artifacts = null)
        {
            _scheduler.ReportOutcome(test.Id, TestOutcome.Skipped);
            return _inner.ReportSkippedAsync(test, artifacts);
        }
    }
}
