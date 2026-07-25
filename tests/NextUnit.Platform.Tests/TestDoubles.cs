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

    public Task ReportPassedAsync(TestCaseDescriptor test, string? output = null, IReadOnlyList<Artifact>? artifacts = null)
    {
        lock (_lock)
        {
            Passed.Add(test);
        }

        return Task.CompletedTask;
    }

    public Task ReportFailedAsync(TestCaseDescriptor test, AssertionFailedException ex, string? output = null, IReadOnlyList<Artifact>? artifacts = null)
    {
        lock (_lock)
        {
            Failed.Add((test, ex));
        }

        return Task.CompletedTask;
    }

    public Task ReportErrorAsync(TestCaseDescriptor test, Exception ex, string? output = null, IReadOnlyList<Artifact>? artifacts = null)
    {
        lock (_lock)
        {
            Errors.Add((test, ex));
        }

        return Task.CompletedTask;
    }

    public Task ReportSkippedAsync(TestCaseDescriptor test, IReadOnlyList<Artifact>? artifacts = null)
    {
        lock (_lock)
        {
            Skipped.Add(test);
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
