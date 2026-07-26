using NextUnit.Internal;

namespace NextUnit.Platform.Tests;

/// <summary>
/// Pins the engine's reusability across runs. <see cref="NextUnitFramework"/> holds one
/// <see cref="TestExecutionEngine"/> in a readonly field and reuses it for every request the platform
/// issues, so a run must not leave the engine's own state disposed for the next one.
/// </summary>
public sealed class TestExecutionEngineReuseTests
{
    [Test]
    public async Task RunAsync_CanRunTwiceOnTheSameEngineAsync()
    {
        var invocations = 0;
        var engine = new TestExecutionEngine();

        TestCaseDescriptor BuildTest(string id) => TestCaseDescriptorBuilder
            .For<SampleTestClass>(id)
            .WithMethod((_, _) =>
            {
                Interlocked.Increment(ref invocations);
                return Task.CompletedTask;
            })
            .Build();

        var firstSink = new RecordingSink();
        await engine.RunAsync([BuildTest("engine.reuse.first")], firstSink, CancellationToken.None);

        // The second run previously threw ObjectDisposedException: cleanup after the first run
        // disposed the engine-scoped assembly setup lock that this run has to wait on.
        var secondSink = new RecordingSink();
        await engine.RunAsync([BuildTest("engine.reuse.second")], secondSink, CancellationToken.None);

        Assert.Equal(2, invocations);
        Assert.Equal(1, firstSink.Passed.Count);
        Assert.Equal(1, secondSink.Passed.Count);
        Assert.Equal(0, firstSink.Errors.Count);
        Assert.Equal(0, secondSink.Errors.Count);
    }

    [Test]
    public async Task RunAsync_PairsGlobalAssemblySetupAndTeardownPerRunAsync()
    {
        var calls = new List<string>();
        var engine = new TestExecutionEngine();
        engine.SetGlobalAssemblyLifecycle(
            [(_, _) =>
            {
                lock (calls)
                {
                    calls.Add("setup");
                }

                return Task.CompletedTask;
            }],
            [(_, _) =>
            {
                lock (calls)
                {
                    calls.Add("teardown");
                }

                return Task.CompletedTask;
            }]);

        var test = TestCaseDescriptorBuilder.For<SampleTestClass>("engine.reuse.assembly").Build();

        await engine.RunAsync([test], new RecordingSink(), CancellationToken.None);
        await engine.RunAsync([test], new RecordingSink(), CancellationToken.None);

        // Teardown is unguarded and runs at the end of every run, so setup must not stay latched from
        // the first run: a reused engine would otherwise tear down assembly state it never set up.
        Assert.Equal("setup,teardown,setup,teardown", string.Join(",", calls));
    }

    [Test]
    public async Task RunAsync_ClearsAssemblySkipReasonBetweenRunsAsync()
    {
        var shouldSkip = true;
        var engine = new TestExecutionEngine();
        engine.SetGlobalAssemblyLifecycle(
            [(_, _) => shouldSkip
                ? throw new TestSkippedException("assembly not applicable")
                : Task.CompletedTask],
            []);

        var test = TestCaseDescriptorBuilder.For<SampleTestClass>("engine.reuse.skip").Build();

        var firstSink = new RecordingSink();
        await engine.RunAsync([test], firstSink, CancellationToken.None);
        Assert.Equal(1, firstSink.Skipped.Count);

        // The skip reason belongs to the run that produced it; leaving it latched would skip every
        // test of every later run on the same engine.
        shouldSkip = false;
        var secondSink = new RecordingSink();
        await engine.RunAsync([test], secondSink, CancellationToken.None);

        Assert.Equal(0, secondSink.Skipped.Count);
        Assert.Equal(1, secondSink.Passed.Count);
    }

    [Test]
    public async Task RunAsync_RejectsAnOverlappingRunOnTheSameEngineAsync()
    {
        var engine = new TestExecutionEngine();
        var firstTestStarted = new TaskCompletionSource();
        var releaseFirstTest = new TaskCompletionSource();

        var blockingTest = TestCaseDescriptorBuilder
            .For<SampleTestClass>("engine.reentrancy.blocking")
            .WithMethod(async (_, _) =>
            {
                firstTestStarted.TrySetResult();
                await releaseFirstTest.Task;
            })
            .Build();

        var firstRun = engine.RunAsync([blockingTest], new RecordingSink(), CancellationToken.None);
        await firstTestStarted.Task;

        // Overlapping runs share assembly-scope state, so the second run would skip setup and then tear
        // the assembly down a second time. It fails fast instead.
        var overlapping = await Assert.ThrowsAsync<InvalidOperationException>(
            () => engine.RunAsync(
                [TestCaseDescriptorBuilder.For<SampleTestClass>("engine.reentrancy.overlapping").Build()],
                new RecordingSink(),
                CancellationToken.None));
        Assert.Contains("already in progress", overlapping.Message);

        releaseFirstTest.SetResult();
        await firstRun;

        // The guard is released with the run, so the engine stays reusable sequentially.
        var afterSink = new RecordingSink();
        await engine.RunAsync(
            [TestCaseDescriptorBuilder.For<SampleTestClass>("engine.reentrancy.sequential").Build()],
            afterSink,
            CancellationToken.None);

        Assert.Equal(1, afterSink.Passed.Count);
    }

    [Test]
    public async Task RunAsync_ReleasesTheReentrancyGuardWhenTheTestCaseSourceThrowsAsync()
    {
        var engine = new TestExecutionEngine();

        static IEnumerable<TestCaseDescriptor> ThrowingSource()
        {
            throw new InvalidOperationException("test case source boom");
#pragma warning disable CS0162 // Unreachable code: the iterator needs a yield to be an iterator.
            yield break;
#pragma warning restore CS0162
        }

        // The claim is taken before the enumeration, so a throwing source must not strand it.
        var sourceFailure = await Assert.ThrowsAsync<InvalidOperationException>(
            () => engine.RunAsync(ThrowingSource(), new RecordingSink(), CancellationToken.None));
        Assert.Equal("test case source boom", sourceFailure.Message);

        var sink = new RecordingSink();
        await engine.RunAsync(
            [TestCaseDescriptorBuilder.For<SampleTestClass>("engine.reentrancy.after-throw").Build()],
            sink,
            CancellationToken.None);

        Assert.Equal(1, sink.Passed.Count);
    }
}
