using NextUnit.Internal;

namespace NextUnit.Platform.Tests;

/// <summary>
/// Captures every result the engine reports, so a test can assert on which scope a result landed on.
/// </summary>
internal sealed class RecordingSink : ITestExecutionSink
{
    private readonly object _lock = new();

    public List<TestCaseDescriptor> Passed { get; } = [];
    public List<TestCaseDescriptor> Skipped { get; } = [];
    public List<(TestCaseDescriptor Test, Exception Exception)> Errors { get; } = [];
    public List<(TestCaseDescriptor Test, AssertionFailedException Exception)> Failed { get; } = [];

    /// <summary>
    /// The output and artifacts delivered with each result, in report order. Retry publishes only the
    /// last attempt's output and artifacts, so tests assert on what actually reached the adapter.
    /// </summary>
    public List<(string? Output, IReadOnlyList<Artifact>? Artifacts)> Reports { get; } = [];

    public Task ReportPassedAsync(TestCaseDescriptor test, string? output = null, IReadOnlyList<Artifact>? artifacts = null)
    {
        lock (_lock)
        {
            Passed.Add(test);
            Reports.Add((output, artifacts));
        }

        return Task.CompletedTask;
    }

    public Task ReportFailedAsync(TestCaseDescriptor test, AssertionFailedException ex, string? output = null, IReadOnlyList<Artifact>? artifacts = null)
    {
        lock (_lock)
        {
            Failed.Add((test, ex));
            Reports.Add((output, artifacts));
        }

        return Task.CompletedTask;
    }

    public Task ReportErrorAsync(TestCaseDescriptor test, Exception ex, string? output = null, IReadOnlyList<Artifact>? artifacts = null)
    {
        lock (_lock)
        {
            Errors.Add((test, ex));
            Reports.Add((output, artifacts));
        }

        return Task.CompletedTask;
    }

    public Task ReportSkippedAsync(TestCaseDescriptor test, IReadOnlyList<Artifact>? artifacts = null)
    {
        lock (_lock)
        {
            Skipped.Add(test);
            Reports.Add((null, artifacts));
        }

        return Task.CompletedTask;
    }
}

/// <summary>
/// Fails every error report, standing in for an adapter whose message bus is down. Counts the
/// attempts so a test can prove reporting continued after the first failure.
/// </summary>
internal sealed class ThrowingReportSink : ITestExecutionSink
{
    private int _errorReportAttempts;

    public int ErrorReportAttempts => Volatile.Read(ref _errorReportAttempts);

    public Task ReportPassedAsync(TestCaseDescriptor test, string? output = null, IReadOnlyList<Artifact>? artifacts = null) => Task.CompletedTask;

    public Task ReportFailedAsync(TestCaseDescriptor test, AssertionFailedException ex, string? output = null, IReadOnlyList<Artifact>? artifacts = null) => Task.CompletedTask;

    public Task ReportErrorAsync(TestCaseDescriptor test, Exception ex, string? output = null, IReadOnlyList<Artifact>? artifacts = null)
    {
        Interlocked.Increment(ref _errorReportAttempts);
        throw new InvalidOperationException("sink is down");
    }

    public Task ReportSkippedAsync(TestCaseDescriptor test, IReadOnlyList<Artifact>? artifacts = null) => Task.CompletedTask;
}

/// <summary>
/// Resolves nothing, so the framework under test falls back to its own defaults.
/// </summary>
internal sealed class NullServiceProvider : IServiceProvider
{
    public object? GetService(Type serviceType) => null;
}
