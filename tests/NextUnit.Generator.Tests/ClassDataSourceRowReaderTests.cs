using System.Collections;
using NextUnit.Internal;

namespace NextUnit.Generator.Tests;

/// <summary>
/// Covers the runtime half of a <c>[ClassDataSource]</c> whose type offers more than one row type:
/// the descriptors here are built by hand exactly as the generator emits them, so the reader and
/// the expander are exercised without going through a full generator run.
/// </summary>
/// <remarks>
/// Every descriptor uses <see cref="SharedType.None"/>, which the shared instance store neither
/// records nor disposes, so nothing here touches the process-wide store the sharing tests populate.
/// Disposal under a real sharing scope is pinned in <c>SharedInstanceStoreTests</c>.
/// </remarks>
public sealed class ClassDataSourceRowReaderTests
{
    /// <summary>
    /// The arm the reader names is the arm the run reads, which is the point of the reader.
    /// </summary>
    [Fact]
    public void ExpandSingle_SourceWithTwoArms_ReadsTheNamedArm()
    {
        var descriptor = CreateDescriptor(
            [typeof(DualRows)],
            [static source => DataSourceAdapter.FromEnumerable<TestDataRow<int>>((IEnumerable<TestDataRow<int>>)source)]);

        var testCase = Assert.Single(ClassDataSourceExpander.ExpandSingle(descriptor).ToList());
        Assert.Equal(new object?[] { TypedArmValue }, testCase.Arguments);
    }

    /// <summary>
    /// The same source read the way the expander used to read every instance, pinning what the
    /// reader changes: the store hands the instance over as <c>object</c> and the expander reads it
    /// back as a non-generic <c>IEnumerable</c>, which dispatches to whatever the source type mapped
    /// that interface to -- here the arm nothing validated.
    /// </summary>
    [Fact]
    public void ExpandSingle_SourceWithTwoArms_ReadUntyped_ReadsTheNonGenericArm()
    {
        var descriptor = CreateDescriptor([typeof(DualRows)], rowReaders: []);

        var testCase = Assert.Single(ClassDataSourceExpander.ExpandSingle(descriptor).ToList());
        Assert.Equal(new object?[] { UntypedArmValue }, testCase.Arguments);
    }

    /// <summary>
    /// The readers are read by the index of the source they belong to, so the second source's reader
    /// is never applied to the first. A compacted array would cast one instance through the other's
    /// row type, which is the failure this alignment exists to prevent.
    /// </summary>
    [Fact]
    public void ExpandSingle_ReaderOnTheSecondSourceOnly_LeavesTheFirstOnItsDirectRead()
    {
        var descriptor = CreateDescriptor(
            [typeof(SingleArmRows), typeof(DualRows)],
            [null, static source => DataSourceAdapter.FromEnumerable<TestDataRow<int>>((IEnumerable<TestDataRow<int>>)source)]);

        var testCases = ClassDataSourceExpander.ExpandSingle(descriptor).ToList();

        Assert.Equal(2, testCases.Count);
        Assert.Equal(new object?[] { SingleArmValue }, testCases[0].Arguments);
        Assert.Equal(new object?[] { TypedArmValue }, testCases[1].Arguments);
    }

    /// <summary>
    /// A reader that finds no rows to read contributes none, which is what a non-enumerable instance
    /// has always contributed. The sources beside it are unaffected.
    /// </summary>
    [Fact]
    public void ExpandSingle_ReaderReturningNull_ContributesNoRows()
    {
        var descriptor = CreateDescriptor(
            [typeof(DualRows), typeof(SingleArmRows)],
            [static _ => null, null]);

        var testCase = Assert.Single(ClassDataSourceExpander.ExpandSingle(descriptor).ToList());
        Assert.Equal(new object?[] { SingleArmValue }, testCase.Arguments);
    }

    /// <summary>
    /// An instance the reader cannot read reports the source type, rather than an
    /// <c>InvalidCastException</c> naming two types the user never wrote. Only a hand-built
    /// descriptor reaches this: the generator emits the reader and the type together.
    /// </summary>
    [Fact]
    public void ExpandSingle_ReaderThatCannotReadTheInstance_NamesTheSourceType()
    {
        var descriptor = CreateDescriptor(
            [typeof(SingleArmRows)],
            [static source => DataSourceAdapter.FromEnumerable<TestDataRow<int>>((IEnumerable<TestDataRow<int>>)source)]);

        var exception = Assert.Throws<InvalidOperationException>(
            () => ClassDataSourceExpander.ExpandSingle(descriptor).ToList());

        Xunit.Assert.Contains(nameof(SingleArmRows), exception.Message, StringComparison.Ordinal);
    }

    private const int UntypedArmValue = 1;
    private const int TypedArmValue = 2;
    private const int SingleArmValue = 3;

    private static ClassDataSourceDescriptor CreateDescriptor(
        Type[] sourceTypes,
        DataSourceRowReaderDelegate?[] rowReaders) => new()
        {
            BaseId = "Tests.Echo",
            TestClass = typeof(Target),
            MethodName = nameof(Target.Echo),
            DataSourceTypes = sourceTypes,
            ParameterTypes = [typeof(int)],
            SharedType = SharedType.None,
            DataSourceRowReaders = rowReaders
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

    private sealed class SingleArmRows : IEnumerable<object[]>
    {
        public IEnumerator<object[]> GetEnumerator() => Rows().GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        private static IEnumerable<object[]> Rows()
        {
            yield return [SingleArmValue];
        }
    }
}
