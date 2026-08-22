using Microsoft.Testing.Platform.TestHost;
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

    /// <summary>
    /// Enters session setup the way <see cref="NextUnitFramework"/> does for a run that selected at
    /// least one test, which is what teardown pairs its hooks against.
    /// </summary>
    /// <remarks>
    /// Every teardown test that expects its <c>[After(Session)]</c> hooks to run has to open the
    /// session first: without it the runner is in the empty-selection state, where the hooks are
    /// skipped on purpose.
    /// </remarks>
    private static Task OpenSessionAsync(SessionLifecycleRunner runner) =>
        runner.RunSetupOnceAsync(CancellationToken.None);

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
        await OpenSessionAsync(runner);

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
        await OpenSessionAsync(runner);

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
        await OpenSessionAsync(runner);

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
        await OpenSessionAsync(runner);

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
        await OpenSessionAsync(runner);

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
        await OpenSessionAsync(runner);

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
        await OpenSessionAsync(runner);

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
        await OpenSessionAsync(runner);

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
        await OpenSessionAsync(runner);

        var error = await runner.RunTeardownAsync(CancellationToken.None);

        Assert.NotNull(error);
        Assert.Contains("teardown boom", error!);
        Assert.Contains("dispose boom", error!);
        Assert.Contains("One or more session teardown methods failed.", error!);
    }

    [Test]
    public async Task RunTeardownAsync_LetsACriticalDisposalFailureEscapeAsync()
    {
        var runner = new SessionLifecycleRunner(
            () => throw new AggregateException("cleanup failed", new OutOfMemoryException()));

        // The store reports several disposal failures as one aggregate, so classifying only the outer
        // exception would turn running out of memory into a teardown message the session reports and
        // moves past.
        var exception = await Assert.ThrowsAsync<AggregateException>(
            () => runner.RunTeardownAsync(CancellationToken.None));

        Assert.True(exception.InnerExceptions.Any(inner => inner is OutOfMemoryException));
    }

    [Test]
    public async Task RunTeardownAsync_LetsACriticalFailureEscapeThroughACancellationWrapperAsync()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var laterHookRan = false;
        var disposed = false;
        var runner = new SessionLifecycleRunner(() =>
        {
            disposed = true;
            return ValueTask.CompletedTask;
        });
        runner.AddMethods(
            null,
            [
                (_, _) => { laterHookRan = true; return Task.CompletedTask; },
                (_, _) => throw new OperationCanceledException("cancelled", new OutOfMemoryException(), cts.Token)
            ]);
        await OpenSessionAsync(runner);

        // Cancellation is classified before anything else, so an OCE carrying a critical exception
        // would be held as run cancellation and the failure inside it never looked at. Reverse order
        // runs the throwing hook first, so what separates escaping from being held is whether teardown
        // carried on afterwards: cancellation is held and the rest still runs, a critical failure is
        // not caught at all and nothing after it happens.
        var exception = await Assert.ThrowsAsync<OperationCanceledException>(
            () => runner.RunTeardownAsync(cts.Token));

        Assert.True(exception.InnerException is OutOfMemoryException);
        Assert.False(laterHookRan);
        Assert.False(disposed);
    }

    [Test]
    public async Task RunSetupOnceAsync_LetsACriticalFailureEscapeThroughASkipAsync()
    {
        var runner = new SessionLifecycleRunner();
        runner.AddMethods(
            [(_, _) => throw new TestSkippedException("no database available", new OutOfMemoryException())],
            null);

        // A skip is the most swallowing branch in setup, and TestSkippedException takes an inner
        // exception, so a hook could hand one a critical failure and have the session report every
        // test as skipped while the process is out of memory.
        var exception = await Assert.ThrowsAsync<TestSkippedException>(
            () => runner.RunSetupOnceAsync(CancellationToken.None));

        Assert.True(exception.InnerException is OutOfMemoryException);
        Assert.Null(runner.SkipReason);
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
    public async Task RunTeardownAsync_SkipsHooksWhenTheRunSelectedNoTestAsync()
    {
        var disposed = false;
        var runner = new SessionLifecycleRunner(() =>
        {
            disposed = true;
            return ValueTask.CompletedTask;
        });
        var teardownCalls = 0;
        runner.AddMethods(null, [(_, _) => { teardownCalls++; return Task.CompletedTask; }]);

        // Deliberately not opened: this is the state CreateTestSessionAsync leaves behind when the
        // filter matches nothing, and it used to run [After(Session)] against a session that no
        // [Before(Session)] had ever set up.
        var error = await runner.RunTeardownAsync(CancellationToken.None);

        Assert.Null(error);
        Assert.Equal(0, teardownCalls);

        // The pairing covers the hooks, not the shared instances: expansion runs before the row-level
        // filter, so a run that ends up selecting nothing can still have constructed one.
        Assert.True(disposed);
    }

    [Test]
    public async Task RunTeardownAsync_RunsHooksForASessionThatDeclaresNoSetupHookAsync()
    {
        var runner = new SessionLifecycleRunner(() => ValueTask.CompletedTask);
        var teardownCalls = 0;
        runner.AddMethods(null, [(_, _) => { teardownCalls++; return Task.CompletedTask; }]);
        await OpenSessionAsync(runner);

        var error = await runner.RunTeardownAsync(CancellationToken.None);

        // Entering the session is reaching the setup phase, not running a hook in it: a session that
        // declares only [After(Session)] hooks has no first [Before(Session)] to start, and its
        // teardown still has to run.
        Assert.Null(error);
        Assert.Equal(1, teardownCalls);
    }

    [Test]
    public async Task RunTeardownAsync_RunsEveryHookAfterSetupThrewPartwayAsync()
    {
        var runner = new SessionLifecycleRunner(() => ValueTask.CompletedTask);
        var setupCalls = new List<string>();
        var teardownCalls = new List<string>();
        runner.AddMethods(
            [
                (_, _) => { setupCalls.Add("first"); return Task.CompletedTask; },
                (_, _) => throw new InvalidOperationException("session setup boom"),
                (_, _) => { setupCalls.Add("third"); return Task.CompletedTask; }
            ],
            [
                (_, _) => { teardownCalls.Add("first"); return Task.CompletedTask; },
                (_, _) => { teardownCalls.Add("second"); return Task.CompletedTask; },
                (_, _) => { teardownCalls.Add("third"); return Task.CompletedTask; }
            ]);

        Assert.NotNull(await runner.RunSetupOnceAsync(CancellationToken.None));
        var error = await runner.RunTeardownAsync(CancellationToken.None);

        // Setup stopped at the second hook, and the whole session is still one level, so every
        // [After(Session)] hook unwinds in reverse declaration order. Cutting the after-list to a
        // shorter prefix would need a pairing the registry does not emit, and would risk skipping an
        // [After(Session)] whose [Before(Session)] did run.
        Assert.Equal("first", string.Join(",", setupCalls));
        Assert.Null(error);
        Assert.Equal("third,second,first", string.Join(",", teardownCalls));
    }

    // Console.Error is process-global, so this must not overlap anything else reading it.
    [Test]
    [NotInParallel]
    public async Task RunTeardownAsync_SaysOnceThatItSkippedTheHooksAsync()
    {
        var withHooks = new SessionLifecycleRunner(() => ValueTask.CompletedTask);
        withHooks.AddMethods(null, [(_, _) => Task.CompletedTask, (_, _) => Task.CompletedTask]);
        var withoutHooks = new SessionLifecycleRunner(() => ValueTask.CompletedTask);

        var captured = new StringWriter();
        var original = Console.Error;
        Console.SetError(captured);
        try
        {
            await withHooks.RunTeardownAsync(CancellationToken.None);
            await withoutHooks.RunTeardownAsync(CancellationToken.None);
        }
        finally
        {
            Console.SetError(original);
        }

        // Silence is the outcome this must not have: a suite whose [After(Session)] hooks stopped
        // running would otherwise look exactly like one whose hooks succeeded. Exactly one line, so a
        // session that declares no [After(Session)] hook contributes nothing to say.
        var lines = captured.ToString()
            .Split('\n')
            .Where(static line => line.Contains("[After(LifecycleScope.Session)]"))
            .ToList();

        Assert.Equal(1, lines.Count);
        Assert.Contains("Skipped 2", lines[0]);
    }

    // Constructing the framework reads filter environment variables; see FilterEnvironmentConstraint.
    [Test]
    [NotInParallel(FilterEnvironmentConstraint.Key)]
    public async Task CreateTestSession_RefusesASecondSessionThatMatchesNoTestAsync()
    {
        // A filter matching nothing is what makes the placement of the check observable: with no test
        // case left, the session is created without ever reaching session setup, so a check that lived
        // only in setup would let this second session open and fail at its close instead.
        using var filter = EnvironmentVariableGuard.Set("NEXTUNIT_TEST_NAME", "nextunit.no.such.test");
        using var framework = new NextUnitFramework(null!, new NullServiceProvider());

        // Asserted rather than assumed: if this filter ever stopped emptying the list, the test would
        // still pass while covering the ordinary path instead of the one under test.
        var messageBus = new RecordingMessageBus();
        await framework.DiscoverAsync(new SessionUid("session-reuse"), messageBus, CancellationToken.None);
        Assert.Equal(0, messageBus.TestNodeUpdates.Count);

        Assert.True((await framework.CreateTestSessionAsync(CancellationToken.None)).IsSuccess);
        Assert.True((await framework.CloseTestSessionAsync(CancellationToken.None)).IsSuccess);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => framework.CreateTestSessionAsync(CancellationToken.None));
    }

    [Test]
    public async Task ThrowIfSessionClosed_RefusesToOpenASecondSessionAsync()
    {
        var runner = new SessionLifecycleRunner(() => ValueTask.CompletedTask);

        runner.ThrowIfSessionClosed();
        await runner.RunTeardownAsync(CancellationToken.None);

        // A session whose filter matches no test never reaches session setup, so this is the only
        // check the second such session would pass through.
        Assert.Throws<InvalidOperationException>(() => runner.ThrowIfSessionClosed());
    }

    [Test]
    public async Task RunSetupOnceAsync_RefusesToStartASecondSessionAsync()
    {
        var runner = new SessionLifecycleRunner(() => ValueTask.CompletedTask);
        var setupCalls = 0;
        runner.AddMethods([(_, _) => { setupCalls++; return Task.CompletedTask; }], null);

        await runner.RunSetupOnceAsync(CancellationToken.None);
        await runner.RunTeardownAsync(CancellationToken.None);

        // The setup gate closed in the first session and is never reopened, so a second session would
        // otherwise report a successful setup that never ran.
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => runner.RunSetupOnceAsync(CancellationToken.None));
        Assert.Equal(1, setupCalls);
    }

    [Test]
    public async Task RunSetupOnceAsync_RefusesToStartASecondSessionAfterASkipAsync()
    {
        var runner = new SessionLifecycleRunner(() => ValueTask.CompletedTask);
        runner.AddMethods([(_, _) => throw new TestSkippedException("no database available")], null);

        await runner.RunSetupOnceAsync(CancellationToken.None);
        await runner.RunTeardownAsync(CancellationToken.None);

        // A skip reason outlives the session that recorded it, so a second session served by this
        // instance would report every one of its tests skipped for the first session's reason.
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => runner.RunSetupOnceAsync(CancellationToken.None));
    }

    [Test]
    public async Task RunTeardownAsync_RefusesToCloseAnAlreadyClosedSessionAsync()
    {
        var disposeCalls = 0;
        var runner = new SessionLifecycleRunner(() =>
        {
            disposeCalls++;
            return ValueTask.CompletedTask;
        });
        var teardownCalls = 0;
        runner.AddMethods(null, [(_, _) => { teardownCalls++; return Task.CompletedTask; }]);
        await OpenSessionAsync(runner);

        await runner.RunTeardownAsync(CancellationToken.None);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => runner.RunTeardownAsync(CancellationToken.None));
        Assert.Equal(1, teardownCalls);
        Assert.Equal(1, disposeCalls);
    }

    [Test]
    public async Task RunTeardownAsync_RefusesToCloseAgainAfterAFailedTeardownAsync()
    {
        var runner = new SessionLifecycleRunner(() => ValueTask.CompletedTask);
        runner.AddMethods(null, [(_, _) => throw new InvalidOperationException("teardown boom")]);
        await OpenSessionAsync(runner);

        // A hook that failed still ran, so the session is over either way and the claim is not released.
        Assert.NotNull(await runner.RunTeardownAsync(CancellationToken.None));

        // The hook throws the same exception type, so the message is what separates the refusal from a
        // second run of the hook.
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => runner.RunTeardownAsync(CancellationToken.None));
        Assert.Contains("already run", exception.Message);
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
