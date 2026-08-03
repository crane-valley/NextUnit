using System.Runtime.CompilerServices;
using NextUnit.Internal;

namespace NextUnit.Generator.Tests;

/// <summary>
/// Covers the discovery half of deferred <c>[TestData]</c> sources: every discovery entry point must
/// report one placeholder without touching the member, and only the execution-time entry point may
/// read it.
/// </summary>
public sealed class DeferredTestDataExpanderTests
{
    [Fact]
    public async Task ExpandAsync_DeferredSource_ProducesOnePlaceholderAsync()
    {
        var invocations = 0;
        var descriptor = CreateDeferredDescriptor(() => invocations++);

        var testCases = await TestDataExpander.ExpandAsync(
            [descriptor],
            TestContext.Current.CancellationToken);

        var placeholder = Assert.Single(testCases);
        Assert.Equal(0, invocations);
        Assert.Equal(
            $"Tests.Add:{typeof(DeferredTestDataExpanderTests).FullName}.{nameof(Rows)}",
            placeholder.Id.Value);
        Assert.Same(descriptor, placeholder.DeferredDataSource);
    }

    /// <summary>
    /// The placeholder identifier is the row prefix without an index, so splitting it on the first
    /// <c>':'</c> yields the descriptor's base id. That is exactly how the VSTest adapter turns a
    /// selected test back into the descriptor to expand, so a placeholder stays selectable.
    /// </summary>
    [Fact]
    public async Task ExpandAsync_DeferredPlaceholderId_MapsBackToBaseIdAsync()
    {
        var testCases = await TestDataExpander.ExpandAsync(
            [CreateDeferredDescriptor()],
            TestContext.Current.CancellationToken);

        var placeholderId = testCases[0].Id.Value;
        Assert.Equal("Tests.Add", placeholderId[..placeholderId.IndexOf(':', StringComparison.Ordinal)]);
    }

    /// <summary>
    /// A placeholder identifier must not collide with the rows it later expands into, which are
    /// indexed off the same prefix.
    /// </summary>
    [Fact]
    public async Task ExpandDeferredAsync_RowIds_DifferFromPlaceholderIdAsync()
    {
        var descriptor = CreateDeferredDescriptor();
        var cancellationToken = TestContext.Current.CancellationToken;

        var placeholder = (await TestDataExpander.ExpandAsync([descriptor], cancellationToken))[0];
        var rows = await TestDataExpander.ExpandDeferredAsync(descriptor, cancellationToken);

        Assert.DoesNotContain(rows, row => row.Id.Value == placeholder.Id.Value);
        Assert.Equal($"{placeholder.Id.Value}[0]", rows[0].Id.Value);
    }

    [Fact]
    public async Task ExpandAsync_DeferredPlaceholder_CarriesNoInvokerOrArgumentsAsync()
    {
        var testCases = await TestDataExpander.ExpandAsync(
            [CreateDeferredDescriptor()],
            TestContext.Current.CancellationToken);

        var placeholder = testCases[0];
        Assert.Null(placeholder.TestMethod);
        Assert.Null(placeholder.TestMethodWithArguments);
        Assert.Null(placeholder.Arguments);
    }

    /// <summary>
    /// The placeholder names the source it stands for, so a group is distinguishable in a test list
    /// from an ordinary test of the same method.
    /// </summary>
    [Fact]
    public async Task ExpandAsync_DeferredPlaceholder_KeepsMethodMetadataAsync()
    {
        var testCases = await TestDataExpander.ExpandAsync(
            [CreateDeferredDescriptor()],
            TestContext.Current.CancellationToken);

        var placeholder = testCases[0];
        Assert.Equal($"Add (deferred data source: {nameof(Rows)})", placeholder.DisplayName);
        Assert.Equal(new[] { "Method" }, placeholder.Categories);
        Assert.Equal(new[] { "Fast" }, placeholder.Tags);
    }

    /// <summary>
    /// The synchronous entry points the VSTest adapter uses must defer too, or discovery there would
    /// enumerate the very source the user opted out of enumerating.
    /// </summary>
    [Fact]
    public void Expand_DeferredSource_DoesNotInvokeTheMember()
    {
        var invocations = 0;

        var testCases = TestDataExpander
            .Expand([CreateDeferredDescriptor(() => invocations++)], CancellationToken.None)
            .ToList();

        Assert.Single(testCases);
        Assert.Equal(0, invocations);
    }

    [Fact]
    public void ExpandSingle_DeferredSource_DoesNotInvokeTheMember()
    {
        var invocations = 0;

        var testCases = TestDataExpander
            .ExpandSingle(CreateDeferredDescriptor(() => invocations++), CancellationToken.None)
            .ToList();

        Assert.Single(testCases);
        Assert.Equal(0, invocations);
    }

    [Fact]
    public async Task ExpandDeferredAsync_SyncSource_ProducesOneTestCasePerRowAsync()
    {
        var invocations = 0;

        var testCases = await TestDataExpander.ExpandDeferredAsync(
            CreateDeferredDescriptor(() => invocations++),
            TestContext.Current.CancellationToken);

        Assert.Equal(1, invocations);
        Assert.Equal(2, testCases.Count);
        Assert.Equal(new object?[] { 1, 2, 3 }, testCases[0].Arguments);
        Assert.Equal(new object?[] { 4, 5, 9 }, testCases[1].Arguments);
        Assert.All(testCases, testCase => Assert.Null(testCase.DeferredDataSource));
    }

    /// <summary>
    /// Deferral and row shape are independent, so an asynchronous source enumerates at execution
    /// time with the run token exactly as a synchronous one does.
    /// </summary>
    [Fact]
    public async Task ExpandDeferredAsync_AsyncSource_ProducesOneTestCasePerRowAsync()
    {
        var descriptor = new TestDataDescriptor
        {
            BaseId = "Tests.Add",
            DisplayName = "Add",
            TestClass = typeof(Target),
            MethodName = nameof(Target.Add),
            DataSourceName = nameof(StreamRowsAsync),
            DataSourceType = typeof(DeferredTestDataExpanderTests),
            ParameterTypes = [typeof(int), typeof(int), typeof(int)],
            DeferredEnumeration = true,
            AsyncDataSourceProvider = static ct =>
                AsyncDataSourceAdapter.FromAsyncEnumerableAsync(StreamRowsAsync(ct), ct)
        };

        var testCases = await TestDataExpander.ExpandDeferredAsync(
            descriptor,
            TestContext.Current.CancellationToken);

        Assert.Equal(2, testCases.Count);
        Assert.Equal(new object?[] { 1, 2, 3 }, testCases[0].Arguments);
    }

    [Fact]
    public async Task ExpandDeferredAsync_CancelledToken_ThrowsOperationCanceledAsync()
    {
        var descriptor = new TestDataDescriptor
        {
            BaseId = "Tests.Add",
            DisplayName = "Add",
            TestClass = typeof(Target),
            MethodName = nameof(Target.Add),
            DataSourceName = nameof(StreamRowsAsync),
            DataSourceType = typeof(DeferredTestDataExpanderTests),
            ParameterTypes = [typeof(int), typeof(int), typeof(int)],
            DeferredEnumeration = true,
            AsyncDataSourceProvider = static ct =>
                AsyncDataSourceAdapter.FromAsyncEnumerableAsync(StreamRowsAsync(ct), ct)
        };

        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Xunit.Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await TestDataExpander.ExpandDeferredAsync(descriptor, cancellation.Token));
    }

    /// <summary>
    /// A synchronous source has no await point of its own, so the expander is the only thing that
    /// can stop it. A run cancelled before its first test must not start reading a source that was
    /// deferred precisely because reading it is expensive.
    /// </summary>
    [Fact]
    public async Task ExpandDeferredAsync_SyncSourceWithCancelledToken_DoesNotInvokeTheMemberAsync()
    {
        var invocations = 0;
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Xunit.Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await TestDataExpander.ExpandDeferredAsync(
                CreateDeferredDescriptor(() => invocations++),
                cancellation.Token));

        Assert.Equal(0, invocations);
    }

    /// <summary>
    /// A lazy synchronous sequence is enumerated on the calling thread, so cancellation raised while
    /// it is yielding can only be observed between rows.
    /// </summary>
    [Fact]
    public async Task ExpandDeferredAsync_SyncSourceCancelledMidEnumeration_StopsEarlyAsync()
    {
        using var cancellation = new CancellationTokenSource();
        var rowsYielded = 0;

        var descriptor = new TestDataDescriptor
        {
            BaseId = "Tests.Add",
            DisplayName = "Add",
            TestClass = typeof(Target),
            MethodName = nameof(Target.Add),
            DataSourceName = "EndlessRows",
            DataSourceType = typeof(DeferredTestDataExpanderTests),
            ParameterTypes = [typeof(int), typeof(int), typeof(int)],
            DeferredEnumeration = true,
            DataSourceProvider = () => EndlessRows(cancellation, () => rowsYielded++)
        };

        await Xunit.Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await TestDataExpander.ExpandDeferredAsync(descriptor, cancellation.Token));

        // Without the per-row check this source would never stop yielding.
        Assert.True(rowsYielded < 100, $"Enumeration continued for {rowsYielded} rows after cancellation.");
    }

    [Fact]
    public async Task ExpandAsync_MixedDescriptors_DefersOnlyTheOptedInSourceAsync()
    {
        var deferredInvocations = 0;
        var eagerInvocations = 0;

        var testCases = await TestDataExpander.ExpandAsync(
            [
                CreateDeferredDescriptor(() => deferredInvocations++),
                CreateEagerDescriptor(() => eagerInvocations++)
            ],
            TestContext.Current.CancellationToken);

        Assert.Equal(0, deferredInvocations);
        Assert.Equal(1, eagerInvocations);
        Assert.Equal(3, testCases.Count);
        Assert.Single(testCases, testCase => testCase.DeferredDataSource is not null);
    }

    private static TestDataDescriptor CreateDeferredDescriptor(Action? onInvoke = null) =>
        CreateDescriptor(deferred: true, onInvoke);

    private static TestDataDescriptor CreateEagerDescriptor(Action? onInvoke = null) =>
        CreateDescriptor(deferred: false, onInvoke);

    private static TestDataDescriptor CreateDescriptor(bool deferred, Action? onInvoke) => new()
    {
        BaseId = "Tests.Add",
        DisplayName = "Add",
        TestClass = typeof(Target),
        MethodName = nameof(Target.Add),
        DataSourceName = nameof(Rows),
        DataSourceType = typeof(DeferredTestDataExpanderTests),
        ParameterTypes = [typeof(int), typeof(int), typeof(int)],
        Categories = ["Method"],
        Tags = ["Fast"],
        DeferredEnumeration = deferred,
        DataSourceProvider = () =>
        {
            onInvoke?.Invoke();
            return Rows();
        }
    };

    private static IEnumerable<object[]> Rows()
    {
        yield return [1, 2, 3];
        yield return [4, 5, 9];
    }

    /// <summary>
    /// Yields rows forever, cancelling the token from the third one. Nothing inside the sequence
    /// observes the token, so only the expander's own check can end the enumeration.
    /// </summary>
    private static IEnumerable<object[]> EndlessRows(CancellationTokenSource cancellation, Action onRow)
    {
        for (var index = 0; ; index++)
        {
            if (index == 2)
            {
                cancellation.Cancel();
            }

            onRow();
            yield return [index, index, index * 2];
        }
    }

    private static async IAsyncEnumerable<object[]> StreamRowsAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await Task.Yield();
        cancellationToken.ThrowIfCancellationRequested();
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
