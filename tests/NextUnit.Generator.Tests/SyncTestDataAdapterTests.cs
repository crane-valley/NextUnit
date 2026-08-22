using System.Collections;
using NextUnit.Internal;

namespace NextUnit.Generator.Tests;

/// <summary>
/// Covers the runtime half of a synchronous <c>[TestData]</c> source that offers more than one row
/// type: the descriptors here are built by hand exactly as the generator emits them, so the adapter
/// and the expander are exercised without going through a full generator run.
/// </summary>
public sealed class SyncTestDataAdapterTests
{
    /// <summary>
    /// The arm named at the call site is the arm the run reads, which is the point of the adapter.
    /// </summary>
    [Fact]
    public void Expand_SourceWithTwoArms_ReadsTheNamedArm()
    {
        var descriptor = CreateDescriptor(
            static () => DataSourceAdapter.FromEnumerable<TestDataRow<int>>(new DualRows()));

        var testCases = TestDataExpander
            .ExpandSingle(descriptor, TestContext.Current.CancellationToken)
            .ToArray();

        var testCase = Assert.Single(testCases);
        Assert.Equal(new object?[] { TypedArmValue }, testCase.Arguments);
    }

    /// <summary>
    /// The same source read the way the provider used to hand it over, pinning what the adapter
    /// changes: the runtime holds the value as <c>object</c> and reads it back as a non-generic
    /// <c>IEnumerable</c>, which dispatches to whatever the source type mapped that interface to --
    /// here the arm nothing validated.
    /// </summary>
    [Fact]
    public void Expand_SourceWithTwoArms_ReadUntyped_ReadsTheNonGenericArm()
    {
        var descriptor = CreateDescriptor(static () => (object?)new DualRows());

        var testCases = TestDataExpander
            .ExpandSingle(descriptor, TestContext.Current.CancellationToken)
            .ToArray();

        var testCase = Assert.Single(testCases);
        Assert.Equal(new object?[] { UntypedArmValue }, testCase.Arguments);
    }

    /// <summary>
    /// A member that returned null still reaches the expander as null, so the reflection fallback
    /// and the message naming the missing member are the ones the run gets -- not a complaint about
    /// a parameter of a method the user never called.
    /// </summary>
    [Fact]
    public void Expand_NullSource_ReportsTheMemberAsMissing()
    {
        Assert.Null(DataSourceAdapter.FromEnumerable<object[]>(null));

        var descriptor = CreateDescriptor(static () => DataSourceAdapter.FromEnumerable<object[]>(null));

        var exception = Assert.Throws<InvalidOperationException>(
            () => TestDataExpander.ExpandSingle(descriptor, TestContext.Current.CancellationToken).ToArray());

        Xunit.Assert.Contains("not found", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// The wrapper stays lazy. Deferred enumeration and the per-row cancellation checks both depend
    /// on the source being read one row at a time, after the provider has already returned.
    /// </summary>
    [Fact]
    public void FromEnumerable_DoesNotReadTheSourceUntilItIsEnumerated()
    {
        var source = new CountingRows();

        var rows = DataSourceAdapter.FromEnumerable<object[]>(source);

        Assert.NotNull(rows);
        Assert.Equal(0, source.EnumeratorRequests);

        using var enumerator = rows!.GetEnumerator();
        Assert.True(enumerator.MoveNext());
        Assert.Equal(1, source.EnumeratorRequests);
    }

    private const int UntypedArmValue = 1;
    private const int TypedArmValue = 2;

    private static TestDataDescriptor CreateDescriptor(DataSourceProviderDelegate provider) => new()
    {
        BaseId = "Tests.Echo",
        TestClass = typeof(Target),
        MethodName = nameof(Target.Echo),
        DataSourceName = "Rows",
        DataSourceType = typeof(SyncTestDataAdapterTests),
        ParameterTypes = [typeof(int)],
        DataSourceProvider = provider
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

    /// <summary>
    /// Counts the enumerators handed out, so a wrapper that read the source eagerly would show up.
    /// </summary>
    private sealed class CountingRows : IEnumerable<object[]>
    {
        public int EnumeratorRequests { get; private set; }

        public IEnumerator<object[]> GetEnumerator()
        {
            EnumeratorRequests++;
            return Rows().GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        private static IEnumerable<object[]> Rows()
        {
            yield return [UntypedArmValue];
        }
    }
}
