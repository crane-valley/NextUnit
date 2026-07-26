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
        var test = TestCaseDescriptorBuilder
            .ForReflectionActivation("timeout.per.attempt", typeof(TestExecutionEngineTests))
            .WithMethodName("PerAttempt")
            .WithTimeout(1000)
            .WithRetry(3)
            .WithMethod(async (_, ct) =>
            {
                var current = Interlocked.Increment(ref attempts);
                await Task.Delay(400, ct);
                if (current < 3)
                {
                    throw new InvalidOperationException("transient failure");
                }
            })
            .Build();

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
        var test = TestCaseDescriptorBuilder
            .ForReflectionActivation("cancel.no.retry", typeof(TestExecutionEngineTests))
            .WithMethodName("Cancel")
            .WithRetry(3)
            .WithMethod((_, ct) =>
            {
                Interlocked.Increment(ref attempts);
                cts.Cancel();
                ct.ThrowIfCancellationRequested();
                return Task.CompletedTask;
            })
            .Build();

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
        var test = TestCaseDescriptorBuilder
            .For<SampleTestClass>("teardown.reported")
            .WithAfterClass(static (_, _) => throw new InvalidOperationException("teardown boom"))
            .Build();

        var sink = new RecordingSink();
        await new TestExecutionEngine().RunAsync([test], sink, CancellationToken.None);

        // The passing test is not retroactively failed; the teardown error surfaces on a class-scope node.
        Assert.Single(sink.Passed);
        var error = Assert.Single(sink.Errors);
        Assert.Contains("teardown boom", error.Exception.Message);
        Assert.True(error.Test.Id.Value.EndsWith("[ClassTeardown]", StringComparison.Ordinal));
    }

    [Test]
    public async Task ClassTeardownAndDisposeFailures_ReportDistinctNodesAsync()
    {
        var test = TestCaseDescriptorBuilder
            .For<ThrowingDisposeClass>("teardown.and.dispose")
            .WithAfterClass(static (_, _) => throw new InvalidOperationException("teardown boom"))
            .Build();

        var sink = new RecordingSink();
        await new TestExecutionEngine().RunAsync([test], sink, CancellationToken.None);

        // Teardown and disposal failures must land on distinct node identities so they do not collide.
        var errorIds = sink.Errors.Select(static e => e.Test.Id.Value).ToList();
        Assert.Contains(errorIds, static id => id.EndsWith("[ClassTeardown]", StringComparison.Ordinal));
        Assert.Contains(errorIds, static id => id.EndsWith("[ClassDispose]", StringComparison.Ordinal));
    }

    [Test]
    public async Task ClassTeardownObservingCancellation_IsNotReportedAsErrorAsync()
    {
        using var cts = new CancellationTokenSource();
        var test = TestCaseDescriptorBuilder
            .For<SampleTestClass>("teardown.cancelled")
            // Serial execution keeps the parallel loop from observing the token, so the AfterClass
            // teardown is the only place cancellation is first seen (the case that was being lost).
            .Serial()
            .WithMethod((_, _) =>
            {
                cts.Cancel();
                return Task.CompletedTask;
            })
            .WithAfterClass(static (_, ct) =>
            {
                // Teardown observes the cancelled run token; this is not a teardown failure.
                ct.ThrowIfCancellationRequested();
                return Task.CompletedTask;
            })
            .Build();

        var sink = new RecordingSink();

        // Cancellation first seen in teardown must still propagate, not be silently swallowed.
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => new TestExecutionEngine().RunAsync([test], sink, cts.Token));

        Assert.Empty(sink.Errors);
    }

    [Test]
    public async Task ClassTeardownHooksRunToCompletionAfterCancellationAsync()
    {
        using var cts = new CancellationTokenSource();
        var secondHookRan = false;
        var test = TestCaseDescriptorBuilder
            .For<SampleTestClass>("teardown.multi.hook")
            .Serial()
            .WithMethod((_, _) =>
            {
                cts.Cancel();
                return Task.CompletedTask;
            })
            .WithAfterClass(
                static (_, ct) =>
                {
                    ct.ThrowIfCancellationRequested();
                    return Task.CompletedTask;
                },
                (_, _) =>
                {
                    secondHookRan = true;
                    return Task.CompletedTask;
                })
            .Build();

        var sink = new RecordingSink();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => new TestExecutionEngine().RunAsync([test], sink, cts.Token));

        // A hook observing cancellation must not skip the remaining AfterClass hooks.
        Assert.True(secondHookRan);
        Assert.Empty(sink.Errors);
    }

    [Test]
    public async Task AssemblyTeardownHooksRunToCompletionAfterCancellationAsync()
    {
        using var cts = new CancellationTokenSource();
        var secondHookRan = false;
        var engine = new TestExecutionEngine();
        engine.SetGlobalAssemblyLifecycle(
            beforeMethods: null,
            afterMethods:
            [
                static (_, ct) =>
                {
                    ct.ThrowIfCancellationRequested();
                    return Task.CompletedTask;
                },
                (_, _) =>
                {
                    secondHookRan = true;
                    return Task.CompletedTask;
                }
            ]);

        var test = TestCaseDescriptorBuilder
            .For<SampleTestClass>("assembly.teardown.multi")
            .Serial()
            .WithMethod((_, _) =>
            {
                cts.Cancel();
                return Task.CompletedTask;
            })
            .Build();

        var sink = new RecordingSink();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => engine.RunAsync([test], sink, cts.Token));

        // A hook observing cancellation must not skip the remaining assembly teardown hooks.
        Assert.True(secondHookRan);
    }

    [Test]
    public async Task SerialBatch_DoesNotStartTestsAfterCancellationAsync()
    {
        using var cts = new CancellationTokenSource();
        var executions = 0;

        TestCaseDescriptor MakeTest(string id, int priority) => TestCaseDescriptorBuilder
            .For<SampleTestClass>(id)
            .WithPriority(priority)
            .Serial()
            .WithMethod((_, _) =>
            {
                Interlocked.Increment(ref executions);
                // Ignore the token and return normally; the batch loop must still stop.
                cts.Cancel();
                return Task.CompletedTask;
            })
            .Build();

        var tests = new[] { MakeTest("serial.a", 10), MakeTest("serial.b", 0) };
        var sink = new RecordingSink();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => new TestExecutionEngine().RunAsync(tests, sink, cts.Token));

        // Exactly one test runs; the second must not start after the run is cancelled.
        Assert.Equal(1, executions);
    }

    [Test]
    public async Task Run_SurfacesCancellationWhenOnlyTestIgnoresTokenAsync()
    {
        using var cts = new CancellationTokenSource();
        var test = TestCaseDescriptorBuilder
            .For<SampleTestClass>("only.ignores.token")
            .Serial()
            .WithMethod((_, _) =>
            {
                // The only test ignores the token and completes normally.
                cts.Cancel();
                return Task.CompletedTask;
            })
            .Build();

        var sink = new RecordingSink();

        // With no further loop iteration to observe the token, the run must still surface cancellation.
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => new TestExecutionEngine().RunAsync([test], sink, cts.Token));

        Assert.Single(sink.Passed);
    }

    [Test]
    public async Task MultipleClassTeardownFailures_AggregateIntoSingleNodeAsync()
    {
        var test = TestCaseDescriptorBuilder
            .For<SampleTestClass>("teardown.multi.fail")
            .WithAfterClass(
                static (_, _) => throw new InvalidOperationException("first boom"),
                static (_, _) => throw new InvalidOperationException("second boom"))
            .Build();

        var sink = new RecordingSink();
        await new TestExecutionEngine().RunAsync([test], sink, CancellationToken.None);

        Assert.Single(sink.Passed);
        var error = Assert.Single(sink.Errors);
        Assert.True(error.Test.Id.Value.EndsWith("[ClassTeardown]", StringComparison.Ordinal));
        var aggregate = error.Exception as AggregateException;
        Assert.NotNull(aggregate);
        Assert.Equal(2, aggregate!.InnerExceptions.Count);
    }

    [Test]
    public async Task CancellationAndTeardownFailure_AreBothSurfacedAsync()
    {
        using var cts = new CancellationTokenSource();
        var engine = new TestExecutionEngine();
        engine.SetGlobalAssemblyLifecycle(
            beforeMethods: null,
            afterMethods: [static (_, _) => throw new InvalidOperationException("assembly boom")]);

        var test = TestCaseDescriptorBuilder
            .For<SampleTestClass>("cancel.plus.failure")
            .Serial()
            .WithMethod((_, _) =>
            {
                cts.Cancel();
                return Task.CompletedTask;
            })
            .WithAfterClass(static (_, ct) =>
            {
                ct.ThrowIfCancellationRequested();
                return Task.CompletedTask;
            })
            .Build();

        var sink = new RecordingSink();

        // Run cancellation and a normal teardown failure coexist; neither may be discarded. Cancellation
        // still propagates, while the assembly teardown failure now surfaces as a result node.
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => engine.RunAsync([test], sink, cts.Token));

        var error = Assert.Single(sink.Errors);
        Assert.True(error.Test.Id.Value.EndsWith("[AssemblyTeardown]", StringComparison.Ordinal));
        Assert.Contains("assembly boom", error.Exception.Message);
    }

    [Test]
    public async Task AssemblyTeardownFailure_IsReportedOnAssemblyScopeNodeAsync()
    {
        var engine = new TestExecutionEngine();
        engine.SetGlobalAssemblyLifecycle(
            beforeMethods: null,
            afterMethods: [static (_, _) => throw new InvalidOperationException("assembly boom")]);

        var test = TestCaseDescriptorBuilder
            .For<SampleTestClass>("assembly.teardown.node")
            .Build();

        var sink = new RecordingSink();

        // An assembly teardown failure is a result, not an exception the caller must catch: the run
        // completes and the failure reaches every adapter through the sink.
        await engine.RunAsync([test], sink, CancellationToken.None);

        Assert.Single(sink.Passed);
        var error = Assert.Single(sink.Errors);
        Assert.True(error.Test.Id.Value.EndsWith("[AssemblyTeardown]", StringComparison.Ordinal));
        Assert.Contains("assembly boom", error.Exception.Message);
    }

    [Test]
    public async Task MultipleAssemblyTeardownFailures_AggregateIntoSingleNodeAsync()
    {
        var engine = new TestExecutionEngine();
        engine.SetGlobalAssemblyLifecycle(
            beforeMethods: null,
            afterMethods:
            [
                static (_, _) => throw new InvalidOperationException("first assembly boom"),
                static (_, _) => throw new InvalidOperationException("second assembly boom")
            ]);

        var test = TestCaseDescriptorBuilder
            .For<SampleTestClass>("assembly.teardown.multi.fail")
            .Build();

        var sink = new RecordingSink();
        await engine.RunAsync([test], sink, CancellationToken.None);

        // Two failures must not collide on the same node identity.
        var error = Assert.Single(sink.Errors);
        Assert.True(error.Test.Id.Value.EndsWith("[AssemblyTeardown]", StringComparison.Ordinal));
        Assert.Equal(2, AsAggregate(error.Exception).InnerExceptions.Count);
    }

    [Test]
    public async Task AssemblyTeardownReportFailure_SurfacesBothErrorsAsync()
    {
        var engine = new TestExecutionEngine();
        engine.SetGlobalAssemblyLifecycle(
            beforeMethods: null,
            afterMethods: [static (_, _) => throw new InvalidOperationException("assembly boom")]);

        var test = TestCaseDescriptorBuilder
            .For<SampleTestClass>("assembly.teardown.sink.fail")
            .Build();

        var sink = new ThrowingReportSink();

        // A teardown failure whose report also fails must not be lost: the run fails with both errors.
        var error = await Assert.ThrowsAsync<AggregateException>(
            () => engine.RunAsync([test], sink, CancellationToken.None));

        var flat = error.Flatten();
        Assert.Contains(flat.InnerExceptions, static e => e.Message.Contains("assembly boom"));
        Assert.Contains(flat.InnerExceptions, static e => e.Message.Contains("sink is down"));
    }

    [Test]
    public async Task PassingTestWithThrowingDispose_IsReportedAsTestScopedErrorAsync()
    {
        var test = TestCaseDescriptorBuilder
            .For<ThrowingDisposeClass>("dispose.after.pass")
            .Build();

        var sink = new RecordingSink();

        // A throwing Dispose must not escape RunAsync; it fails the test that owns the instance.
        await new TestExecutionEngine().RunAsync([test], sink, CancellationToken.None);

        Assert.Empty(sink.Passed);
        var error = Assert.Single(sink.Errors);
        Assert.Equal("dispose.after.pass", error.Test.Id.Value);
        Assert.Contains("dispose boom", error.Exception.Message);
    }

    [Test]
    public async Task FailingTestWithThrowingDispose_KeepsOriginalFailureAsync()
    {
        var attempts = 0;
        var test = TestCaseDescriptorBuilder
            .For<ThrowingDisposeClass>("dispose.after.failure")
            .WithMethodName("Boom")
            .WithRetry(3, delayMs: 0)
            .WithMethod((_, _) =>
            {
                Interlocked.Increment(ref attempts);
                throw new InvalidOperationException("test boom");
            })
            .Build();

        var sink = new RecordingSink();
        await new TestExecutionEngine().RunAsync([test], sink, CancellationToken.None);

        // The disposal failure is terminal, so the test is not retried, and the original test failure
        // stays first so the disposal failure cannot mask it.
        Assert.Equal(1, attempts);
        var error = Assert.Single(sink.Errors);
        Assert.Equal("dispose.after.failure", error.Test.Id.Value);
        var aggregate = AsAggregate(error.Exception);
        Assert.Equal(2, aggregate.InnerExceptions.Count);
        Assert.Contains("test boom", aggregate.InnerExceptions[0].Message);
        Assert.Contains("dispose boom", aggregate.InnerExceptions[1].Message);
    }

    [Test]
    public async Task DisposalFailureReportFailure_SurfacesBothErrorsAsync()
    {
        var test = TestCaseDescriptorBuilder
            .For<ThrowingDisposeClass>("dispose.sink.fail")
            .Build();

        var sink = new ThrowingReportSink();

        // A disposal failure whose report also fails must not be lost: a sink that is temporarily down
        // must not erase the actual cleanup error.
        var error = await Assert.ThrowsAsync<AggregateException>(
            () => new TestExecutionEngine().RunAsync([test], sink, CancellationToken.None));

        var flat = error.Flatten();
        Assert.Contains(flat.InnerExceptions, static e => e.Message.Contains("dispose boom"));
        Assert.Contains(flat.InnerExceptions, static e => e.Message.Contains("sink is down"));
    }

    [Test]
    public async Task PassingTestWithThrowingAsyncDispose_IsReportedAsTestScopedErrorAsync()
    {
        var test = TestCaseDescriptorBuilder
            .For<ThrowingAsyncDisposeClass>("async.dispose.after.pass")
            .Build();

        var sink = new RecordingSink();
        await new TestExecutionEngine().RunAsync([test], sink, CancellationToken.None);

        // IAsyncDisposable-only instances take the async disposal path; it must be guarded too.
        Assert.Empty(sink.Passed);
        var error = Assert.Single(sink.Errors);
        Assert.Equal("async.dispose.after.pass", error.Test.Id.Value);
        Assert.Contains("async dispose boom", error.Exception.Message);
    }

    [Test]
    public async Task AssemblySetupForeignCancellation_SurfacesAsFailureNotCancellationAsync()
    {
        var engine = new TestExecutionEngine();
        engine.SetGlobalAssemblyLifecycle(
            beforeMethods: [static (_, _) => throw new OperationCanceledException(new CancellationToken(canceled: true))],
            afterMethods: null);

        var test = TestCaseDescriptorBuilder
            .For<SampleTestClass>("assembly.setup.foreign.oce")
            .Serial()
            .Build();

        var sink = new RecordingSink();

        // The run token is never cancelled, so a setup hook's own OCE must surface as a failure, not be
        // misread as run cancellation (which downstream adapters silently swallow, running zero tests).
        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => engine.RunAsync([test], sink, CancellationToken.None));

        Assert.True(error.InnerException is OperationCanceledException);
    }

    [Test]
    public async Task ClassTeardownCancelsRunAndReturnsNormally_SurfacesCancellationAsync()
    {
        using var cts = new CancellationTokenSource();
        var test = TestCaseDescriptorBuilder
            .For<SampleTestClass>("teardown.cancels.run")
            .Serial()
            .WithAfterClass((_, _) =>
            {
                // Cancellation first fires during cleanup, and the hook returns normally.
                cts.Cancel();
                return Task.CompletedTask;
            })
            .Build();

        var sink = new RecordingSink();

        // A run cancelled only during cleanup must still surface, not complete successfully.
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => new TestExecutionEngine().RunAsync([test], sink, cts.Token));

        Assert.Empty(sink.Errors);
    }

    [Test]
    public async Task TimeoutWithOuterCancellation_PropagatesCancellationNotErrorAsync()
    {
        using var cts = new CancellationTokenSource();
        var test = TestCaseDescriptorBuilder
            .For<SampleTestClass>("timeout.plus.cancel")
            .Serial()
            // Large timeout so the timeout CTS does not fire; the outer run token is what cancels.
            .WithTimeout(60_000)
            .WithMethod(async (_, ct) =>
            {
                // ct is the linked (timeout + run) token; cancelling the run makes the delay throw an
                // OCE that carries the linked token, not the run token.
                cts.Cancel();
                await Task.Delay(Timeout.Infinite, ct);
            })
            .Build();

        var sink = new RecordingSink();

        // Genuine run cancellation must surface as OperationCanceledException, not be wrapped as a failure.
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => new TestExecutionEngine().RunAsync([test], sink, cts.Token));

        Assert.Empty(sink.Errors);
    }

    [Test]
    public async Task RetryAfterCancellation_DoesNotRetryOrReportErrorAsync()
    {
        using var cts = new CancellationTokenSource();
        var attempts = 0;
        var test = TestCaseDescriptorBuilder
            .For<SampleTestClass>("retry.after.cancel")
            .Serial()
            .WithRetry(3, delayMs: 0)
            .WithMethod((_, _) =>
            {
                Interlocked.Increment(ref attempts);
                // Cancel the run, then fail with a normal (non-OCE) exception.
                cts.Cancel();
                throw new InvalidOperationException("boom after cancel");
            })
            .Build();

        var sink = new RecordingSink();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => new TestExecutionEngine().RunAsync([test], sink, cts.Token));

        // The cancelled run must not retry the test or report a spurious error.
        Assert.Equal(1, attempts);
        Assert.Empty(sink.Errors);
    }

    [Test]
    public async Task ClassTeardownForeignCancellation_IsReportedAsFailureNotRunCancellationAsync()
    {
        using var cts = new CancellationTokenSource();
        var test = TestCaseDescriptorBuilder
            .For<SampleTestClass>("teardown.foreign.oce")
            .Serial()
            .WithMethod((_, _) =>
            {
                cts.Cancel();
                return Task.CompletedTask;
            })
            // An OCE carrying a token that is NOT the run token is the hook's own cancellation.
            .WithAfterClass(static (_, _) => throw new OperationCanceledException(new CancellationToken(canceled: true)))
            .Build();

        var sink = new RecordingSink();

        // Genuine run cancellation still surfaces from the run body.
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => new TestExecutionEngine().RunAsync([test], sink, cts.Token));

        // The hook's foreign-token OCE must be reported as a teardown failure, not silently classified
        // as run cancellation.
        var error = Assert.Single(sink.Errors);
        Assert.True(error.Test.Id.Value.EndsWith("[ClassTeardown]", StringComparison.Ordinal));
        Assert.True(error.Exception is OperationCanceledException);
    }

    [Test]
    public async Task SinkFailureDuringCleanupReport_DoesNotAbortRemainingReportsAsync()
    {
        var firstClass = TestCaseDescriptorBuilder
            .For<SampleTestClass>("sink.fail.a")
            .WithAfterClass(static (_, _) => throw new InvalidOperationException("teardown boom"))
            .Build();

        var secondClass = TestCaseDescriptorBuilder
            .For<SecondSampleTestClass>("sink.fail.b")
            .WithAfterClass(static (_, _) => throw new InvalidOperationException("teardown boom"))
            .Build();

        var sink = new ThrowingReportSink();

        // A sink that throws on report must not abort the remaining cleanup reports, and the failures
        // must be surfaced (not silently logged) so a teardown failure whose report also failed is
        // never lost.
        var error = await Assert.ThrowsAsync<AggregateException>(
            () => new TestExecutionEngine().RunAsync([firstClass, secondClass], sink, CancellationToken.None));

        Assert.Equal(2, sink.ErrorReportAttempts);

        // Both the original teardown error and the sink's own failure must be preserved.
        var flat = error.Flatten();
        Assert.Contains(flat.InnerExceptions, static e => e.Message.Contains("teardown boom"));
        Assert.Contains(flat.InnerExceptions, static e => e.Message.Contains("sink is down"));
    }

    // Serialized against the other tests that construct a NextUnitFramework: the constructor reads
    // filter environment variables, so the invalid value this test installs would otherwise leak into
    // a concurrently constructed framework.
    [Test]
    [NotInParallel(FilterEnvironmentConstraint.Key)]
    public void InvalidTestNameRegex_SurfacesErrorInsteadOfRunningEverything()
    {
        using var envVar = EnvironmentVariableGuard.Set("NEXTUNIT_TEST_NAME_REGEX", "(unclosed");

        Assert.Throws<ArgumentException>(
            () => new NextUnitFramework(null!, new NullServiceProvider()));
    }

    [Test]
    public async Task ClassSetupFailure_AbortsRunWithoutRetryingSetupAsync()
    {
        // Characterization: class setup runs in the pre-execution skip check, outside the retry loop,
        // so [Retry] never re-runs a failing BeforeClass hook. The failure is not attributed to a test
        // node either - it aborts the run and surfaces from RunAsync.
        var setupInvocations = 0;
        var testInvocations = 0;
        var test = TestCaseDescriptorBuilder
            .For<SampleTestClass>("class.setup.failure")
            .WithRetry(3)
            .WithMethod((_, _) =>
            {
                Interlocked.Increment(ref testInvocations);
                return Task.CompletedTask;
            })
            .WithBeforeClass((_, _) =>
            {
                Interlocked.Increment(ref setupInvocations);
                throw new InvalidOperationException("class setup boom");
            })
            .Build();

        var sink = new RecordingSink();
        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => new TestExecutionEngine().RunAsync([test], sink, CancellationToken.None));

        Assert.Contains("class setup boom", error.Message);
        Assert.Equal(1, setupInvocations);
        Assert.Equal(0, testInvocations);
        Assert.Empty(sink.Passed);
        Assert.Empty(sink.Errors);
        Assert.Empty(sink.Skipped);
    }

    [Test]
    public async Task ClassSetupSkip_SkipsEveryTestInClassAndRunsSetupOnceAsync()
    {
        // Characterization: TestSkippedException is the one class-setup exception that is caught;
        // it marks the class context so every test in the class is reported skipped with that reason,
        // and the setup is not attempted again for the second test.
        var setupInvocations = 0;
        var lifecycle = new LifecycleInfo
        {
            BeforeClassMethods =
            [
                (_, _) =>
                {
                    Interlocked.Increment(ref setupInvocations);
                    throw new TestSkippedException("environment unavailable");
                }
            ]
        };

        TestCaseDescriptor CreateTest(string id) => TestCaseDescriptorBuilder
            .For<SampleTestClass>(id)
            .Serial()
            .WithLifecycle(lifecycle)
            .Build();

        var sink = new RecordingSink();
        await new TestExecutionEngine().RunAsync(
            [CreateTest("class.setup.skip.1"), CreateTest("class.setup.skip.2")],
            sink,
            CancellationToken.None);

        Assert.Equal(1, setupInvocations);
        Assert.Equal(2, sink.Skipped.Count);
        Assert.All(sink.Skipped, static test => Assert.Equal("environment unavailable", test.SkipReason));
        Assert.Empty(sink.Passed);
        Assert.Empty(sink.Errors);
    }

    [Test]
    public async Task ClassAndAssemblyTeardownFailures_ReportOnSeparateScopeNodesAsync()
    {
        // Characterization: both cleanup scopes report through the sink on synthetic nodes - a class
        // teardown failure on a [ClassTeardown] node and an assembly teardown failure on an
        // [AssemblyTeardown] node. Neither swallows the other, and neither fails the passing test.
        var engine = new TestExecutionEngine();
        engine.SetGlobalAssemblyLifecycle(
            beforeMethods: null,
            afterMethods: [static (_, _) => throw new InvalidOperationException("assembly teardown boom")]);

        var test = TestCaseDescriptorBuilder
            .For<SampleTestClass>("teardown.both.scopes")
            .WithAfterClass(static (_, _) => throw new InvalidOperationException("class teardown boom"))
            .Build();

        var sink = new RecordingSink();
        await engine.RunAsync([test], sink, CancellationToken.None);

        Assert.Single(sink.Passed);
        Assert.Equal(2, sink.Errors.Count);
        Assert.Contains(sink.Errors, static error =>
            error.Test.Id.Value.EndsWith("[ClassTeardown]", StringComparison.Ordinal)
            && error.Exception.Message.Contains("class teardown boom"));
        Assert.Contains(sink.Errors, static error =>
            error.Test.Id.Value.EndsWith("[AssemblyTeardown]", StringComparison.Ordinal)
            && error.Exception.Message.Contains("assembly teardown boom"));
    }

    /// <summary>
    /// Narrows an exception to <see cref="AggregateException"/> without a null-forgiving dereference.
    /// </summary>
    private static AggregateException AsAggregate(Exception exception)
    {
        return exception as AggregateException
            ?? throw new AssertionFailedException(
                $"Expected an AggregateException but got {exception.GetType().Name}: {exception.Message}");
    }
}
