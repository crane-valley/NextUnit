using NextUnit.Internal;

namespace NextUnit.Platform.Tests;

/// <summary>
/// Pins the session-scope hook behavior that <see cref="NextUnitFramework"/> exposes through its
/// session results: a hook exception is caught, classified, and reported rather than escaping the
/// platform callback that invoked it.
/// </summary>
public sealed class SessionLifecycleRunnerTests
{
    [Test]
    public async Task RunSetupOnceAsync_SucceedsWhenNoHookThrowsAsync()
    {
        var runner = new SessionLifecycleRunner();
        var calls = 0;
        runner.AddMethods([(_, _) => { calls++; return Task.CompletedTask; }], null);

        var error = await runner.RunSetupOnceAsync(CancellationToken.None);

        Assert.Null(error);
        Assert.Null(runner.SkipReason);
        Assert.Equal(1, calls);
    }

    [Test]
    public async Task RunSetupOnceAsync_RecordsSkipReasonAndKeepsSessionSuccessfulAsync()
    {
        var runner = new SessionLifecycleRunner();
        runner.AddMethods([(_, _) => throw new TestSkippedException("no database available")], null);

        var error = await runner.RunSetupOnceAsync(CancellationToken.None);

        // A skip is a decision, not a failure: the session still starts, and every test reports skipped.
        Assert.Null(error);
        Assert.Equal("no database available", runner.SkipReason);
    }

    [Test]
    public async Task RunSetupOnceAsync_DoesNotRerunHooksAfterASkipAsync()
    {
        var runner = new SessionLifecycleRunner();
        var calls = 0;
        runner.AddMethods(
            [(_, _) =>
            {
                calls++;
                throw new TestSkippedException("no database available");
            }],
            null);

        await runner.RunSetupOnceAsync(CancellationToken.None);
        await runner.RunSetupOnceAsync(CancellationToken.None);

        // The skip reason is already recorded, so retrying would run the user's hooks a second time.
        Assert.Equal(1, calls);
    }

    [Test]
    public async Task RunSetupOnceAsync_ReportsAFailingHookInsteadOfThrowingAsync()
    {
        var runner = new SessionLifecycleRunner();
        runner.AddMethods([(_, _) => throw new InvalidOperationException("session setup boom")], null);

        var error = await runner.RunSetupOnceAsync(CancellationToken.None);

        Assert.NotNull(error);
        Assert.Contains("session setup boom", error!);
        Assert.Null(runner.SkipReason);
    }

    [Test]
    public async Task RunSetupOnceAsync_StopsAtTheFirstFailingHookAsync()
    {
        var runner = new SessionLifecycleRunner();
        var secondCalled = false;
        runner.AddMethods(
            [
                (_, _) => throw new InvalidOperationException("session setup boom"),
                (_, _) => { secondCalled = true; return Task.CompletedTask; }
            ],
            null);

        var error = await runner.RunSetupOnceAsync(CancellationToken.None);

        // Setup is ordered: a hook whose precondition failed must not have later hooks run on top of it.
        Assert.NotNull(error);
        Assert.False(secondCalled);
    }

    [Test]
    public async Task RunSetupOnceAsync_RetriesAfterAFailingHookAsync()
    {
        var runner = new SessionLifecycleRunner();
        var calls = 0;
        runner.AddMethods(
            [(_, _) =>
            {
                calls++;
                return calls == 1
                    ? throw new InvalidOperationException("session setup boom")
                    : Task.CompletedTask;
            }],
            null);

        // The gate must stay open on failure: reporting the failure does not mean setup ran.
        Assert.NotNull(await runner.RunSetupOnceAsync(CancellationToken.None));
        Assert.Null(await runner.RunSetupOnceAsync(CancellationToken.None));
        Assert.Equal(2, calls);
    }

    [Test]
    public async Task RunSetupOnceAsync_ReportsAHookOwnCancellationAsAFailureAsync()
    {
        var runner = new SessionLifecycleRunner();
        runner.AddMethods([(_, _) => throw new OperationCanceledException("hook gave up")], null);

        using var cts = new CancellationTokenSource();
        var error = await runner.RunSetupOnceAsync(cts.Token);

        // The run token was never cancelled, so this OCE is the hook's own and must not pass for run
        // cancellation, which adapters swallow.
        Assert.NotNull(error);
        Assert.Contains("does not represent run cancellation", error!);
    }

    [Test]
    public async Task RunSetupOnceAsync_LetsGenuineRunCancellationPropagateAsync()
    {
        var runner = new SessionLifecycleRunner();
        using var cts = new CancellationTokenSource();
        runner.AddMethods(
            [(_, ct) =>
            {
                cts.Cancel();
                ct.ThrowIfCancellationRequested();
                return Task.CompletedTask;
            }],
            null);

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => runner.RunSetupOnceAsync(cts.Token));
    }

    [Test]
    public async Task RunTeardownAsync_RunsHooksInReverseOrderAsync()
    {
        var runner = new SessionLifecycleRunner();
        var calls = new List<string>();
        runner.AddMethods(
            null,
            [
                (_, _) => { calls.Add("first"); return Task.CompletedTask; },
                (_, _) => { calls.Add("second"); return Task.CompletedTask; }
            ]);

        var error = await runner.RunTeardownAsync(CancellationToken.None);

        Assert.Null(error);
        Assert.Equal("second,first", string.Join(",", calls));
    }

    [Test]
    public async Task RunTeardownAsync_RunsRemainingHooksAfterOneFailsAsync()
    {
        var runner = new SessionLifecycleRunner();
        var calls = new List<string>();
        runner.AddMethods(
            null,
            [
                (_, _) => { calls.Add("first"); return Task.CompletedTask; },
                (_, _) => throw new InvalidOperationException("teardown boom")
            ]);

        var error = await runner.RunTeardownAsync(CancellationToken.None);

        // Reverse order runs the throwing hook first; the remaining hook must still release its resources.
        Assert.Equal("first", string.Join(",", calls));
        Assert.NotNull(error);
        Assert.Contains("teardown boom", error!);
    }

    [Test]
    public async Task RunTeardownAsync_AggregatesEveryHookFailureAsync()
    {
        var runner = new SessionLifecycleRunner();
        runner.AddMethods(
            null,
            [
                (_, _) => throw new InvalidOperationException("first boom"),
                (_, _) => throw new InvalidOperationException("second boom")
            ]);

        var error = await runner.RunTeardownAsync(CancellationToken.None);

        Assert.NotNull(error);
        Assert.Contains("first boom", error!);
        Assert.Contains("second boom", error!);
        Assert.Contains("One or more session teardown methods failed.", error!);
    }

    [Test]
    public async Task RunTeardownAsync_ReportsAHookOwnCancellationAsAFailureAsync()
    {
        var runner = new SessionLifecycleRunner();
        runner.AddMethods(null, [(_, _) => throw new OperationCanceledException("hook gave up")]);

        using var cts = new CancellationTokenSource();
        var error = await runner.RunTeardownAsync(cts.Token);

        Assert.NotNull(error);
        Assert.Contains("does not represent run cancellation", error!);
    }

    [Test]
    public async Task RunTeardownAsync_RunsRemainingHooksThenPropagatesRunCancellationAsync()
    {
        var runner = new SessionLifecycleRunner();
        using var cts = new CancellationTokenSource();
        var laterHookRan = false;
        runner.AddMethods(
            null,
            [
                (_, _) => { laterHookRan = true; return Task.CompletedTask; },
                (_, ct) =>
                {
                    cts.Cancel();
                    ct.ThrowIfCancellationRequested();
                    return Task.CompletedTask;
                }
            ]);

        // Cancellation must not stop the remaining hooks from releasing their resources, but it must
        // not be dropped either: a cancelled close reporting as a clean session is the bug the engine's
        // assembly teardown already avoids by handing its cancellation back to the run.
        await Assert.ThrowsAsync<OperationCanceledException>(() => runner.RunTeardownAsync(cts.Token));
        Assert.True(laterHookRan);
    }

    [Test]
    public async Task RunTeardownAsync_PrefersReportingAFailureOverRunCancellationAsync()
    {
        var runner = new SessionLifecycleRunner();
        using var cts = new CancellationTokenSource();
        runner.AddMethods(
            null,
            [
                (_, _) => throw new InvalidOperationException("teardown boom"),
                (_, ct) =>
                {
                    cts.Cancel();
                    ct.ThrowIfCancellationRequested();
                    return Task.CompletedTask;
                }
            ]);

        var error = await runner.RunTeardownAsync(cts.Token);

        // Adapters swallow cancellation, so rethrowing it here would lose the failure it coexists with.
        Assert.NotNull(error);
        Assert.Contains("teardown boom", error!);
        Assert.Contains("The test run was cancelled", error!);
    }

    [Test]
    public async Task RunTeardownAsync_DisposesSharedInstancesAfterEveryHookAsync()
    {
        var calls = new List<string>();
        var runner = new SessionLifecycleRunner(() =>
        {
            calls.Add("dispose");
            return ValueTask.CompletedTask;
        });
        runner.AddMethods(
            null,
            [
                (_, _) => { calls.Add("first"); return Task.CompletedTask; },
                (_, _) => { calls.Add("second"); return Task.CompletedTask; }
            ]);

        var error = await runner.RunTeardownAsync(CancellationToken.None);

        // An [After(Session)] hook may still read what a session-shared data source is holding, so the
        // instances outlive every hook and are released only once the last one has run.
        Assert.Null(error);
        Assert.Equal("second,first,dispose", string.Join(",", calls));
    }

    [Test]
    public async Task RunTeardownAsync_DisposesSharedInstancesEvenAfterAHookFailsAsync()
    {
        var disposed = false;
        var runner = new SessionLifecycleRunner(() =>
        {
            disposed = true;
            return ValueTask.CompletedTask;
        });
        runner.AddMethods(null, [(_, _) => throw new InvalidOperationException("teardown boom")]);

        var error = await runner.RunTeardownAsync(CancellationToken.None);

        Assert.NotNull(error);
        Assert.True(disposed);
    }

    [Test]
    public async Task RunTeardownAsync_ReportsADisposalFailureAsync()
    {
        var runner = new SessionLifecycleRunner(
            () => throw new InvalidOperationException("dispose boom"));

        var error = await runner.RunTeardownAsync(CancellationToken.None);

        // Session close has no sink, so a data source that failed to release its resources is only
        // ever visible through the session result.
        Assert.NotNull(error);
        Assert.Contains("dispose boom", error!);
    }

    [Test]
    public async Task RunTeardownAsync_AggregatesAHookFailureWithADisposalFailureAsync()
    {
        var runner = new SessionLifecycleRunner(
            () => throw new InvalidOperationException("dispose boom"));
        runner.AddMethods(null, [(_, _) => throw new InvalidOperationException("teardown boom")]);

        var error = await runner.RunTeardownAsync(CancellationToken.None);

        Assert.NotNull(error);
        Assert.Contains("teardown boom", error!);
        Assert.Contains("dispose boom", error!);
        Assert.Contains("One or more session teardown methods failed.", error!);
    }

    [Test]
    public async Task RunTeardownAsync_ReportsDisposalOwnCancellationAsAFailureAsync()
    {
        var runner = new SessionLifecycleRunner(
            () => throw new OperationCanceledException("disposer gave up"));

        using var cts = new CancellationTokenSource();
        var error = await runner.RunTeardownAsync(cts.Token);

        Assert.NotNull(error);
        Assert.Contains("does not represent run cancellation", error!);
    }

    [Test]
    public async Task TryReportSessionSkipAsync_ReportsNothingWithoutASkipReasonAsync()
    {
        var runner = new SessionLifecycleRunner();
        var sink = new RecordingSink();

        var skipped = await runner.TryReportSessionSkipAsync(
            [TestCaseDescriptorBuilder.For<SampleTestClass>("session.skip.none").Build()],
            sink,
            CancellationToken.None);

        Assert.False(skipped);
        Assert.Equal(0, sink.Skipped.Count);
    }

    [Test]
    public async Task TryReportSessionSkipAsync_ReportsEveryTestAsSkippedWithTheHookReasonAsync()
    {
        var runner = new SessionLifecycleRunner();
        runner.AddMethods([(_, _) => throw new TestSkippedException("no database available")], null);
        await runner.RunSetupOnceAsync(CancellationToken.None);

        var sink = new RecordingSink();
        var testCases = new[]
        {
            TestCaseDescriptorBuilder.For<SampleTestClass>("session.skip.first").Build(),
            TestCaseDescriptorBuilder.For<SecondSampleTestClass>("session.skip.second").Build()
        };

        var skipped = await runner.TryReportSessionSkipAsync(testCases, sink, CancellationToken.None);

        Assert.True(skipped);
        Assert.Equal(2, sink.Skipped.Count);
        Assert.True(sink.Skipped.All(static t => t.SkipReason == "no database available"));
    }

    [Test]
    public async Task TryReportSessionSkipAsync_ObservesCancellationRequestedByTheLastReportAsync()
    {
        var runner = new SessionLifecycleRunner();
        runner.AddMethods([(_, _) => throw new TestSkippedException("no database available")], null);
        await runner.RunSetupOnceAsync(CancellationToken.None);

        using var cts = new CancellationTokenSource();
        var sink = new CancellingSink(cts);

        // The final report cancels the token and then returns normally, so no loop iteration is left to
        // see it. Returning true here would send the caller home without ever reaching the engine, which
        // is the only other place a run would notice.
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => runner.TryReportSessionSkipAsync(
                [TestCaseDescriptorBuilder.For<SampleTestClass>("session.skip.cancelled").Build()],
                sink,
                cts.Token));
    }

    /// <summary>
    /// Cancels the run while reporting, then returns normally, standing in for a sink whose consumer
    /// stops the run mid-report.
    /// </summary>
    private sealed class CancellingSink : ITestExecutionSink
    {
        private readonly CancellationTokenSource _cts;

        public CancellingSink(CancellationTokenSource cts) => _cts = cts;

        public Task ReportPassedAsync(TestCaseDescriptor test, string? output = null, IReadOnlyList<Artifact>? artifacts = null) => Task.CompletedTask;

        public Task ReportFailedAsync(TestCaseDescriptor test, AssertionFailedException ex, string? output = null, IReadOnlyList<Artifact>? artifacts = null) => Task.CompletedTask;

        public Task ReportErrorAsync(TestCaseDescriptor test, Exception ex, string? output = null, IReadOnlyList<Artifact>? artifacts = null) => Task.CompletedTask;

        public Task ReportSkippedAsync(TestCaseDescriptor test, IReadOnlyList<Artifact>? artifacts = null)
        {
            _cts.Cancel();
            return Task.CompletedTask;
        }
    }
}
