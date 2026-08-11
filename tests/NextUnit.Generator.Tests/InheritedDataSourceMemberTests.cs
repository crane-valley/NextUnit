using NextUnit.Internal;

namespace NextUnit.Generator.Tests;

/// <summary>
/// Covers the runtime reflection fallback over a data source declared on a base test class.
/// </summary>
/// <remarks>
/// The descriptors are built by hand with no provider, which is the shape the generator emits for
/// a member it declined to bind, so these exercise the reflection path rather than the emitted one.
/// The fallback used to stop at the declaring type, reporting a source C# resolves as
/// <c>Derived.Rows</c> as missing.
/// </remarks>
public sealed class InheritedDataSourceMemberTests
{
    [Fact]
    public void ExpandSingle_InheritedProperty_ResolvesThroughBaseChain()
    {
        var testCase = Assert.Single(Expand(nameof(RowsBase.PropertyRows)));

        Assert.Equal(new object?[] { 1, 2, 3 }, testCase.Arguments);
    }

    [Fact]
    public void ExpandSingle_InheritedMethod_ResolvesThroughBaseChain()
    {
        var testCase = Assert.Single(Expand(nameof(RowsBase.MethodRows)));

        Assert.Equal(new object?[] { 4, 5, 9 }, testCase.Arguments);
    }

    [Fact]
    public void ExpandSingle_InheritedField_ResolvesThroughBaseChain()
    {
        var testCase = Assert.Single(Expand(nameof(RowsBase.FieldRows)));

        Assert.Equal(new object?[] { 6, 7, 13 }, testCase.Arguments);
    }

    /// <summary>
    /// A member declared two levels up is still the one C# binds, so the walk cannot stop at the
    /// immediate base type.
    /// </summary>
    [Fact]
    public void ExpandSingle_MemberOnGrandparent_ResolvesThroughBaseChain()
    {
        var testCase = Assert.Single(Expand(nameof(RowsRoot.RootRows)));

        Assert.Equal(new object?[] { 2, 3, 5 }, testCase.Arguments);
    }

    /// <summary>
    /// The most-derived declaration wins, matching the precedence the compile-time resolver applies
    /// and the member C# itself would bind.
    /// </summary>
    [Fact]
    public void ExpandSingle_ShadowedMember_ResolvesMostDerived()
    {
        var testCase = Assert.Single(Expand(nameof(RowsDerived.ShadowedRows)));

        Assert.Equal(new object?[] { 8, 9, 17 }, testCase.Arguments);
    }

    /// <summary>
    /// The type the attribute names is what the walk starts from, so the base class on its own
    /// still resolves its own member rather than the shadowing one.
    /// </summary>
    [Fact]
    public void ExpandSingle_ShadowedMemberOnBaseType_ResolvesBaseDeclaration()
    {
        var descriptor = CreateDescriptor(nameof(RowsBase.ShadowedRows), typeof(RowsBase));

        var testCase = Assert.Single(
            TestDataExpander.ExpandSingle(descriptor, TestContext.Current.CancellationToken));

        Assert.Equal(new object?[] { 0, 1, 1 }, testCase.Arguments);
    }

    private static IEnumerable<TestCaseDescriptor> Expand(string dataSourceName) =>
        TestDataExpander.ExpandSingle(
            CreateDescriptor(dataSourceName, typeof(RowsDerived)),
            TestContext.Current.CancellationToken);

    private static TestDataDescriptor CreateDescriptor(string dataSourceName, Type dataSourceType) => new()
    {
        BaseId = "Tests.Add",
        TestClass = typeof(Target),
        MethodName = nameof(Target.Add),
        DataSourceName = dataSourceName,
        DataSourceType = dataSourceType,
        ParameterTypes = [typeof(int), typeof(int), typeof(int)]
    };

    private class RowsRoot
    {
        public static IEnumerable<object[]> RootRows => [[2, 3, 5]];
    }

    private class RowsBase : RowsRoot
    {
        public static readonly IEnumerable<object[]> FieldRows = new object[][] { [6, 7, 13] };

        public static IEnumerable<object[]> PropertyRows => [[1, 2, 3]];

        public static IEnumerable<object[]> ShadowedRows => [[0, 1, 1]];

        public static IEnumerable<object[]> MethodRows() => [[4, 5, 9]];
    }

    private sealed class RowsDerived : RowsBase
    {
        public static new IEnumerable<object[]> ShadowedRows => [[8, 9, 17]];
    }

    private sealed class Target
    {
        public void Add(int a, int b, int expected)
        {
        }
    }
}
