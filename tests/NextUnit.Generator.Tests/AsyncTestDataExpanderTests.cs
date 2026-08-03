using System.Runtime.CompilerServices;
using NextUnit.Internal;

namespace NextUnit.Generator.Tests;

/// <summary>
/// Covers the runtime half of asynchronous <c>[TestData]</c> sources: the descriptors here are
/// built by hand exactly as the generator emits them, so the adapter, the expander, and the
/// blocking bridge are exercised without going through a full generator run.
/// </summary>
public sealed class AsyncTestDataExpanderTests
{
    [Fact]
    public async Task ExpandAsync_AsyncEnumerableSource_ProducesOneTestCasePerRowAsync()
    {
        var descriptor = CreateDescriptor(
            nameof(StreamRowsAsync),
            static ct => AsyncDataSourceAdapter.FromAsyncEnumerableAsync(StreamRowsAsync(ct), ct));

        var testCases = await TestDataExpander.ExpandAsync(
            [descriptor],
            TestContext.Current.CancellationToken);

        Assert.Equal(2, testCases.Count);
        Assert.Equal(new object?[] { 1, 2, 3 }, testCases[0].Arguments);
        Assert.Equal(new object?[] { 4, 5, 9 }, testCases[1].Arguments);
        Assert.Equal(
            $"Tests.Add:{typeof(AsyncTestDataExpanderTests).FullName}.{nameof(StreamRowsAsync)}[1]",
            testCases[1].Id.Value);
    }

    [Fact]
    public async Task ExpandAsync_TaskWrappedSource_ProducesTestCasesAsync()
    {
        var descriptor = CreateDescriptor(
            nameof(TaskRowsAsync),
            static ct => AsyncDataSourceAdapter.FromTaskAsync(TaskRowsAsync(), ct));

        var testCases = await TestDataExpander.ExpandAsync(
            [descriptor],
            TestContext.Current.CancellationToken);

        var testCase = Assert.Single(testCases);
        Assert.Equal(new object?[] { 7, 8, 15 }, testCase.Arguments);
    }

    [Fact]
    public async Task ExpandAsync_ValueTaskWrappedSource_ProducesTestCasesAsync()
    {
        var descriptor = CreateDescriptor(
            nameof(ValueTaskRowsAsync),
            static ct => AsyncDataSourceAdapter.FromTaskAsync(ValueTaskRowsAsync().AsTask(), ct));

        var testCases = await TestDataExpander.ExpandAsync(
            [descriptor],
            TestContext.Current.CancellationToken);

        var testCase = Assert.Single(testCases);
        Assert.Equal(new object?[] { 2, 2, 4 }, testCase.Arguments);
    }

    /// <summary>
    /// A typed row keeps its per-row metadata when it arrives from an asynchronous source, so async
    /// sources are not a second-class path for display names, categories, tags, and skip reasons.
    /// </summary>
    [Fact]
    public async Task ExpandAsync_TypedRows_PreserveRowMetadataAsync()
    {
        var descriptor = CreateDescriptor(
            nameof(TypedRowsAsync),
            static ct => AsyncDataSourceAdapter.FromAsyncEnumerableAsync(TypedRowsAsync(ct), ct));

        var testCases = await TestDataExpander.ExpandAsync(
            [descriptor],
            TestContext.Current.CancellationToken);

        var testCase = Assert.Single(testCases);
        Assert.Equal("async typed row", testCase.DisplayName);
        Assert.Equal(new object?[] { 2, 3, 5 }, testCase.Arguments);
        Assert.Equal(new[] { "Method", "Row" }, testCase.Categories);
        Assert.Equal(new[] { "Fast", "Streamed" }, testCase.Tags);
        Assert.True(testCase.IsSkipped);
        Assert.Equal("Tracked issue", testCase.SkipReason);
    }

    /// <summary>
    /// The synchronous entry point still works against an asynchronous source, which is what the
    /// VSTest adapter depends on.
    /// </summary>
    [Fact]
    public void ExpandSingle_AsyncSource_DrainsWithoutDeadlock()
    {
        var descriptor = CreateDescriptor(
            nameof(StreamRowsAsync),
            static ct => AsyncDataSourceAdapter.FromAsyncEnumerableAsync(StreamRowsAsync(ct), ct));

        var testCases = TestDataExpander
            .ExpandSingle(descriptor, TestContext.Current.CancellationToken)
            .ToList();

        Assert.Equal(2, testCases.Count);
    }

    [Fact]
    public async Task ExpandAsync_CancelledToken_ThrowsOperationCanceledAsync()
    {
        var descriptor = CreateDescriptor(
            nameof(StreamRowsAsync),
            static ct => AsyncDataSourceAdapter.FromAsyncEnumerableAsync(StreamRowsAsync(ct), ct));

        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Xunit.Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await TestDataExpander.ExpandAsync([descriptor], cancellation.Token));
    }

    /// <summary>
    /// A source that ignores the token it was handed must still be interruptible, because the
    /// adapter checks the token itself between rows.
    /// </summary>
    [Fact]
    public async Task ExpandAsync_SourceIgnoringToken_StillCancelsAsync()
    {
        using var cancellation = new CancellationTokenSource();
        var descriptor = CreateDescriptor(
            nameof(IgnoringRowsAsync),
            _ => AsyncDataSourceAdapter.FromAsyncEnumerableAsync(
                IgnoringRowsAsync(cancellation),
                cancellation.Token));

        await Xunit.Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await TestDataExpander.ExpandAsync([descriptor], cancellation.Token));
    }

    /// <summary>
    /// A source that blocks forever inside <c>MoveNextAsync</c> must still be interruptible.
    /// Forwarding the token cannot cancel a move that is already in flight, so only the expander's
    /// race against the token can end this.
    /// </summary>
    [Fact]
    public async Task ExpandAsync_SourceThatNeverYields_IsStillCancellableAsync()
    {
        using var cancellation = new CancellationTokenSource();
        var enumerationStarted = new TaskCompletionSource();
        var descriptor = CreateDescriptor(
            "NeverYieldingRows",
            _ => NeverYieldingRowsAsync(enumerationStarted));

        var expansion = TestDataExpander.ExpandAsync([descriptor], cancellation.Token).AsTask();
        await enumerationStarted.Task;
        await cancellation.CancelAsync();

        await Xunit.Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await expansion);
    }

    /// <summary>
    /// The same guarantee for a task-wrapped source, which takes no token at all.
    /// </summary>
    [Fact]
    public async Task ExpandAsync_TaskSourceThatNeverCompletes_IsStillCancellableAsync()
    {
        using var cancellation = new CancellationTokenSource();
        var pending = new TaskCompletionSource<IEnumerable<object[]>>();
        var descriptor = CreateDescriptor(
            "PendingTaskRows",
            ct => AsyncDataSourceAdapter.FromTaskAsync(pending.Task, ct));

        var expansion = TestDataExpander.ExpandAsync([descriptor], cancellation.Token).AsTask();
        await cancellation.CancelAsync();

        await Xunit.Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await expansion);
    }

    [Fact]
    public async Task ExpandAsync_SourceThrows_PropagatesOriginalExceptionAsync()
    {
        var descriptor = CreateDescriptor(
            nameof(ThrowingRowsAsync),
            static ct => AsyncDataSourceAdapter.FromAsyncEnumerableAsync(ThrowingRowsAsync(ct), ct));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await TestDataExpander.ExpandAsync(
                [descriptor],
                TestContext.Current.CancellationToken));

        Assert.Equal("data source failed", exception.Message);
    }

    /// <summary>
    /// A synchronous descriptor keeps taking the synchronous path even when the asynchronous
    /// overloads are used, so the two kinds of source can coexist in one registry.
    /// </summary>
    [Fact]
    public async Task ExpandAsync_SyncSource_StillExpandsAsync()
    {
        var descriptor = new TestDataDescriptor
        {
            BaseId = "Tests.Add",
            TestClass = typeof(Target),
            MethodName = nameof(Target.Add),
            DataSourceName = nameof(SyncRows),
            DataSourceType = typeof(AsyncTestDataExpanderTests),
            ParameterTypes = [typeof(int), typeof(int), typeof(int)],
            DataSourceProvider = static () => SyncRows()
        };

        var testCases = await TestDataExpander.ExpandAsync(
            [descriptor],
            TestContext.Current.CancellationToken);

        var testCase = Assert.Single(testCases);
        Assert.Equal(new object?[] { 1, 1, 2 }, testCase.Arguments);
    }

    [Fact]
    public async Task FromTask_NullCollection_ThrowsAsync()
    {
        var descriptor = CreateDescriptor(
            nameof(NullRowsAsync),
            static ct => AsyncDataSourceAdapter.FromTaskAsync(NullRowsAsync(), ct));

        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await TestDataExpander.ExpandAsync(
                [descriptor],
                TestContext.Current.CancellationToken));
    }

    private static TestDataDescriptor CreateDescriptor(
        string dataSourceName,
        AsyncDataSourceProviderDelegate asyncProvider) => new()
        {
            BaseId = "Tests.Add",
            TestClass = typeof(Target),
            MethodName = nameof(Target.Add),
            DataSourceName = dataSourceName,
            DataSourceType = typeof(AsyncTestDataExpanderTests),
            ParameterTypes = [typeof(int), typeof(int), typeof(int)],
            Categories = ["Method"],
            Tags = ["Fast"],
            AsyncDataSourceProvider = asyncProvider
        };

    private static async IAsyncEnumerable<object[]> StreamRowsAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await Task.Yield();
        cancellationToken.ThrowIfCancellationRequested();
        yield return [1, 2, 3];
        yield return [4, 5, 9];
    }

    private static async IAsyncEnumerable<TestDataRow<(int A, int B, int Expected)>> TypedRowsAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await Task.Yield();
        cancellationToken.ThrowIfCancellationRequested();
        yield return new TestDataRow<(int A, int B, int Expected)>(
            (2, 3, 5),
            displayName: "async typed row",
            categories: ["Row"],
            tags: ["Streamed"],
            skipReason: "Tracked issue");
    }

    /// <summary>
    /// Yields one row, then cancels without ever observing the token, so only the adapter's own
    /// check can stop the enumeration.
    /// </summary>
    private static async IAsyncEnumerable<object[]> IgnoringRowsAsync(CancellationTokenSource cancellation)
    {
        await Task.Yield();
        yield return [1, 2, 3];
        await cancellation.CancelAsync();
        yield return [4, 5, 9];
    }

    /// <summary>
    /// Signals that enumeration reached the source, then never produces a row and never observes
    /// any token.
    /// </summary>
    private static async IAsyncEnumerable<object?> NeverYieldingRowsAsync(TaskCompletionSource enumerationStarted)
    {
        enumerationStarted.SetResult();
        await new TaskCompletionSource().Task;
        yield return new object[] { 1, 2, 3 };
    }

    private static async IAsyncEnumerable<object[]> ThrowingRowsAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await Task.Yield();
        cancellationToken.ThrowIfCancellationRequested();
        yield return [1, 2, 3];
        throw new InvalidOperationException("data source failed");
    }

    private static Task<IEnumerable<object[]>> TaskRowsAsync() =>
        Task.FromResult<IEnumerable<object[]>>([[7, 8, 15]]);

    private static ValueTask<IReadOnlyList<object[]>> ValueTaskRowsAsync() =>
        new(new object[][] { [2, 2, 4] });

    private static Task<IEnumerable<object[]>> NullRowsAsync() =>
        Task.FromResult<IEnumerable<object[]>>(null!);

    private static IEnumerable<object[]> SyncRows()
    {
        yield return [1, 1, 2];
    }

    private sealed class Target
    {
        public void Add(int a, int b, int expected)
        {
        }
    }
}
