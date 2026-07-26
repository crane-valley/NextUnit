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
    public async Task RunAsync_RunsGlobalAssemblySetupOncePerEngineAsync()
    {
        var assemblySetups = 0;
        var engine = new TestExecutionEngine();
        engine.SetGlobalAssemblyLifecycle(
            [(_, _) =>
            {
                Interlocked.Increment(ref assemblySetups);
                return Task.CompletedTask;
            }],
            []);

        var test = TestCaseDescriptorBuilder.For<SampleTestClass>("engine.reuse.assembly").Build();

        await engine.RunAsync([test], new RecordingSink(), CancellationToken.None);
        await engine.RunAsync([test], new RecordingSink(), CancellationToken.None);

        // Characterization: the assembly-scope guard is never reset, so global assembly setup runs
        // once per engine, not once per run. Reaching the guard at all on the second run is the point:
        // it is read under the assembly setup lock this change stopped disposing.
        Assert.Equal(1, assemblySetups);
    }
}
