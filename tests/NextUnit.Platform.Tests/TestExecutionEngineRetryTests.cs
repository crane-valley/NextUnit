using NextUnit.Core;
using NextUnit.Internal;

namespace NextUnit.Platform.Tests;

/// <summary>
/// Behavior tests for selective retry: the <see cref="IRetryPolicy"/> decision point, the attempt
/// number exposed on <see cref="ITestContext"/>, and what carries across attempts.
/// </summary>
public sealed class TestExecutionEngineRetryTests
{
    [Test]
    public async Task NoPolicy_RetriesEveryFailureAsync()
    {
        var attempts = 0;
        var test = TestCaseDescriptorBuilder
            .For<SampleTestClass>("retry.default.all")
            .WithRetry(3, delayMs: 0)
            .WithMethod((_, _) =>
            {
                Interlocked.Increment(ref attempts);
                throw new InvalidOperationException("boom");
            })
            .Build();

        var sink = new RecordingSink();
        await new TestExecutionEngine().RunAsync([test], sink, CancellationToken.None);

        // The compatibility default: without a policy every retriable failure is retried, so the
        // budget is spent in full.
        Assert.Equal(3, attempts);
        Assert.Single(sink.Errors);
    }

    [Test]
    public async Task Policy_DecliningRetry_StopsAfterTheFailingAttemptAsync()
    {
        var attempts = 0;
        var policy = new RecordingRetryPolicy(static _ => false);
        var test = TestCaseDescriptorBuilder
            .For<SampleTestClass>("retry.policy.declines")
            .WithRetryPolicy(3, () => policy)
            .WithMethod((_, _) =>
            {
                Interlocked.Increment(ref attempts);
                throw new InvalidOperationException("not worth retrying");
            })
            .Build();

        var sink = new RecordingSink();
        await new TestExecutionEngine().RunAsync([test], sink, CancellationToken.None);

        Assert.Equal(1, attempts);
        var error = Assert.Single(sink.Errors);

        // The test's own exception is reported unchanged; declining a retry is not a failure of its own.
        Assert.Contains("not worth retrying", error.Exception.Message);
    }

    [Test]
    public async Task Policy_SelectingByExceptionType_RetriesOnlyMatchingFailuresAsync()
    {
        var attempts = 0;
        var policy = new RecordingRetryPolicy(static context => context.Exception is TimeoutException);
        var test = TestCaseDescriptorBuilder
            .For<SampleTestClass>("retry.policy.by.exception")
            .WithRetryPolicy(4, () => policy)
            .WithMethod((_, _) =>
            {
                var attempt = Interlocked.Increment(ref attempts);

                // Retriable twice, then a failure the policy refuses to retry.
                throw attempt < 3
                    ? new TimeoutException("transient")
                    : new InvalidOperationException("permanent");
            })
            .Build();

        var sink = new RecordingSink();
        await new TestExecutionEngine().RunAsync([test], sink, CancellationToken.None);

        // Two retries granted, then the third failure ends it with one attempt of budget unused.
        Assert.Equal(3, attempts);
        var error = Assert.Single(sink.Errors);
        Assert.Contains("permanent", error.Exception.Message);
    }

    [Test]
    public async Task Policy_ReceivesFailureContextAndOneBasedAttemptAsync()
    {
        var policy = new RecordingRetryPolicy(static _ => true);
        var test = TestCaseDescriptorBuilder
            .For<SampleTestClass>("retry.policy.context")
            .WithMethodName("Boom")
            .WithRetryPolicy(3, () => policy)
            .WithMethod(static (_, _) => throw new InvalidOperationException("boom"))
            .Build();

        var sink = new RecordingSink();
        await new TestExecutionEngine().RunAsync([test], sink, CancellationToken.None);

        // Consulted after each failure that has a further attempt available, and never after the last:
        // three attempts produce two decisions.
        Assert.Equal(2, policy.Decisions.Count);

        Assert.Equal(1, policy.Decisions[0].Attempt);
        Assert.Equal(2, policy.Decisions[1].Attempt);
        Assert.Equal(3, policy.Decisions[0].MaxAttempts);
        Assert.Equal("boom", policy.Decisions[0].ExceptionMessage);
        Assert.Equal("retry.policy.context", policy.Decisions[0].FullyQualifiedName);

        // The context handed to the policy belongs to the attempt that just failed.
        Assert.Equal(1, policy.Decisions[0].ContextRetryAttempt);
        Assert.Equal(2, policy.Decisions[1].ContextRetryAttempt);
    }

    [Test]
    public async Task Policy_IsCreatedOncePerTestExecutionAsync()
    {
        var created = 0;
        var test = TestCaseDescriptorBuilder
            .For<SampleTestClass>("retry.policy.instance")
            .WithRetryPolicy(3, () =>
            {
                Interlocked.Increment(ref created);
                return new RecordingRetryPolicy(static _ => true);
            })
            .WithMethod(static (_, _) => throw new InvalidOperationException("boom"))
            .Build();

        var sink = new RecordingSink();
        await new TestExecutionEngine().RunAsync([test], sink, CancellationToken.None);

        // Two decisions, one instance: a stateful policy sees one test's attempts as one sequence.
        Assert.Equal(1, created);
    }

    [Test]
    public async Task PassingTest_NeverConsultsThePolicyAsync()
    {
        var created = 0;
        var test = TestCaseDescriptorBuilder
            .For<SampleTestClass>("retry.policy.unused")
            .WithRetryPolicy(3, () =>
            {
                Interlocked.Increment(ref created);
                return new RecordingRetryPolicy(static _ => true);
            })
            .Build();

        var sink = new RecordingSink();
        await new TestExecutionEngine().RunAsync([test], sink, CancellationToken.None);

        // A passing test must not pay for a policy it never needs.
        Assert.Single(sink.Passed);
        Assert.Equal(0, created);
    }

    [Test]
    public async Task Policy_ThrowingWhileDeciding_ReportsBothFailuresAndStopsAsync()
    {
        var attempts = 0;
        var test = TestCaseDescriptorBuilder
            .For<SampleTestClass>("retry.policy.throws")
            .WithRetryPolicy(3, static () => new ThrowingRetryPolicy())
            .WithMethod((_, _) =>
            {
                Interlocked.Increment(ref attempts);
                throw new InvalidOperationException("test boom");
            })
            .Build();

        var sink = new RecordingSink();
        await new TestExecutionEngine().RunAsync([test], sink, CancellationToken.None);

        // A policy that cannot decide must not be read as either answer, so no further attempt runs.
        Assert.Equal(1, attempts);
        var error = Assert.Single(sink.Errors);
        var aggregate = AsAggregate(error.Exception);

        // The test's own failure stays first so the policy failure cannot mask it.
        Assert.Equal(2, aggregate.InnerExceptions.Count);
        Assert.Contains("test boom", aggregate.InnerExceptions[0].Message);
        Assert.Contains("policy boom", aggregate.InnerExceptions[1].Message);
    }

    [Test]
    public async Task Policy_ForeignCancellation_IsReportedAsAPolicyFailureAsync()
    {
        var test = TestCaseDescriptorBuilder
            .For<SampleTestClass>("retry.policy.foreign.oce")
            .WithRetryPolicy(
                3,
                static () => new DelegatingRetryPolicy(
                    static _ => throw new OperationCanceledException(new CancellationToken(canceled: true))))
            .WithMethod(static (_, _) => throw new InvalidOperationException("test boom"))
            .Build();

        var sink = new RecordingSink();

        // The run token never fires, so the policy's own cancellation is a failure. Classifying it as
        // run cancellation would make the adapters swallow it and report nothing.
        await new TestExecutionEngine().RunAsync([test], sink, CancellationToken.None);

        var error = Assert.Single(sink.Errors);
        var aggregate = AsAggregate(error.Exception);
        Assert.True(aggregate.InnerExceptions[1] is OperationCanceledException);
    }

    [Test]
    public async Task Policy_ObservingRunCancellation_PropagatesCancellationAsync()
    {
        using var cts = new CancellationTokenSource();
        var test = TestCaseDescriptorBuilder
            .For<SampleTestClass>("retry.policy.run.cancel")
            .Serial()
            .WithRetryPolicy(
                3,
                () => new DelegatingRetryPolicy(context =>
                {
                    // The policy is the first place the cancelled run is observed.
                    cts.Cancel();
                    context.CancellationToken.ThrowIfCancellationRequested();
                    return true;
                }))
            .WithMethod(static (_, _) => throw new InvalidOperationException("test boom"))
            .Build();

        var sink = new RecordingSink();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => new TestExecutionEngine().RunAsync([test], sink, cts.Token));

        // The run ending is not the policy failing, so nothing is reported against the test.
        Assert.Empty(sink.Errors);
    }

    [Test]
    public async Task RetryAttempt_IsOneBasedAndIncrementsPerAttemptAsync()
    {
        var observed = new List<int>();
        var test = TestCaseDescriptorBuilder
            .For<SampleTestClass>("retry.attempt.visible")
            .WithRetry(3, delayMs: 0)
            .WithMethod((_, _) =>
            {
                observed.Add(TestContext.Current!.RetryAttempt);
                throw new InvalidOperationException("boom");
            })
            .Build();

        var sink = new RecordingSink();
        await new TestExecutionEngine().RunAsync([test], sink, CancellationToken.None);

        Assert.Equal("1,2,3", string.Join(",", observed));
    }

    [Test]
    public async Task RetryAttempt_IsOneForATestWithoutRetryAsync()
    {
        var observed = 0;
        var test = TestCaseDescriptorBuilder
            .For<SampleTestClass>("retry.attempt.default")
            .WithMethod((_, _) =>
            {
                observed = TestContext.Current!.RetryAttempt;
                return Task.CompletedTask;
            })
            .Build();

        var sink = new RecordingSink();
        await new TestExecutionEngine().RunAsync([test], sink, CancellationToken.None);

        // Every test runs an attempt, so the number is meaningful with or without [Retry].
        Assert.Equal(1, observed);
    }

    [Test]
    public async Task ExhaustedRetry_ReportsTotalAttemptsInTheFailureOutputAsync()
    {
        var test = TestCaseDescriptorBuilder
            .For<SampleTestClass>("retry.output.exhausted")
            .WithRetry(3, delayMs: 0)
            .WithMethod(static (_, _) => throw new InvalidOperationException("boom"))
            .Build();

        var sink = new RecordingSink();
        await new TestExecutionEngine().RunAsync([test], sink, CancellationToken.None);

        Assert.Single(sink.Errors);
        var report = Assert.Single(sink.Reports);
        Assert.Contains("[NextUnit] Test failed after 3 of 3 attempts.", report.Output!);
    }

    [Test]
    public async Task PolicyStoppedRetry_ReportsTheAttemptsActuallyRunAsync()
    {
        var test = TestCaseDescriptorBuilder
            .For<SampleTestClass>("retry.output.stopped")
            .WithRetryPolicy(5, static () => new DelegatingRetryPolicy(static context => context.Attempt < 2))
            .WithMethod(static (_, _) => throw new InvalidOperationException("boom"))
            .Build();

        var sink = new RecordingSink();
        await new TestExecutionEngine().RunAsync([test], sink, CancellationToken.None);

        // Attempts run, not attempts budgeted: the difference is the whole point of a policy.
        var report = Assert.Single(sink.Reports);
        Assert.Contains("[NextUnit] Test failed after 2 of 5 attempts.", report.Output!);
    }

    [Test]
    public async Task FailureWithoutRetry_HasNoAttemptSummaryAsync()
    {
        var test = TestCaseDescriptorBuilder
            .For<SampleTestClass>("retry.output.absent")
            .WithMethod(static (_, _) => throw new InvalidOperationException("boom"))
            .Build();

        var sink = new RecordingSink();
        await new TestExecutionEngine().RunAsync([test], sink, CancellationToken.None);

        // "after 1 of 1 attempts" on every ordinary failure would be noise.
        var report = Assert.Single(sink.Reports);
        Assert.False(
            (report.Output ?? "").Contains("[NextUnit] Test failed after", StringComparison.Ordinal),
            "A test without [Retry] must not carry an attempt summary.");
    }

    [Test]
    public async Task Output_ReportsOnlyTheFinalAttemptAsync()
    {
        var attempts = 0;
        var test = TestCaseDescriptorBuilder
            .For<SampleTestClass>("retry.output.per.attempt")
            .WithRetry(3, delayMs: 0)
            .WithMethod((_, _) =>
            {
                var attempt = Interlocked.Increment(ref attempts);
                TestContext.Current!.Output.WriteLine($"attempt {attempt}");
                throw new InvalidOperationException("boom");
            })
            .Build();

        var sink = new RecordingSink();
        await new TestExecutionEngine().RunAsync([test], sink, CancellationToken.None);

        // Each attempt gets a fresh output capture, so a retried test does not accumulate the noise of
        // the attempts that were discarded.
        var report = Assert.Single(sink.Reports);
        Assert.Contains("attempt 3", report.Output!);
        Assert.False(
            report.Output!.Contains("attempt 1", StringComparison.Ordinal),
            "Output from a discarded attempt reached the reported result.");
    }

    [Test]
    public async Task StateBag_DoesNotCarryAcrossAttemptsAsync()
    {
        var observed = new List<bool>();
        var test = TestCaseDescriptorBuilder
            .For<SampleTestClass>("retry.statebag")
            .WithRetry(3, delayMs: 0)
            .WithMethod((_, _) =>
            {
                var stateBag = TestContext.Current!.StateBag;
                observed.Add(stateBag.ContainsKey("seen"));
                stateBag["seen"] = true;
                throw new InvalidOperationException("boom");
            })
            .Build();

        var sink = new RecordingSink();
        await new TestExecutionEngine().RunAsync([test], sink, CancellationToken.None);

        // A retry is a fresh attempt with a fresh context, so the StateBag starts empty every time.
        // State that must survive a retry belongs in the test class or in class-scope state.
        Assert.Equal(3, observed.Count);
        Assert.All(observed, static seen => Assert.False(seen, "The StateBag carried a value into the next attempt."));
    }

    [Test]
    public async Task Artifacts_ReportOnlyTheFinalAttemptAsync()
    {
        var firstAttemptFile = Path.Combine(Path.GetTempPath(), $"nextunit-retry-{Guid.NewGuid():N}-1.txt");
        var lastAttemptFile = Path.Combine(Path.GetTempPath(), $"nextunit-retry-{Guid.NewGuid():N}-2.txt");
        await File.WriteAllTextAsync(firstAttemptFile, "first");
        await File.WriteAllTextAsync(lastAttemptFile, "last");

        try
        {
            var attempts = 0;
            var test = TestCaseDescriptorBuilder
                .For<SampleTestClass>("retry.artifacts")
                .WithRetry(2, delayMs: 0)
                .WithMethod((_, _) =>
                {
                    var attempt = Interlocked.Increment(ref attempts);
                    TestContext.Current!.AttachArtifact(attempt == 1 ? firstAttemptFile : lastAttemptFile);
                    throw new InvalidOperationException("boom");
                })
                .Build();

            var sink = new RecordingSink();
            await new TestExecutionEngine().RunAsync([test], sink, CancellationToken.None);

            // Artifacts live on the per-attempt context, so a discarded attempt's artifacts are
            // discarded with it rather than piling up on the reported result.
            var report = Assert.Single(sink.Reports);
            var artifact = Assert.Single(report.Artifacts!);
            Assert.Equal(Path.GetFullPath(lastAttemptFile), artifact.FilePath);
        }
        finally
        {
            File.Delete(firstAttemptFile);
            File.Delete(lastAttemptFile);
        }
    }

    [Test]
    public async Task EachAttempt_GetsItsOwnInstanceAndTestScopedHooksAsync()
    {
        AttemptScopedInstance.Reset();
        var beforeCalls = 0;
        var afterCalls = 0;

        var test = TestCaseDescriptorBuilder
            .For<AttemptScopedInstance>("retry.instance.per.attempt")
            .WithRetry(3, delayMs: 0)
            .WithLifecycle(new LifecycleInfo
            {
                BeforeTestMethods =
                [
                    (_, _) =>
                    {
                        Interlocked.Increment(ref beforeCalls);
                        return Task.CompletedTask;
                    }
                ],
                AfterTestMethods =
                [
                    (_, _) =>
                    {
                        Interlocked.Increment(ref afterCalls);
                        return Task.CompletedTask;
                    }
                ]
            })
            .WithMethod(static (_, _) => throw new InvalidOperationException("boom"))
            .Build();

        var sink = new RecordingSink();
        await new TestExecutionEngine().RunAsync([test], sink, CancellationToken.None);

        // Every attempt is a full test execution: a new instance, its own setup, and its own disposal.
        Assert.Equal(3, AttemptScopedInstance.Created);
        Assert.Equal(3, AttemptScopedInstance.Disposed);
        Assert.Equal(3, beforeCalls);

        // The failing attempt never reaches its [After] hooks, which is why disposal is the cleanup
        // that must run per attempt.
        Assert.Equal(0, afterCalls);
    }

    private static AggregateException AsAggregate(Exception exception)
    {
        return exception as AggregateException
            ?? throw new AssertionFailedException(
                $"Expected an AggregateException but got {exception.GetType().Name}: {exception.Message}");
    }
}

/// <summary>
/// Answers from a caller-supplied predicate and records what it was asked, flattened to values so an
/// assertion cannot accidentally read a context after the attempt that owns it has ended.
/// </summary>
internal sealed class RecordingRetryPolicy : IRetryPolicy
{
    private readonly Func<RetryContext, bool> _decide;

    public RecordingRetryPolicy(Func<RetryContext, bool> decide) => _decide = decide;

    public List<RecordedDecision> Decisions { get; } = [];

    public ValueTask<bool> ShouldRetryAsync(RetryContext context)
    {
        Decisions.Add(new RecordedDecision(
            context.Attempt,
            context.MaxAttempts,
            context.Exception.Message,
            context.TestContext.FullyQualifiedName,
            context.TestContext.RetryAttempt));

        return ValueTask.FromResult(_decide(context));
    }

    internal sealed record RecordedDecision(
        int Attempt,
        int MaxAttempts,
        string ExceptionMessage,
        string FullyQualifiedName,
        int ContextRetryAttempt);
}

/// <summary>
/// Runs a caller-supplied decision, including one that throws.
/// </summary>
internal sealed class DelegatingRetryPolicy : IRetryPolicy
{
    private readonly Func<RetryContext, bool> _decide;

    public DelegatingRetryPolicy(Func<RetryContext, bool> decide) => _decide = decide;

    public ValueTask<bool> ShouldRetryAsync(RetryContext context) => ValueTask.FromResult(_decide(context));
}

/// <summary>
/// Fails the decision, standing in for a policy whose own dependency is broken.
/// </summary>
internal sealed class ThrowingRetryPolicy : IRetryPolicy
{
    public ValueTask<bool> ShouldRetryAsync(RetryContext context) =>
        throw new InvalidOperationException("policy boom");
}

/// <summary>
/// Counts its own creation and disposal so a test can prove both happen once per attempt.
/// </summary>
internal sealed class AttemptScopedInstance : IDisposable
{
    private static int _created;
    private static int _disposed;

    public AttemptScopedInstance() => Interlocked.Increment(ref _created);

    public static int Created => Volatile.Read(ref _created);
    public static int Disposed => Volatile.Read(ref _disposed);

    public static void Reset()
    {
        Interlocked.Exchange(ref _created, 0);
        Interlocked.Exchange(ref _disposed, 0);
    }

    public void Dispose() => Interlocked.Increment(ref _disposed);
}
