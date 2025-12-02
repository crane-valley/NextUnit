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
    /// <returns>A task that represents the asynchronous operation.</returns>
    public Task ReportPassedAsync(TestCaseDescriptor test);

    /// <summary>
    /// Reports that a test has failed due to an assertion failure.
    /// </summary>
    /// <param name="test">The test case that failed.</param>
    /// <param name="ex">The assertion exception that caused the failure.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public Task ReportFailedAsync(TestCaseDescriptor test, AssertionFailedException ex);

    /// <summary>
    /// Reports that a test encountered an unexpected error.
    /// </summary>
    /// <param name="test">The test case that encountered an error.</param>
    /// <param name="ex">The exception that was thrown.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public Task ReportErrorAsync(TestCaseDescriptor test, Exception ex);
}

/// <summary>
/// Orchestrates the execution of test cases with support for dependencies, parallelism, and lifecycle hooks.
/// </summary>
public sealed class TestExecutionEngine
{
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
        var graph = DependencyGraph.Build(testCases);
        var scheduler = new ParallelScheduler(graph);

        await foreach (var test in scheduler.GetExecutionOrderAsync(cancellationToken).ConfigureAwait(false))
        {
            await ExecuteSingleAsync(test, sink, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Executes a single test case with its lifecycle hooks.
    /// </summary>
    /// <param name="testCase">The test case to execute.</param>
    /// <param name="sink">The sink for reporting the test result.</param>
    /// <param name="cancellationToken">A cancellation token to observe while waiting for the task to complete.</param>
    /// <returns>A task that represents the asynchronous execution operation.</returns>
    private static async Task ExecuteSingleAsync(
        TestCaseDescriptor testCase,
        ITestExecutionSink sink,
        CancellationToken cancellationToken)
    {
        if (testCase.TestMethod is null)
        {
            await sink.ReportErrorAsync(
                testCase,
                new InvalidOperationException($"Test method delegate is null for test '{testCase.Id.Value}'")).ConfigureAwait(false);
            return;
        }

        var instance = Activator.CreateInstance(testCase.TestClass)!;

        try
        {
            // Execute before lifecycle methods
            foreach (var beforeMethod in testCase.Lifecycle.BeforeTestMethods)
            {
                await beforeMethod(instance, cancellationToken).ConfigureAwait(false);
            }

            // Execute the test method
            await testCase.TestMethod(instance, cancellationToken).ConfigureAwait(false);

            // Execute after lifecycle methods
            foreach (var afterMethod in testCase.Lifecycle.AfterTestMethods)
            {
                await afterMethod(instance, cancellationToken).ConfigureAwait(false);
            }

            await sink.ReportPassedAsync(testCase).ConfigureAwait(false);
        }
        catch (AssertionFailedException ex)
        {
            await sink.ReportFailedAsync(testCase, ex).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            await sink.ReportErrorAsync(testCase, ex).ConfigureAwait(false);
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
}
