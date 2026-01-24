using System.Collections.Concurrent;
using System.Reflection;
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
    /// <returns>A task that represents the asynchronous operation.</returns>
    public Task ReportSkippedAsync(TestCaseDescriptor test);
}

/// <summary>
/// Orchestrates the execution of test cases with support for dependencies, parallelism, and lifecycle hooks.
/// </summary>
public sealed class TestExecutionEngine
{
    private readonly ConcurrentDictionary<Type, ClassExecutionContext> _classContexts = new();
    private readonly SemaphoreSlim _assemblySetupLock = new(1, 1);
    private bool _assemblySetupExecuted;
    private string? _assemblySkipReason;
    private readonly List<LifecycleMethodDelegate> _assemblyBeforeMethods = new();
    private readonly List<LifecycleMethodDelegate> _assemblyAfterMethods = new();

    /// <summary>
    /// Runs a collection of test cases asynchronously.
    /// </summary>
    /// <param name="testCases">The test cases to execute.</param>
    /// <param name="sink">The sink for reporting test results.</param>
    /// <param name="cancellationToken">A cancellation token to observe while waiting for the task to complete.</param>
    /// <returns>A task that represents the asynchronous execution operation.</returns>
    public async Task RunAsync(
        IEnumerable<TestCaseDescriptor> testCases,
        ITestExecutionSink sink,
        CancellationToken cancellationToken)
    {
        var testCasesList = testCases.ToList();

        // Collect assembly-level lifecycle methods from the first test
        if (testCasesList.Count > 0)
        {
            var firstTest = testCasesList[0];
            _assemblyBeforeMethods.AddRange(firstTest.Lifecycle.BeforeAssemblyMethods);
            _assemblyAfterMethods.AddRange(firstTest.Lifecycle.AfterAssemblyMethods);
        }

        var graph = DependencyGraph.Build(testCasesList);
        var scheduler = new ParallelScheduler(graph);

        // Wrap sink to track outcomes for ProceedOnFailure support
        var trackingSink = new OutcomeTrackingSink(sink, scheduler);

        try
        {
            // Execute assembly-level setup
            await ExecuteAssemblySetupAsync(testCasesList, cancellationToken).ConfigureAwait(false);

            // Execute tests in batches with parallel constraints
            await foreach (var batch in scheduler.GetExecutionBatchesAsync(cancellationToken).ConfigureAwait(false))
            {
                await ExecuteBatchAsync(batch, trackingSink, cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            // Execute class teardown and cleanup
            await CleanupClassInstancesAsync(cancellationToken).ConfigureAwait(false);

            // Execute assembly-level teardown
            await ExecuteAssemblyTeardownAsync(testCasesList, cancellationToken).ConfigureAwait(false);
        }
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
        if (testCases.Count == 0)
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

            // Use the first test class for assembly-level lifecycle
            var firstTestClass = testCases[0].TestClass;
            var assemblyInstance = CreateTestInstance(firstTestClass, NullTestOutput.Instance, NullTestContext.Instance);

            try
            {
                foreach (var beforeMethod in _assemblyBeforeMethods)
                {
                    await beforeMethod(assemblyInstance, cancellationToken).ConfigureAwait(false);
                }
            }
            catch (TestSkippedException ex)
            {
                // Assembly setup requested skip - all tests will be skipped
                _assemblySkipReason = ex.Message;
            }

            // Dispose the temporary instance
            await DisposeHelper.DisposeAsync(assemblyInstance).ConfigureAwait(false);

            _assemblySetupExecuted = true;
        }
        finally
        {
            _assemblySetupLock.Release();
        }
    }

    /// <summary>
    /// Executes assembly-level teardown methods.
    /// </summary>
    private async Task ExecuteAssemblyTeardownAsync(List<TestCaseDescriptor> testCases, CancellationToken cancellationToken)
    {
        if (_assemblyAfterMethods.Count == 0 || testCases.Count == 0)
        {
            return;
        }

        // Use the first test class for assembly-level lifecycle
        var firstTestClass = testCases[0].TestClass;
        var assemblyInstance = CreateTestInstance(firstTestClass, NullTestOutput.Instance, NullTestContext.Instance);

        try
        {
            foreach (var afterMethod in _assemblyAfterMethods)
            {
                await afterMethod(assemblyInstance, cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            // Dispose the temporary instance
            await DisposeHelper.DisposeAsync(assemblyInstance).ConfigureAwait(false);
        }
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

        // Create a combined cancellation token if timeout is specified
        using var timeoutCts = testCase.TimeoutMs.HasValue
            ? new CancellationTokenSource(testCase.TimeoutMs.Value)
            : null;
        using var linkedCts = timeoutCts is not null
            ? CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token)
            : null;
        var effectiveToken = linkedCts?.Token ?? cancellationToken;

        // Set up test context for async-local access
        TestContext.SetCurrent(CreateTestContext(testCase, effectiveToken, new TestOutputCapture()));

        try
        {
            await ExecuteWithRetryAsync(testCase, sink, effectiveToken, timeoutCts, cancellationToken).ConfigureAwait(false);
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

        if (testCase.TestMethod is null)
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
            assemblyName: testCase.TestClass.Assembly.GetName().Name ?? "",
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
        CancellationToken effectiveToken,
        CancellationTokenSource? timeoutCts,
        CancellationToken cancellationToken)
    {
        var maxAttempts = testCase.Retry.Count ?? 1;
        var retryDelayMs = testCase.Retry.DelayMs;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            var testOutput = new TestOutputCapture();
            var testContext = CreateTestContext(testCase, effectiveToken, testOutput);
            TestContext.SetCurrent(testContext);

            var attemptResult = await ExecuteSingleAttemptAsync(
                testCase, sink, effectiveToken, timeoutCts, testOutput, cancellationToken).ConfigureAwait(false);

            if (attemptResult.IsTerminal)
            {
                return;
            }

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
        var instance = CreateTestInstance(testCase.TestClass, testOutput, currentContext);

        try
        {
            // Execute before lifecycle methods (test-scoped)
            foreach (var beforeMethod in testCase.Lifecycle.BeforeTestMethods)
            {
                await beforeMethod(instance, effectiveToken).ConfigureAwait(false);
            }

            // Execute the test method
            // TestMethod is guaranteed non-null because CheckSkipConditionsAsync validates it before execution
            await testCase.TestMethod!(instance, effectiveToken).ConfigureAwait(false);

            // Execute after lifecycle methods (test-scoped)
            foreach (var afterMethod in testCase.Lifecycle.AfterTestMethods)
            {
                await afterMethod(instance, effectiveToken).ConfigureAwait(false);
            }

            // Test passed - report success with artifacts
            var artifacts = currentContext.Artifacts;
            await sink.ReportPassedAsync(testCase, testOutput.GetOutput(), artifacts).ConfigureAwait(false);
            return AttemptResult.Passed;
        }
        catch (OperationCanceledException) when (timeoutCts?.IsCancellationRequested == true && !cancellationToken.IsCancellationRequested)
        {
            // Timeout occurred - do not retry timeouts
            var timeoutEx = new TestTimeoutException(testCase.TimeoutMs!.Value);
            var artifacts = currentContext.Artifacts;
            await sink.ReportErrorAsync(testCase, timeoutEx, testOutput.GetOutput(), artifacts).ConfigureAwait(false);
            return AttemptResult.TimedOut;
        }
        catch (TestSkippedException ex)
        {
            // Runtime skip - do not retry skips
            await sink.ReportSkippedAsync(testCase.WithSkipReason(ex.Message)).ConfigureAwait(false);
            return AttemptResult.Skipped;
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
        finally
        {
            await DisposeInstanceAsync(instance).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Reports the final exception after all retry attempts are exhausted.
    /// </summary>
    /// <remarks>
    /// Callers must ensure the exception is non-null before calling this method.
    /// </remarks>
    private static async Task ReportFinalExceptionAsync(
        TestCaseDescriptor testCase,
        ITestExecutionSink sink,
        Exception exception,
        string? output,
        IReadOnlyList<Artifact>? artifacts = null)
    {
        if (exception is AssertionFailedException assertionEx)
        {
            await sink.ReportFailedAsync(testCase, assertionEx, output, artifacts).ConfigureAwait(false);
        }
        else
        {
            await sink.ReportErrorAsync(testCase, exception, output, artifacts).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Disposes a test instance if it implements IDisposable or IAsyncDisposable.
    /// </summary>
    private static async Task DisposeInstanceAsync(object instance)
    {
        await DisposeHelper.DisposeAsync(instance).ConfigureAwait(false);
    }

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
        public static AttemptResult Skipped => new() { Outcome = AttemptOutcome.Skipped };

        /// <summary>Test timed out (terminal, no retry).</summary>
        public static AttemptResult TimedOut => new() { Outcome = AttemptOutcome.TimedOut };

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

        /// <summary>Test failed and may be retried.</summary>
        Retriable
    }

    /// <summary>
    /// Creates a test class instance with appropriate constructor injection.
    /// </summary>
    /// <param name="testClass">The type of test class to instantiate.</param>
    /// <param name="testOutput">The test output capture to inject into the constructor.</param>
    /// <param name="testContext">The test context capture to inject into the constructor.</param>
    /// <returns>A new instance of the test class.</returns>
    private static object CreateTestInstance(Type testClass, ITestOutput testOutput, ITestContext testContext)
    {
        // Find the best matching constructor in a single pass
        // Priority: 2-param > 1-param ITestContext > 1-param ITestOutput > parameterless
        var constructors = testClass.GetConstructors();

        ConstructorInfo? twoParamCtor = null;
        bool twoParamContextFirst = false;
        ConstructorInfo? contextOnlyCtor = null;
        ConstructorInfo? outputOnlyCtor = null;
        ConstructorInfo? parameterlessCtor = null;

        foreach (var ctor in constructors)
        {
            var parameters = ctor.GetParameters();

            switch (parameters.Length)
            {
                case 0:
                    parameterlessCtor = ctor;
                    break;
                case 1:
                    if (parameters[0].ParameterType == typeof(ITestContext))
                    {
                        contextOnlyCtor = ctor;
                    }
                    else if (parameters[0].ParameterType == typeof(ITestOutput))
                    {
                        outputOnlyCtor = ctor;
                    }
                    break;
                case 2:
                    var param0Type = parameters[0].ParameterType;
                    var param1Type = parameters[1].ParameterType;
                    if (param0Type == typeof(ITestContext) && param1Type == typeof(ITestOutput))
                    {
                        twoParamCtor = ctor;
                        twoParamContextFirst = true;
                    }
                    else if (param0Type == typeof(ITestOutput) && param1Type == typeof(ITestContext))
                    {
                        twoParamCtor = ctor;
                        twoParamContextFirst = false;
                    }
                    break;
            }
        }

        // Return based on priority
        if (twoParamCtor is not null)
        {
            return twoParamContextFirst
                ? twoParamCtor.Invoke([testContext, testOutput])
                : twoParamCtor.Invoke([testOutput, testContext]);
        }

        if (contextOnlyCtor is not null)
        {
            return contextOnlyCtor.Invoke([testContext]);
        }

        if (outputOnlyCtor is not null)
        {
            return outputOnlyCtor.Invoke([testOutput]);
        }

        if (parameterlessCtor is not null)
        {
            return parameterlessCtor.Invoke([]);
        }

        return Activator.CreateInstance(testClass)!;
    }

    /// <summary>
    /// Ensures class-level setup methods have been executed for the test class.
    /// </summary>
    private async Task EnsureClassSetupAsync(TestCaseDescriptor testCase, CancellationToken cancellationToken)
    {
        var testClass = testCase.TestClass;

        // Get or create class context (thread-safe)
        var context = _classContexts.GetOrAdd(testClass, _ =>
        {
            var instance = CreateTestInstance(testClass, NullTestOutput.Instance, NullTestContext.Instance);

            return new ClassExecutionContext
            {
                Instance = instance,
                Lifecycle = testCase.Lifecycle,
                SetupLock = new SemaphoreSlim(1, 1)
            };
        });

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
                context.SetupExecuted = true;
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
    private async Task CleanupClassInstancesAsync(CancellationToken cancellationToken)
    {
        foreach (var kvp in _classContexts)
        {
            var context = kvp.Value;

            try
            {
                // Execute AfterClass methods
                foreach (var afterClassMethod in context.Lifecycle.AfterClassMethods)
                {
                    await afterClassMethod(context.Instance, cancellationToken).ConfigureAwait(false);
                }

                // Dispose instance
                await DisposeHelper.DisposeAsync(context.Instance).ConfigureAwait(false);
            }
            catch
            {
                // Suppress cleanup errors
            }
            finally
            {
                // Dispose semaphore
                context.SetupLock.Dispose();
            }
        }

        _classContexts.Clear();
        _assemblySetupLock.Dispose();
    }

    /// <summary>
    /// Holds execution context for a test class.
    /// </summary>
    private sealed class ClassExecutionContext
    {
        public object Instance { get; init; } = null!;
        public LifecycleInfo Lifecycle { get; init; } = null!;
        public bool SetupExecuted { get; set; }
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

        public async Task ReportPassedAsync(TestCaseDescriptor test, string? output = null, IReadOnlyList<Artifact>? artifacts = null)
        {
            _scheduler.ReportOutcome(test.Id, TestOutcome.Passed);
            await _inner.ReportPassedAsync(test, output, artifacts).ConfigureAwait(false);
        }

        public async Task ReportFailedAsync(TestCaseDescriptor test, AssertionFailedException ex, string? output = null, IReadOnlyList<Artifact>? artifacts = null)
        {
            _scheduler.ReportOutcome(test.Id, TestOutcome.Failed);
            await _inner.ReportFailedAsync(test, ex, output, artifacts).ConfigureAwait(false);
        }

        public async Task ReportErrorAsync(TestCaseDescriptor test, Exception ex, string? output = null, IReadOnlyList<Artifact>? artifacts = null)
        {
            _scheduler.ReportOutcome(test.Id, TestOutcome.Error);
            await _inner.ReportErrorAsync(test, ex, output, artifacts).ConfigureAwait(false);
        }

        public async Task ReportSkippedAsync(TestCaseDescriptor test)
        {
            _scheduler.ReportOutcome(test.Id, TestOutcome.Skipped);
            await _inner.ReportSkippedAsync(test).ConfigureAwait(false);
        }
    }
}
