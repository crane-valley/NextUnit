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
    public async Task ClassTeardownAndDisposeFailures_ReportDistinctNodesAsync()
    {
        var test = new TestCaseDescriptor
        {
            Id = new TestCaseId("teardown.and.dispose"),
            DisplayName = "teardown.and.dispose",
            TestClass = typeof(ThrowingDisposeClass),
            MethodName = "Ok",
            TestClassFactory = static (_, _) => new ThrowingDisposeClass(),
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
        try
        {
            await new TestExecutionEngine().RunAsync([test], sink, CancellationToken.None);
        }
        catch (InvalidOperationException)
        {
            // The per-test instance also throws on dispose; that is incidental here. This test asserts
            // on the class-scope error nodes captured by the sink, not on the per-test disposal path.
        }

        // Teardown and disposal failures must land on distinct node identities so they do not collide.
        var errorIds = sink.Errors.Select(static e => e.Test.Id.Value).ToList();
        Assert.Contains(errorIds, static id => id.EndsWith("[ClassTeardown]", StringComparison.Ordinal));
        Assert.Contains(errorIds, static id => id.EndsWith("[ClassDispose]", StringComparison.Ordinal));
    }

    [Test]
    public async Task ClassTeardownObservingCancellation_IsNotReportedAsErrorAsync()
    {
        using var cts = new CancellationTokenSource();
        var test = new TestCaseDescriptor
        {
            Id = new TestCaseId("teardown.cancelled"),
            DisplayName = "teardown.cancelled",
            TestClass = typeof(SampleTestClass),
            MethodName = "Ok",
            TestClassFactory = static (_, _) => new SampleTestClass(),
            // Serial execution keeps the parallel loop from observing the token, so the AfterClass
            // teardown is the only place cancellation is first seen (the case that was being lost).
            Parallel = new ParallelInfo { NotInParallel = true },
            TestMethod = (_, _) =>
            {
                cts.Cancel();
                return Task.CompletedTask;
            },
            Lifecycle = new LifecycleInfo
            {
                AfterClassMethods =
                [
                    static (_, ct) =>
                    {
                        // Teardown observes the cancelled run token; this is not a teardown failure.
                        ct.ThrowIfCancellationRequested();
                        return Task.CompletedTask;
                    }
                ]
            }
        };

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
        var test = new TestCaseDescriptor
        {
            Id = new TestCaseId("teardown.multi.hook"),
            DisplayName = "teardown.multi.hook",
            TestClass = typeof(SampleTestClass),
            MethodName = "Ok",
            TestClassFactory = static (_, _) => new SampleTestClass(),
            Parallel = new ParallelInfo { NotInParallel = true },
            TestMethod = (_, _) =>
            {
                cts.Cancel();
                return Task.CompletedTask;
            },
            Lifecycle = new LifecycleInfo
            {
                AfterClassMethods =
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
                ]
            }
        };

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

        var test = new TestCaseDescriptor
        {
            Id = new TestCaseId("assembly.teardown.multi"),
            DisplayName = "assembly.teardown.multi",
            TestClass = typeof(SampleTestClass),
            MethodName = "Ok",
            TestClassFactory = static (_, _) => new SampleTestClass(),
            Parallel = new ParallelInfo { NotInParallel = true },
            TestMethod = (_, _) =>
            {
                cts.Cancel();
                return Task.CompletedTask;
            }
        };

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

        TestCaseDescriptor MakeTest(string id, int priority) => new()
        {
            Id = new TestCaseId(id),
            DisplayName = id,
            TestClass = typeof(SampleTestClass),
            MethodName = "Ok",
            TestClassFactory = static (_, _) => new SampleTestClass(),
            Priority = priority,
            Parallel = new ParallelInfo { NotInParallel = true },
            TestMethod = (_, _) =>
            {
                Interlocked.Increment(ref executions);
                // Ignore the token and return normally; the batch loop must still stop.
                cts.Cancel();
                return Task.CompletedTask;
            }
        };

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
        var test = new TestCaseDescriptor
        {
            Id = new TestCaseId("only.ignores.token"),
            DisplayName = "only.ignores.token",
            TestClass = typeof(SampleTestClass),
            MethodName = "Ok",
            TestClassFactory = static (_, _) => new SampleTestClass(),
            Parallel = new ParallelInfo { NotInParallel = true },
            TestMethod = (_, _) =>
            {
                // The only test ignores the token and completes normally.
                cts.Cancel();
                return Task.CompletedTask;
            }
        };

        var sink = new RecordingSink();

        // With no further loop iteration to observe the token, the run must still surface cancellation.
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => new TestExecutionEngine().RunAsync([test], sink, cts.Token));

        Assert.Single(sink.Passed);
    }

    [Test]
    public async Task MultipleClassTeardownFailures_AggregateIntoSingleNodeAsync()
    {
        var test = new TestCaseDescriptor
        {
            Id = new TestCaseId("teardown.multi.fail"),
            DisplayName = "teardown.multi.fail",
            TestClass = typeof(SampleTestClass),
            MethodName = "Ok",
            TestClassFactory = static (_, _) => new SampleTestClass(),
            TestMethod = static (_, _) => Task.CompletedTask,
            Lifecycle = new LifecycleInfo
            {
                AfterClassMethods =
                [
                    static (_, _) => throw new InvalidOperationException("first boom"),
                    static (_, _) => throw new InvalidOperationException("second boom")
                ]
            }
        };

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

        var test = new TestCaseDescriptor
        {
            Id = new TestCaseId("cancel.plus.failure"),
            DisplayName = "cancel.plus.failure",
            TestClass = typeof(SampleTestClass),
            MethodName = "Ok",
            TestClassFactory = static (_, _) => new SampleTestClass(),
            Parallel = new ParallelInfo { NotInParallel = true },
            TestMethod = (_, _) =>
            {
                cts.Cancel();
                return Task.CompletedTask;
            },
            Lifecycle = new LifecycleInfo
            {
                AfterClassMethods =
                [
                    static (_, ct) =>
                    {
                        ct.ThrowIfCancellationRequested();
                        return Task.CompletedTask;
                    }
                ]
            }
        };

        var sink = new RecordingSink();

        // Run cancellation and a normal teardown failure coexist; neither may be discarded.
        var error = await Assert.ThrowsAsync<AggregateException>(
            () => engine.RunAsync([test], sink, cts.Token));

        Assert.Contains(error.InnerExceptions, static e => e is OperationCanceledException);
        Assert.Contains(error.InnerExceptions, static e => e is InvalidOperationException && e.Message.Contains("assembly boom"));
    }

    [Test]
    public async Task ClassTeardownForeignCancellation_IsReportedAsFailureNotRunCancellationAsync()
    {
        using var cts = new CancellationTokenSource();
        var test = new TestCaseDescriptor
        {
            Id = new TestCaseId("teardown.foreign.oce"),
            DisplayName = "teardown.foreign.oce",
            TestClass = typeof(SampleTestClass),
            MethodName = "Ok",
            TestClassFactory = static (_, _) => new SampleTestClass(),
            Parallel = new ParallelInfo { NotInParallel = true },
            TestMethod = (_, _) =>
            {
                cts.Cancel();
                return Task.CompletedTask;
            },
            Lifecycle = new LifecycleInfo
            {
                AfterClassMethods =
                [
                    // An OCE carrying a token that is NOT the run token is the hook's own cancellation.
                    static (_, _) => throw new OperationCanceledException(new CancellationToken(canceled: true))
                ]
            }
        };

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
        var firstClass = new TestCaseDescriptor
        {
            Id = new TestCaseId("sink.fail.a"),
            DisplayName = "sink.fail.a",
            TestClass = typeof(SampleTestClass),
            MethodName = "Ok",
            TestClassFactory = static (_, _) => new SampleTestClass(),
            TestMethod = static (_, _) => Task.CompletedTask,
            Lifecycle = new LifecycleInfo
            {
                AfterClassMethods = [static (_, _) => throw new InvalidOperationException("teardown boom")]
            }
        };

        var secondClass = new TestCaseDescriptor
        {
            Id = new TestCaseId("sink.fail.b"),
            DisplayName = "sink.fail.b",
            TestClass = typeof(SecondSampleTestClass),
            MethodName = "Ok",
            TestClassFactory = static (_, _) => new SecondSampleTestClass(),
            TestMethod = static (_, _) => Task.CompletedTask,
            Lifecycle = new LifecycleInfo
            {
                AfterClassMethods = [static (_, _) => throw new InvalidOperationException("teardown boom")]
            }
        };

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

    private sealed class SecondSampleTestClass
    {
    }

    private sealed class ThrowingDisposeClass : IDisposable
    {
        public void Dispose() => throw new InvalidOperationException("dispose boom");
    }

    private sealed class NullServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }

    private sealed class ThrowingReportSink : ITestExecutionSink
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
