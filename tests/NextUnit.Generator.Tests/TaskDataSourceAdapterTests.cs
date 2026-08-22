using System.Collections;
using NextUnit.Internal;

namespace NextUnit.Generator.Tests;

/// <summary>
/// Covers the runtime half of a task-wrapped <c>[TestData]</c> source that offers more than one row
/// type: the descriptors here are built by hand exactly as the generator emits them, so the adapter
/// and the expander are exercised without going through a full generator run.
/// </summary>
public sealed class TaskDataSourceAdapterTests
{
    /// <summary>
    /// The arm the converter names is the arm the run reads, which is the point of the converter.
    /// </summary>
    [Fact]
    public async Task ExpandAsync_SourceWithTwoArms_ReadsTheNamedArmAsync()
    {
        var descriptor = CreateDescriptor(
            static ct => AsyncDataSourceAdapter.FromTaskAsync(
                Task.FromResult(new DualRows()),
                static rows => DataSourceAdapter.FromEnumerable<TestDataRow<int>>(rows),
                ct));

        var testCases = await TestDataExpander.ExpandAsync(
            [descriptor],
            TestContext.Current.CancellationToken);

        var testCase = Assert.Single(testCases);
        Assert.Equal(new object?[] { TypedArmValue }, testCase.Arguments);
    }

    /// <summary>
    /// The same source read the way the two-argument call reads it, pinning what the converter
    /// changes: the awaited collection is enumerated through the non-generic <c>IEnumerable</c>,
    /// which dispatches to whatever the source type mapped that interface to -- here the arm nothing
    /// validated.
    /// </summary>
    [Fact]
    public async Task ExpandAsync_SourceWithTwoArms_ReadUntyped_ReadsTheNonGenericArmAsync()
    {
        var descriptor = CreateDescriptor(
            static ct => AsyncDataSourceAdapter.FromTaskAsync(Task.FromResult(new DualRows()), ct));

        var testCases = await TestDataExpander.ExpandAsync(
            [descriptor],
            TestContext.Current.CancellationToken);

        var testCase = Assert.Single(testCases);
        Assert.Equal(new object?[] { UntypedArmValue }, testCase.Arguments);
    }

    /// <summary>
    /// A converter that finds no collection to read reports what a member completing with a null
    /// collection reports, rather than a <c>NullReferenceException</c> from inside the enumeration.
    /// </summary>
    [Fact]
    public async Task ExpandAsync_ConverterReturningNull_ReportsTheNullCollectionAsync()
    {
        var descriptor = CreateDescriptor(
            static ct => AsyncDataSourceAdapter.FromTaskAsync(
                Task.FromResult(new DualRows()),
                static _ => null,
                ct));

        var exception = await Xunit.Assert.ThrowsAsync<InvalidOperationException>(
            async () => await TestDataExpander.ExpandAsync(
                [descriptor],
                TestContext.Current.CancellationToken));

        Xunit.Assert.Contains("null collection", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// A null converter is refused where a null source already is, and at the same moment: both
    /// overloads hand the enumerable back before anything is read, so the check runs on the first
    /// move rather than at the call.
    /// </summary>
    [Fact]
    public async Task FromTaskAsync_NullConverter_ThrowsOnTheFirstMoveAsync()
    {
        var rows = AsyncDataSourceAdapter.FromTaskAsync(
            Task.FromResult(new DualRows()),
            reader: null!,
            TestContext.Current.CancellationToken);

        await Xunit.Assert.ThrowsAsync<ArgumentNullException>(async () =>
        {
            await foreach (var row in rows)
            {
                Assert.Null(row);
            }
        });
    }

    private const int UntypedArmValue = 1;
    private const int TypedArmValue = 2;

    private static TestDataDescriptor CreateDescriptor(AsyncDataSourceProviderDelegate provider) => new()
    {
        BaseId = "Tests.Echo",
        TestClass = typeof(Target),
        MethodName = nameof(Target.Echo),
        DataSourceName = "Rows",
        DataSourceType = typeof(TaskDataSourceAdapterTests),
        ParameterTypes = [typeof(int)],
        AsyncDataSourceProvider = provider
    };

    private sealed class Target
    {
        public void Echo(int value)
        {
        }
    }

    /// <summary>
    /// Implements the element interface twice, with a third, non-generic implementation that agrees
    /// with the untyped arm -- the arm a runtime that reads through <c>IEnumerable</c> gets.
    /// </summary>
    private sealed class DualRows : IEnumerable<object[]>, IEnumerable<TestDataRow<int>>
    {
        IEnumerator<object[]> IEnumerable<object[]>.GetEnumerator() => Untyped().GetEnumerator();

        IEnumerator<TestDataRow<int>> IEnumerable<TestDataRow<int>>.GetEnumerator() => Typed().GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => Untyped().GetEnumerator();

        private static IEnumerable<object[]> Untyped()
        {
            yield return [UntypedArmValue];
        }

        private static IEnumerable<TestDataRow<int>> Typed()
        {
            yield return new TestDataRow<int>(TypedArmValue);
        }
    }
}
