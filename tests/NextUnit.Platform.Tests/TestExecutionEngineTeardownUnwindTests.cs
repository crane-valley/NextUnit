using NextUnit.Internal;

namespace NextUnit.Platform.Tests;

/// <summary>
/// Pins which <c>[After]</c> hooks run when a test, a <c>[Before]</c> hook, or a class setup fails,
/// and what the engine reports when a teardown hook itself fails.
/// </summary>
/// <remarks>
/// A "level" is one class in the test class's base chain. The hook lists are flat, so a level is a
/// run of entries in them, and <see cref="LifecycleLevel"/> says how long each run is: before-hooks
/// base to derived, after-hooks derived to base. These tests build those lists by hand rather than
/// through the generator, because what is under test is the engine's unwind, not the emission.
/// </remarks>
public sealed class TestExecutionEngineTeardownUnwindTests
{
    [Test]
    public async Task BeforeFailure_UnwindsOnlyTheLevelsThatWereEnteredAsync()
    {
        var calls = new HookRecorder();
        var lifecycle = ThreeLevelLifecycle(
            calls,
            failingBefore: "Mid.Before");

        var test = TestCaseDescriptorBuilder
            .For<SampleTestClass>("unwind.before.failure")
            .WithLifecycle(lifecycle)
            .Build();

        var sink = new RecordingSink();
        await new TestExecutionEngine().RunAsync([test], sink, CancellationToken.None);

        // Base and Mid were entered, so both unwind, derived first. Derived was never reached, so its
        // [After] hook has nothing to tear down and must not run.
        Assert.Equal(
            ["Base.Before", "Mid.Before", "Mid.After", "Base.After"],
            calls.Names);
        Assert.Empty(sink.Passed);
        Assert.Single(sink.Errors);
    }

    [Test]
    public async Task TestBodyFailure_UnwindsEveryLevelDerivedToBaseAsync()
    {
        var calls = new HookRecorder();
        var test = TestCaseDescriptorBuilder
            .For<SampleTestClass>("unwind.body.failure")
            .WithLifecycle(ThreeLevelLifecycle(calls))
            .WithMethod(static (_, _) => throw new InvalidOperationException("body boom"))
            .Build();

        var sink = new RecordingSink();
        await new TestExecutionEngine().RunAsync([test], sink, CancellationToken.None);

        Assert.Equal(
            ["Base.Before", "Mid.Before", "Derived.Before", "Derived.After", "Mid.After", "Base.After"],
            calls.Names);

        var error = Assert.Single(sink.Errors);
        Assert.Contains("body boom", error.Exception.Message);
    }

    [Test]
    public async Task PassingTest_StillUnwindsEveryLevelAsync()
    {
        var calls = new HookRecorder();
        var test = TestCaseDescriptorBuilder
            .For<SampleTestClass>("unwind.pass")
            .WithLifecycle(ThreeLevelLifecycle(calls))
            .Build();

        var sink = new RecordingSink();
        await new TestExecutionEngine().RunAsync([test], sink, CancellationToken.None);

        Assert.Equal(
            ["Base.Before", "Mid.Before", "Derived.Before", "Derived.After", "Mid.After", "Base.After"],
            calls.Names);
        Assert.Single(sink.Passed);
    }

    [Test]
    public async Task FailingAfterHook_DoesNotSkipTheRemainingHooksAsync()
    {
        var calls = new HookRecorder();
        var lifecycle = ThreeLevelLifecycle(calls, failingAfter: "Derived.After");

        var test = TestCaseDescriptorBuilder
            .For<SampleTestClass>("unwind.after.failure")
            .WithLifecycle(lifecycle)
            .WithMethod(static (_, _) => throw new InvalidOperationException("body boom"))
            .Build();

        var sink = new RecordingSink();
        await new TestExecutionEngine().RunAsync([test], sink, CancellationToken.None);

        Assert.Equal(
            ["Base.Before", "Mid.Before", "Derived.Before", "Derived.After", "Mid.After", "Base.After"],
            calls.Names);

        // The body's failure stays first so the teardown failure cannot mask what actually broke.
        var error = Assert.Single(sink.Errors);
        var aggregate = AsAggregate(error.Exception);
        Assert.Contains("body boom", aggregate.InnerExceptions[0].Message);
        Assert.Contains("Derived.After", aggregate.InnerExceptions[1].Message);
    }

    [Test]
    public async Task SeveralTeardownFailures_AreReportedInUnwindOrderAsync()
    {
        var calls = new HookRecorder();
        var lifecycle = ThreeLevelLifecycle(calls, failingAfter: "Derived.After");
        lifecycle = new LifecycleInfo
        {
            BeforeTestMethods = lifecycle.BeforeTestMethods,
            AfterTestMethods =
            [
                calls.Hook("Derived.After", shouldThrow: true),
                calls.Hook("Mid.After", shouldThrow: true),
                calls.Hook("Base.After")
            ],
            TestLevels = lifecycle.TestLevels
        };

        var test = TestCaseDescriptorBuilder
            .For<SampleTestClass>("unwind.after.failures")
            .WithLifecycle(lifecycle)
            .Build();

        var sink = new RecordingSink();
        await new TestExecutionEngine().RunAsync([test], sink, CancellationToken.None);

        var error = Assert.Single(sink.Errors);
        var aggregate = AsAggregate(error.Exception);
        Assert.Equal(2, aggregate.InnerExceptions.Count);
        Assert.Contains("Derived.After", aggregate.InnerExceptions[0].Message);
        Assert.Contains("Mid.After", aggregate.InnerExceptions[1].Message);
    }

    [Test]
    public async Task TeardownFailureOnAPassingTest_FailsItAndIsNotRetriedAsync()
    {
        var bodyInvocations = 0;
        var test = TestCaseDescriptorBuilder
            .For<SampleTestClass>("unwind.teardown.no.retry")
            .WithRetry(3)
            .WithLifecycle(new LifecycleInfo
            {
                AfterTestMethods = [static (_, _) => throw new InvalidOperationException("teardown boom")]
            })
            .WithMethod((_, _) =>
            {
                Interlocked.Increment(ref bodyInvocations);
                return Task.CompletedTask;
            })
            .Build();

        var sink = new RecordingSink();
        await new TestExecutionEngine().RunAsync([test], sink, CancellationToken.None);

        // Terminal like a failing disposer: retrying a test whose teardown threw would re-run a body
        // that never failed, and a later passing attempt would discard the failure already reported.
        Assert.Equal(1, bodyInvocations);
        Assert.Empty(sink.Passed);
        var error = Assert.Single(sink.Errors);
        Assert.Contains("teardown boom", error.Exception.Message);
    }

    [Test]
    public async Task RuntimeSkipWithTeardownFailure_IsReportedAsAnErrorAsync()
    {
        var test = TestCaseDescriptorBuilder
            .For<SampleTestClass>("unwind.skip.teardown")
            .WithRetry(3)
            .WithLifecycle(new LifecycleInfo
            {
                AfterTestMethods = [static (_, _) => throw new InvalidOperationException("teardown boom")]
            })
            .WithMethod(static (_, _) => throw new TestSkippedException("not today"))
            .Build();

        var sink = new RecordingSink();
        await new TestExecutionEngine().RunAsync([test], sink, CancellationToken.None);

        // A skipped test whose teardown failed is not merely skipped: the skip report carries only a
        // reason, so the teardown failure would have nowhere to go.
        Assert.Empty(sink.Skipped);
        var error = Assert.Single(sink.Errors);
        var aggregate = AsAggregate(error.Exception);
        Assert.True(aggregate.InnerExceptions[0] is TestSkippedException);
        Assert.Contains("teardown boom", aggregate.InnerExceptions[1].Message);
    }

    [Test]
    public async Task TimedOutTest_UnwindsWithALiveTokenAsync()
    {
        var afterRan = false;
        var hookTokenCancelled = true;
        var contextTokenCancelled = false;

        var test = TestCaseDescriptorBuilder
            .For<SampleTestClass>("unwind.timeout")
            .WithTimeout(50)
            .WithLifecycle(new LifecycleInfo
            {
                AfterTestMethods =
                [
                    (_, token) =>
                    {
                        afterRan = true;
                        hookTokenCancelled = token.IsCancellationRequested;
                        contextTokenCancelled = NextUnit.Core.TestContext.Current!.CancellationToken.IsCancellationRequested;
                        return Task.CompletedTask;
                    }
                ]
            })
            .WithMethod(static async (_, token) => await Task.Delay(TimeSpan.FromSeconds(30), token))
            .Build();

        var sink = new RecordingSink();
        await new TestExecutionEngine().RunAsync([test], sink, CancellationToken.None);

        // The hook is passed the run token, so a timeout does not make teardown a no-op. The context
        // still describes the attempt, whose linked token the timeout did cancel; the two disagreeing
        // is the deliberate consequence of bounding the body but not the cleanup of it.
        Assert.True(afterRan);
        Assert.False(hookTokenCancelled);
        Assert.True(contextTokenCancelled);

        var error = Assert.Single(sink.Errors);
        Assert.True(error.Exception is TestTimeoutException);
    }

    [Test]
    public async Task AfterHookObservingRunCancellation_PublishesNoOutcomeAsync()
    {
        using var cts = new CancellationTokenSource();
        var test = TestCaseDescriptorBuilder
            .For<SampleTestClass>("unwind.cancelled")
            .Serial()
            .WithLifecycle(new LifecycleInfo
            {
                AfterTestMethods =
                [
                    (_, token) =>
                    {
                        cts.Cancel();
                        token.ThrowIfCancellationRequested();
                        return Task.CompletedTask;
                    }
                ]
            })
            .Build();

        var sink = new RecordingSink();
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => new TestExecutionEngine().RunAsync([test], sink, cts.Token));

        // The run is being abandoned, so nothing about this test was established: publishing it as
        // passed would leave a result behind that the cancelled teardown never finished backing up.
        Assert.Empty(sink.Passed);
        Assert.Empty(sink.Errors);
    }

    [Test]
    public async Task AfterHookThatCancelsAndReturnsNormally_SurfacesCancellationAsync()
    {
        using var cts = new CancellationTokenSource();
        var test = TestCaseDescriptorBuilder
            .For<SampleTestClass>("unwind.cancelled.silently")
            .Serial()
            .WithLifecycle(new LifecycleInfo
            {
                AfterTestMethods =
                [
                    (_, _) =>
                    {
                        cts.Cancel();
                        return Task.CompletedTask;
                    }
                ]
            })
            .Build();

        var sink = new RecordingSink();
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => new TestExecutionEngine().RunAsync([test], sink, cts.Token));

        Assert.Empty(sink.Passed);
    }

    [Test]
    public async Task TeardownFailureCoexistingWithCancellation_IsStillReportedAsync()
    {
        using var cts = new CancellationTokenSource();
        var test = TestCaseDescriptorBuilder
            .For<SampleTestClass>("unwind.cancelled.and.failed")
            .Serial()
            .WithLifecycle(new LifecycleInfo
            {
                AfterTestMethods =
                [
                    (_, token) =>
                    {
                        cts.Cancel();
                        token.ThrowIfCancellationRequested();
                        return Task.CompletedTask;
                    },
                    static (_, _) => throw new InvalidOperationException("teardown boom")
                ]
            })
            .Build();

        var sink = new RecordingSink();
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => new TestExecutionEngine().RunAsync([test], sink, cts.Token));

        // Reported before the cancellation is rethrown, so neither is lost.
        var error = Assert.Single(sink.Errors);
        Assert.Contains("teardown boom", error.Exception.Message);
    }

    [Test]
    public async Task DescriptorWithoutLevels_UnwindsEveryHookAsync()
    {
        var calls = new HookRecorder();
        var test = TestCaseDescriptorBuilder
            .For<SampleTestClass>("unwind.legacy.shape")
            .WithLifecycle(new LifecycleInfo
            {
                BeforeTestMethods = [calls.Hook("First.Before"), calls.Hook("Second.Before", shouldThrow: true)],
                AfterTestMethods = [calls.Hook("First.After"), calls.Hook("Second.After")]
            })
            .Build();

        var sink = new RecordingSink();
        await new TestExecutionEngine().RunAsync([test], sink, CancellationToken.None);

        // No levels means one level holding everything, which is what a descriptor written against the
        // pre-3.0.0 shape means and what a test class with no annotated base class is. Entering that
        // level unwinds all of it -- the two-hooks-in-one-class case the fix exists for.
        Assert.Equal(
            ["First.Before", "Second.Before", "First.After", "Second.After"],
            calls.Names);
    }

    [Test]
    public async Task LevelWithNoBeforeHooks_IsStillEnteredAndUnwoundAsync()
    {
        var calls = new HookRecorder();
        var test = TestCaseDescriptorBuilder
            .For<SampleTestClass>("unwind.after.only.level")
            .WithLifecycle(new LifecycleInfo
            {
                BeforeTestMethods = [calls.Hook("Derived.Before")],
                AfterTestMethods = [calls.Hook("Derived.After"), calls.Hook("Base.After")],
                TestLevels =
                [
                    new LifecycleLevel { BeforeCount = 0, AfterCount = 1 },
                    new LifecycleLevel { BeforeCount = 1, AfterCount = 1 }
                ]
            })
            .Build();

        var sink = new RecordingSink();
        await new TestExecutionEngine().RunAsync([test], sink, CancellationToken.None);

        // A level is entered when the walk reaches it, so a base class that declares only [After]
        // hooks is entered too -- it has no first [Before] to start.
        Assert.Equal(["Derived.Before", "Derived.After", "Base.After"], calls.Names);
    }

    [Test]
    public async Task MalformedLevelCounts_StillUnwindTheEnteredLevelsAsync()
    {
        var calls = new HookRecorder();
        var test = TestCaseDescriptorBuilder
            .For<SampleTestClass>("unwind.malformed.counts")
            .WithLifecycle(new LifecycleInfo
            {
                BeforeTestMethods = [calls.Hook("Base.Before", shouldThrow: true)],
                AfterTestMethods = [calls.Hook("Derived.After"), calls.Hook("Base.After")],
                TestLevels =
                [
                    new LifecycleLevel { BeforeCount = 1, AfterCount = -3 },
                    new LifecycleLevel { BeforeCount = int.MaxValue, AfterCount = int.MaxValue }
                ]
            })
            .Build();

        var sink = new RecordingSink();
        await new TestExecutionEngine().RunAsync([test], sink, CancellationToken.None);

        // The unwind is measured backwards from the end by the ENTERED levels' counts, so a count on a
        // level that was never entered cannot move the cut and cannot skip an entered level's teardown.
        // Here the base level was entered with a negative count, which contributes nothing, so nothing
        // below it runs -- and no index ever leaves the array.
        Assert.Equal(["Base.Before"], calls.Names);
        Assert.Single(sink.Errors);
    }

    [Test]
    public async Task ClassSetupFailingPartway_UnwindsOnlyTheEnteredLevelsAsync()
    {
        var calls = new HookRecorder();
        var test = TestCaseDescriptorBuilder
            .For<SampleTestClass>("unwind.class.setup.failure")
            .WithLifecycle(new LifecycleInfo
            {
                BeforeClassMethods =
                [
                    calls.Hook("Base.BeforeClass"),
                    calls.Hook("Mid.BeforeClass", shouldThrow: true),
                    calls.Hook("Derived.BeforeClass")
                ],
                AfterClassMethods =
                [
                    calls.Hook("Derived.AfterClass"),
                    calls.Hook("Mid.AfterClass"),
                    calls.Hook("Base.AfterClass")
                ],
                ClassLevels =
                [
                    new LifecycleLevel { BeforeCount = 1, AfterCount = 1 },
                    new LifecycleLevel { BeforeCount = 1, AfterCount = 1 },
                    new LifecycleLevel { BeforeCount = 1, AfterCount = 1 }
                ]
            })
            .Build();

        var sink = new RecordingSink();
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => new TestExecutionEngine().RunAsync([test], sink, CancellationToken.None));

        // The class setup still aborts the run, but cleanup now unwinds only what it entered rather
        // than running a derived AfterClass against a fixture whose derived setup never ran.
        Assert.Equal(
            ["Base.BeforeClass", "Mid.BeforeClass", "Mid.AfterClass", "Base.AfterClass"],
            calls.Names);
    }

    [Test]
    public async Task ClassWithOnlyAfterClassHooks_RunsThemAllAsync()
    {
        var calls = new HookRecorder();
        var test = TestCaseDescriptorBuilder
            .For<SampleTestClass>("unwind.class.after.only")
            .WithLifecycle(new LifecycleInfo
            {
                AfterClassMethods = [calls.Hook("Derived.AfterClass"), calls.Hook("Base.AfterClass")],
                ClassLevels =
                [
                    new LifecycleLevel { BeforeCount = 0, AfterCount = 1 },
                    new LifecycleLevel { BeforeCount = 0, AfterCount = 1 }
                ]
            })
            .Build();

        var sink = new RecordingSink();
        await new TestExecutionEngine().RunAsync([test], sink, CancellationToken.None);

        Assert.Equal(["Derived.AfterClass", "Base.AfterClass"], calls.Names);
        Assert.Single(sink.Passed);
    }

    /// <summary>
    /// A three-level test-scoped lifecycle, one before-hook and one after-hook per level.
    /// </summary>
    private static LifecycleInfo ThreeLevelLifecycle(
        HookRecorder calls,
        string? failingBefore = null,
        string? failingAfter = null)
    {
        return new LifecycleInfo
        {
            BeforeTestMethods =
            [
                calls.Hook("Base.Before", failingBefore == "Base.Before"),
                calls.Hook("Mid.Before", failingBefore == "Mid.Before"),
                calls.Hook("Derived.Before", failingBefore == "Derived.Before")
            ],
            AfterTestMethods =
            [
                calls.Hook("Derived.After", failingAfter == "Derived.After"),
                calls.Hook("Mid.After", failingAfter == "Mid.After"),
                calls.Hook("Base.After", failingAfter == "Base.After")
            ],
            TestLevels =
            [
                new LifecycleLevel { BeforeCount = 1, AfterCount = 1 },
                new LifecycleLevel { BeforeCount = 1, AfterCount = 1 },
                new LifecycleLevel { BeforeCount = 1, AfterCount = 1 }
            ]
        };
    }

    private static AggregateException AsAggregate(Exception exception)
    {
        return exception as AggregateException
            ?? throw new AssertionFailedException(
                $"Expected an AggregateException but got {exception.GetType().Name}: {exception.Message}");
    }

    /// <summary>
    /// Records the hooks that ran, in order, and fails on demand.
    /// </summary>
    private sealed class HookRecorder
    {
        private readonly object _lock = new();
        private readonly List<string> _names = [];

        public IReadOnlyList<string> Names
        {
            get
            {
                lock (_lock)
                {
                    return _names.ToArray();
                }
            }
        }

        public LifecycleMethodDelegate Hook(string name, bool shouldThrow = false) => (_, _) =>
        {
            lock (_lock)
            {
                _names.Add(name);
            }

            return shouldThrow
                ? throw new InvalidOperationException($"{name} boom")
                : Task.CompletedTask;
        };
    }
}
