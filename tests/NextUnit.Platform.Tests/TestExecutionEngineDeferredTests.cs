using NextUnit.Internal;

namespace NextUnit.Platform.Tests;

/// <summary>
/// Covers the execution half of deferred <c>[TestData]</c> sources: the engine replaces the single
/// placeholder discovery produced with the rows it stands for, before anything downstream can see
/// that the test case list ever contained a placeholder.
/// </summary>
public sealed class TestExecutionEngineDeferredTests
{
    [Test]
    public async Task RunAsync_DeferredPlaceholder_ReportsOneResultPerRowAsync()
    {
        var sink = new RecordingSink();
        var placeholder = CreatePlaceholder();

        await new TestExecutionEngine().RunAsync([placeholder], sink, CancellationToken.None);

        Assert.Equal(2, sink.Passed.Count);
        Assert.Empty(sink.Errors);
        Assert.DoesNotContain(sink.Passed, test => test.Id.Value == placeholder.Id.Value);
        Assert.Contains(sink.Passed, test => test.Id.Value == $"{placeholder.Id.Value}[0]");
        Assert.Contains(sink.Passed, test => test.Id.Value == $"{placeholder.Id.Value}[1]");
    }

    /// <summary>
    /// The rows must arrive as ordinary test cases, so retry, timeout, parallel constraints, labels,
    /// and priority behave exactly as they do for an eagerly expanded source.
    /// </summary>
    [Test]
    public async Task RunAsync_DeferredRows_InheritMethodLevelMetadataAsync()
    {
        var sink = new RecordingSink();

        await new TestExecutionEngine().RunAsync([CreatePlaceholder()], sink, CancellationToken.None);

        Assert.All(sink.Passed, test =>
        {
            Assert.Equal("Deferred", Assert.Single(test.Categories));
            Assert.Null(test.DeferredDataSource);
            Assert.NotNull(test.TestMethodWithArguments);
        });
    }

    /// <summary>
    /// The source is read once, when the run reaches it, and not at all before then.
    /// </summary>
    [Test]
    public async Task RunAsync_DeferredPlaceholder_ReadsTheSourceExactlyOnceAsync()
    {
        var invocations = 0;
        var placeholder = CreatePlaceholder(() => Interlocked.Increment(ref invocations));

        Assert.Equal(0, invocations);

        await new TestExecutionEngine().RunAsync([placeholder], new RecordingSink(), CancellationToken.None);

        Assert.Equal(1, invocations);
    }

    /// <summary>
    /// A source that throws has no rows to attribute the failure to, so it is reported on the
    /// placeholder. The rest of the run must still happen: one unreachable data source cannot be
    /// allowed to cost the user every other test in the assembly.
    /// </summary>
    [Test]
    public async Task RunAsync_DeferredSourceThrows_ReportsErrorOnPlaceholderAndRunsTheRestAsync()
    {
        var sink = new RecordingSink();
        var placeholder = CreateThrowingPlaceholder();
        var ordinary = TestCaseDescriptorBuilder.For<Target>("Tests.Other").Build();

        await new TestExecutionEngine().RunAsync([placeholder, ordinary], sink, CancellationToken.None);

        var failure = Assert.Single(sink.Errors);
        Assert.Equal(placeholder.Id.Value, failure.Test.Id.Value);
        Assert.Equal("data source failed", failure.Exception.Message);
        Assert.Equal("Tests.Other", Assert.Single(sink.Passed).Id.Value);
    }

    /// <summary>
    /// A skipped test's rows would all be reported skipped, so reading the source would pay its full
    /// cost to produce nothing but a longer list of skips.
    /// </summary>
    [Test]
    public async Task RunAsync_SkippedDeferredPlaceholder_ReportsOneSkipWithoutReadingTheSourceAsync()
    {
        var invocations = 0;
        var sink = new RecordingSink();
        var placeholder = CreatePlaceholder(() => Interlocked.Increment(ref invocations));

        await new TestExecutionEngine().RunAsync(
            [placeholder.WithSkipReason("not today")],
            sink,
            CancellationToken.None);

        Assert.Equal(0, invocations);
        var skipped = Assert.Single(sink.Skipped);
        Assert.Equal(placeholder.Id.Value, skipped.Id.Value);
        Assert.Equal("not today", skipped.SkipReason);
        Assert.Empty(sink.Errors);
    }

    /// <summary>
    /// Run cancellation is not a data source failure, so it must end the run rather than be reported
    /// as an error on the placeholder.
    /// </summary>
    [Test]
    public async Task RunAsync_CancelledBeforeExpansion_PropagatesCancellationAsync()
    {
        var invocations = 0;
        var sink = new RecordingSink();
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(
            async () => await new TestExecutionEngine().RunAsync(
                [CreateAsyncPlaceholder(() => Interlocked.Increment(ref invocations))],
                sink,
                cancellation.Token));

        Assert.Equal(0, invocations);
        Assert.Empty(sink.Errors);
    }

    /// <summary>
    /// Discovery advertised the placeholder, so a source that turns out to have no rows must still
    /// report something. Dropping it would leave a test the user can see and select but that never
    /// reports anything, and a run missing it would still pass.
    /// </summary>
    [Test]
    public async Task RunAsync_DeferredSourceWithNoRows_ReportsThePlaceholderAsSkippedAsync()
    {
        var sink = new RecordingSink();
        var placeholder = CreateEmptyPlaceholder();

        await new TestExecutionEngine().RunAsync([placeholder], sink, CancellationToken.None);

        var skipped = Assert.Single(sink.Skipped);
        Assert.Equal(placeholder.Id.Value, skipped.Id.Value);
        Assert.Equal("The deferred data source 'EmptyRows' produced no rows.", skipped.SkipReason);
        Assert.Empty(sink.Errors);
        Assert.Empty(sink.Passed);
    }

    /// <summary>
    /// A failed expansion must not strand the engine: the non-reentrancy claim is released like any
    /// other run, so the same instance can be used again.
    /// </summary>
    [Test]
    public async Task RunAsync_AfterDeferredSourceFailure_EngineIsReusableAsync()
    {
        var engine = new TestExecutionEngine();
        var sink = new RecordingSink();

        await engine.RunAsync([CreateThrowingPlaceholder()], sink, CancellationToken.None);
        await engine.RunAsync(
            [TestCaseDescriptorBuilder.For<Target>("Tests.Later").Build()],
            sink,
            CancellationToken.None);

        Assert.Equal("Tests.Later", Assert.Single(sink.Passed).Id.Value);
    }

    private static TestCaseDescriptor CreatePlaceholder(Action? onInvoke = null) =>
        SinglePlaceholder(new TestDataDescriptor
        {
            BaseId = "Tests.Add",
            DisplayName = "Add",
            TestClass = typeof(Target),
            MethodName = nameof(Target.Add),
            DataSourceName = nameof(Rows),
            DataSourceType = typeof(TestExecutionEngineDeferredTests),
            ParameterTypes = [typeof(int), typeof(int), typeof(int)],
            Categories = ["Deferred"],
            DeferredEnumeration = true,
            TestClassFactory = static (_, _) => new Target(),
            TestMethodWithArguments = static (_, _, _) => Task.CompletedTask,
            DataSourceProvider = () =>
            {
                onInvoke?.Invoke();
                return Rows();
            }
        });

    private static TestCaseDescriptor CreateThrowingPlaceholder() =>
        SinglePlaceholder(new TestDataDescriptor
        {
            BaseId = "Tests.Broken",
            DisplayName = "Broken",
            TestClass = typeof(Target),
            MethodName = nameof(Target.Add),
            DataSourceName = "BrokenRows",
            DataSourceType = typeof(TestExecutionEngineDeferredTests),
            ParameterTypes = [typeof(int), typeof(int), typeof(int)],
            DeferredEnumeration = true,
            TestClassFactory = static (_, _) => new Target(),
            TestMethodWithArguments = static (_, _, _) => Task.CompletedTask,
            DataSourceProvider = static () => throw new InvalidOperationException("data source failed")
        });

    private static TestCaseDescriptor CreateEmptyPlaceholder() =>
        SinglePlaceholder(new TestDataDescriptor
        {
            BaseId = "Tests.Empty",
            DisplayName = "Empty",
            TestClass = typeof(Target),
            MethodName = nameof(Target.Add),
            DataSourceName = "EmptyRows",
            DataSourceType = typeof(TestExecutionEngineDeferredTests),
            ParameterTypes = [typeof(int), typeof(int), typeof(int)],
            DeferredEnumeration = true,
            TestClassFactory = static (_, _) => new Target(),
            TestMethodWithArguments = static (_, _, _) => Task.CompletedTask,
            DataSourceProvider = static () => Array.Empty<object[]>()
        });

    private static TestCaseDescriptor CreateAsyncPlaceholder(Action onInvoke) =>
        SinglePlaceholder(new TestDataDescriptor
        {
            BaseId = "Tests.Streamed",
            DisplayName = "Streamed",
            TestClass = typeof(Target),
            MethodName = nameof(Target.Add),
            DataSourceName = "StreamedRows",
            DataSourceType = typeof(TestExecutionEngineDeferredTests),
            ParameterTypes = [typeof(int), typeof(int), typeof(int)],
            DeferredEnumeration = true,
            TestClassFactory = static (_, _) => new Target(),
            TestMethodWithArguments = static (_, _, _) => Task.CompletedTask,
            AsyncDataSourceProvider = ct =>
            {
                onInvoke();
                return AsyncDataSourceAdapter.FromTaskAsync(
                    Task.FromResult<IEnumerable<object[]>>(Rows()),
                    ct);
            }
        });

    /// <summary>
    /// Produces the placeholder through the ordinary discovery path, so these tests exercise the
    /// same object the adapters hand the engine.
    /// </summary>
    private static TestCaseDescriptor SinglePlaceholder(TestDataDescriptor descriptor) =>
        TestDataExpander.ExpandSingle(descriptor, CancellationToken.None).Single();

    private static IEnumerable<object[]> Rows()
    {
        yield return [1, 2, 3];
        yield return [4, 5, 9];
    }

    private sealed class Target
    {
        public void Add(int a, int b, int expected)
        {
        }
    }
}
