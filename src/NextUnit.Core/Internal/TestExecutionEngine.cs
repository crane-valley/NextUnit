using System.Collections.Concurrent;

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
    /// <returns>A task that represents the asynchronous operation.</returns>
    public Task ReportPassedAsync(TestCaseDescriptor test, string? output = null);

    /// <summary>
    /// Reports that a test has failed due to an assertion failure.
    /// </summary>
    /// <param name="test">The test case that failed.</param>
    /// <param name="ex">The assertion exception that caused the failure.</param>
    /// <param name="output">The test output captured during execution, or null if no output.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public Task ReportFailedAsync(TestCaseDescriptor test, AssertionFailedException ex, string? output = null);

    /// <summary>
    /// Reports that a test encountered an unexpected error.
    /// </summary>
    /// <param name="test">The test case that encountered an error.</param>
    /// <param name="ex">The exception that was thrown.</param>
    /// <param name="output">The test output captured during execution, or null if no output.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public Task ReportErrorAsync(TestCaseDescriptor test, Exception ex, string? output = null);

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

        try
        {
            // Execute assembly-level setup
            await ExecuteAssemblySetupAsync(testCasesList, cancellationToken).ConfigureAwait(false);

            // Execute tests in batches with parallel constraints
            await foreach (var batch in scheduler.GetExecutionBatchesAsync(cancellationToken).ConfigureAwait(false))
            {
                await ExecuteBatchAsync(batch, sink, cancellationToken).ConfigureAwait(false);
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
            var requiresTestOutput = testCases[0].RequiresTestOutput;
            var assemblyInstance = requiresTestOutput
                ? Activator.CreateInstance(firstTestClass, NullTestOutput.Instance)!
                : Activator.CreateInstance(firstTestClass)!;

            foreach (var beforeMethod in _assemblyBeforeMethods)
            {
                await beforeMethod(assemblyInstance, cancellationToken).ConfigureAwait(false);
            }

            // Dispose the temporary instance
            if (assemblyInstance is IDisposable disposable)
            {
                disposable.Dispose();
            }
            else if (assemblyInstance is IAsyncDisposable asyncDisposable)
            {
                await asyncDisposable.DisposeAsync().ConfigureAwait(false);
            }

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
        var requiresTestOutput = testCases[0].RequiresTestOutput;
        var assemblyInstance = requiresTestOutput
            ? Activator.CreateInstance(firstTestClass, NullTestOutput.Instance)!
            : Activator.CreateInstance(firstTestClass)!;

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
            if (assemblyInstance is IDisposable disposable)
            {
                disposable.Dispose();
            }
            else if (assemblyInstance is IAsyncDisposable asyncDisposable)
            {
                await asyncDisposable.DisposeAsync().ConfigureAwait(false);
            }
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
        // Check if test is skipped
        if (testCase.IsSkipped)
        {
            await sink.ReportSkippedAsync(testCase).ConfigureAwait(false);
            return;
        }

        if (testCase.TestMethod is null)
        {
            await sink.ReportErrorAsync(
                testCase,
                new InvalidOperationException($"Test method delegate is null for test '{testCase.Id.Value}'")).ConfigureAwait(false);
            return;
        }

        // Execute class-level setup if not already done
        await EnsureClassSetupAsync(testCase, cancellationToken).ConfigureAwait(false);

        // Create test output capture if needed
        TestOutputCapture? testOutput = testCase.RequiresTestOutput ? new TestOutputCapture() : null;

        // Create test instance (each test gets its own instance)
        var instance = testCase.RequiresTestOutput
            ? Activator.CreateInstance(testCase.TestClass, testOutput)!
            : Activator.CreateInstance(testCase.TestClass)!;

        try
        {
            // Execute before lifecycle methods (test-scoped)
            foreach (var beforeMethod in testCase.Lifecycle.BeforeTestMethods)
            {
                await beforeMethod(instance, cancellationToken).ConfigureAwait(false);
            }

            // Execute the test method
            await testCase.TestMethod(instance, cancellationToken).ConfigureAwait(false);

            // Execute after lifecycle methods (test-scoped)
            foreach (var afterMethod in testCase.Lifecycle.AfterTestMethods)
            {
                await afterMethod(instance, cancellationToken).ConfigureAwait(false);
            }

            await sink.ReportPassedAsync(testCase, testOutput?.GetOutput()).ConfigureAwait(false);
        }
        catch (TestSkippedException ex)
        {
            // Runtime skip - use WithSkipReason to create a modified descriptor
            await sink.ReportSkippedAsync(testCase.WithSkipReason(ex.Message)).ConfigureAwait(false);
        }
        catch (AssertionFailedException ex)
        {
            await sink.ReportFailedAsync(testCase, ex, testOutput?.GetOutput()).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            await sink.ReportErrorAsync(testCase, ex, testOutput?.GetOutput()).ConfigureAwait(false);
        }
        finally
        {
            if (instance is IDisposable disposable)
            {
                disposable.Dispose();
            }
            else if (instance is IAsyncDisposable asyncDisposable)
            {
                await asyncDisposable.DisposeAsync().ConfigureAwait(false);
            }
        }
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
            var instance = testCase.RequiresTestOutput
                ? Activator.CreateInstance(testClass, NullTestOutput.Instance)!
                : Activator.CreateInstance(testClass)!;

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
                foreach (var beforeClassMethod in testCase.Lifecycle.BeforeClassMethods)
                {
                    await beforeClassMethod(context.Instance, cancellationToken).ConfigureAwait(false);
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
                if (context.Instance is IDisposable disposable)
                {
                    disposable.Dispose();
                }
                else if (context.Instance is IAsyncDisposable asyncDisposable)
                {
                    await asyncDisposable.DisposeAsync().ConfigureAwait(false);
                }
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
        public SemaphoreSlim SetupLock { get; init; } = null!;
    }
}
