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
    public async Task RunTeardownAsync_TreatsGenuineRunCancellationAsANormalOutcomeAsync()
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

        var error = await runner.RunTeardownAsync(cts.Token);

        // A cancelled run is not a broken session, and cancellation must not stop the remaining hooks
        // from releasing their resources.
        Assert.Null(error);
        Assert.True(laterHookRan);
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
}
