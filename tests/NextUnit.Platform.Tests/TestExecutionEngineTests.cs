using NextUnit.Internal;

namespace NextUnit.Platform.Tests;

/// <summary>
/// Runtime behavior tests for <see cref="TestExecutionEngine"/> and the platform filter loading,
/// covering the cancellation, timeout, and teardown reporting guarantees.
/// </summary>
public sealed class TestExecutionEngineTests
{
    [Test]
    public async Task Timeout_IsPerAttempt_NotWholeRetryBudgetAsync()
    {
        // Each attempt stays under the timeout, but their cumulative duration exceeds it.
        // A per-test shared timeout source would report TimedOut; a per-attempt source lets the test pass.
        var attempts = 0;
        var test = new TestCaseDescriptor
        {
            Id = new TestCaseId("timeout.per.attempt"),
            DisplayName = "timeout.per.attempt",
            TestClass = typeof(TestExecutionEngineTests),
            MethodName = "PerAttempt",
            TimeoutMs = 1000,
            Retry = new RetryInfo { Count = 3 },
            TestMethod = async (_, ct) =>
            {
                var current = Interlocked.Increment(ref attempts);
                await Task.Delay(400, ct);
                if (current < 3)
                {
                    throw new InvalidOperationException("transient failure");
                }
            }
        };

        var sink = new RecordingSink();
        await new TestExecutionEngine().RunAsync([test], sink, CancellationToken.None);

        Assert.Equal(3, attempts);
        Assert.Single(sink.Passed);
        Assert.Empty(sink.Errors);
    }

    [Test]
    public async Task OuterCancellation_IsNotReportedAsErrorAndNotRetriedAsync()
    {
        using var cts = new CancellationTokenSource();
        var attempts = 0;
        var test = new TestCaseDescriptor
        {
            Id = new TestCaseId("cancel.no.retry"),
            DisplayName = "cancel.no.retry",
            TestClass = typeof(TestExecutionEngineTests),
            MethodName = "Cancel",
            Retry = new RetryInfo { Count = 3 },
            TestMethod = (_, ct) =>
            {
                Interlocked.Increment(ref attempts);
                cts.Cancel();
                ct.ThrowIfCancellationRequested();
                return Task.CompletedTask;
            }
        };

        var sink = new RecordingSink();

        // The run token firing must propagate as cancellation, not be swallowed into an error.
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => new TestExecutionEngine().RunAsync([test], sink, cts.Token));

        Assert.Equal(1, attempts);
        Assert.Empty(sink.Errors);
    }

    [Test]
    public async Task ClassTeardownException_IsReportedAgainstClassScopeAsync()
    {
        var test = new TestCaseDescriptor
        {
            Id = new TestCaseId("teardown.reported"),
            DisplayName = "teardown.reported",
            TestClass = typeof(SampleTestClass),
            MethodName = "Ok",
            TestClassFactory = static (_, _) => new SampleTestClass(),
            TestMethod = static (_, _) => Task.CompletedTask,
            Lifecycle = new LifecycleInfo
            {
                AfterClassMethods =
                [
                    static (_, _) => throw new InvalidOperationException("teardown boom")
                ]
            }
        };

        var sink = new RecordingSink();
        await new TestExecutionEngine().RunAsync([test], sink, CancellationToken.None);

        // The passing test is not retroactively failed; the teardown error surfaces on a class-scope node.
        Assert.Single(sink.Passed);
        var error = Assert.Single(sink.Errors);
        Assert.Contains("teardown boom", error.Exception.Message);
        Assert.True(error.Test.Id.Value.EndsWith("[ClassTeardown]", StringComparison.Ordinal));
    }

    [Test]
    public void InvalidTestNameRegex_SurfacesErrorInsteadOfRunningEverything()
    {
        const string envVar = "NEXTUNIT_TEST_NAME_REGEX";
        var original = Environment.GetEnvironmentVariable(envVar);
        Environment.SetEnvironmentVariable(envVar, "(unclosed");
        try
        {
            Assert.Throws<ArgumentException>(
                () => new NextUnitFramework(null!, new NullServiceProvider()));
        }
        finally
        {
            Environment.SetEnvironmentVariable(envVar, original);
        }
    }

    private sealed class SampleTestClass
    {
    }

    private sealed class NullServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }

    private sealed class RecordingSink : ITestExecutionSink
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
}
