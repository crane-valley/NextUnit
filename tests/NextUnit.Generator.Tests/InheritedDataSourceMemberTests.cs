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

    /// <summary>
    /// The derived type declares the name, so it is the only level consulted, and the token-taking
    /// overload it declares is not something this synchronous path can invoke. The inherited
    /// parameterless member is not reached -- reaching it would answer with a member the nearest
    /// declaring level has taken over.
    /// </summary>
    [Fact]
    public void ExpandSingle_DerivedTokenOverload_ReportsNotFound()
    {
        var descriptor = CreateDescriptor(nameof(TokenBase.OverloadedRows), typeof(TokenDerived));

        var exception = Assert.Throws<InvalidOperationException>(
            () => TestDataExpander.ExpandSingle(descriptor, TestContext.Current.CancellationToken).ToList());

        Xunit.Assert.Contains("not found", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// A derived method takes the name over, so the base property it hides must not be read. The
    /// fallback searching kind by kind would run the test against data C# says the name does not
    /// refer to, which is worse than reporting the source as missing.
    /// </summary>
    [Fact]
    public void ExpandSingle_DerivedMethodHidingInheritedProperty_ReportsNotFound()
    {
        var descriptor = CreateDescriptor(nameof(HidingBase.HiddenRows), typeof(HidingDerived));

        var exception = Assert.Throws<InvalidOperationException>(
            () => TestDataExpander.ExpandSingle(descriptor, TestContext.Current.CancellationToken).ToList());

        Xunit.Assert.Contains("not found", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// A derived instance declaration hides the base static one it repeats, so the name is not a
    /// static reference at all. Reading the base member would run the test against data the name
    /// does not refer to -- the analyzer reports NU0003 for the same declaration.
    /// </summary>
    [Fact]
    public void ExpandSingle_DerivedInstanceMemberHidingBaseStatic_ReportsNotFound()
    {
        var descriptor = CreateDescriptor(nameof(HidingBase.InstanceHiddenRows), typeof(HidingDerived));

        var exception = Assert.Throws<InvalidOperationException>(
            () => TestDataExpander.ExpandSingle(descriptor, TestContext.Current.CancellationToken).ToList());

        Xunit.Assert.Contains("not found", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// C# binds a derived overload whose parameters it can fill in, so the inherited parameterless
    /// member is not what the name refers to. Reflection cannot supply the omitted arguments, so
    /// the lookup reports the source as missing rather than reading the member C# reduced away.
    /// </summary>
    [Fact]
    public void ExpandSingle_DerivedOptionalParameterOverload_ReportsNotFound()
    {
        var descriptor = CreateDescriptor(nameof(HidingBase.OptionalHiddenRows), typeof(HidingDerived));

        var exception = Assert.Throws<InvalidOperationException>(
            () => TestDataExpander.ExpandSingle(descriptor, TestContext.Current.CancellationToken).ToList());

        Xunit.Assert.Contains("not found", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// The same for a <c>params</c> overload, which C# binds in its expanded form with no elements.
    /// </summary>
    [Fact]
    public void ExpandSingle_DerivedParamsOverload_ReportsNotFound()
    {
        var descriptor = CreateDescriptor(nameof(HidingBase.ParamsHiddenRows), typeof(HidingDerived));

        var exception = Assert.Throws<InvalidOperationException>(
            () => TestDataExpander.ExpandSingle(descriptor, TestContext.Current.CancellationToken).ToList());

        Xunit.Assert.Contains("not found", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// A derived overload that requires an argument still claims the name for its level, so the
    /// inherited parameterless member is not reached. C# would fall back to it; the contract
    /// reports the source instead of resolving against a farther level.
    /// </summary>
    [Fact]
    public void ExpandSingle_DerivedRequiredParameterOverload_ReportsNotFound()
    {
        var descriptor = CreateDescriptor(nameof(HidingBase.RequiredArgRows), typeof(HidingDerived));

        var exception = Assert.Throws<InvalidOperationException>(
            () => TestDataExpander.ExpandSingle(descriptor, TestContext.Current.CancellationToken).ToList());

        Xunit.Assert.Contains("not found", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// The parameter-level fallback applies the same hiding, since it shares the lookup.
    /// </summary>
    [Fact]
    public void ExpandSingle_ParameterMemberHiddenByDerivedMethod_ReportsNotFound()
    {
        var descriptor = CreateParameterDescriptor(nameof(HidingBase.HiddenValues), typeof(HidingDerived));

        var exception = Assert.Throws<InvalidOperationException>(
            () => CombinedDataSourceExpander.ExpandSingle(descriptor, registryMaxTestCasesPerMethod: null).ToList());

        // The expander wraps the lookup failure with the parameter it was resolving, so the
        // member-level reason is the inner exception.
        Xunit.Assert.Contains(
            "not found",
            exception.InnerException?.Message ?? exception.Message,
            StringComparison.Ordinal);
    }

    /// <summary>
    /// The parameter-level fallback reaches the base chain too. It is a separate lookup from the
    /// <c>[TestData]</c> one, so it needs its own coverage.
    /// </summary>
    [Fact]
    public void ExpandSingle_InheritedParameterMember_ResolvesThroughBaseChain()
    {
        var descriptor = CreateParameterDescriptor(nameof(RowsBase.ParameterValues), typeof(RowsDerived));

        var testCases = CombinedDataSourceExpander.ExpandSingle(descriptor, registryMaxTestCasesPerMethod: null).ToList();

        Assert.Equal(
            new object?[] { 11, 12 },
            testCases.Select(static testCase => testCase.Arguments![0]).ToArray());
    }

    private static CombinedDataSourceDescriptor CreateParameterDescriptor(string memberName, Type memberType) => new()
    {
        BaseId = "Tests.Single",
        TestClass = typeof(Target),
        MethodName = nameof(Target.Single),
        ParameterTypes = [typeof(int)],
        ParameterSources =
        [
            new ParameterDataSource
            {
                ParameterIndex = 0,
                ParameterName = "value",
                Kind = ParameterDataSourceKind.Member,
                MemberName = memberName,
                MemberType = memberType
            }
        ]
    };

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

        public static IEnumerable<int> ParameterValues => [11, 12];

        public static IEnumerable<object[]> MethodRows() => [[4, 5, 9]];
    }

    private sealed class RowsDerived : RowsBase
    {
        public static new IEnumerable<object[]> ShadowedRows => [[8, 9, 17]];
    }

    private class HidingBase
    {
        public static IEnumerable<object[]> HiddenRows => [[5, 5, 10]];

        public static IEnumerable<int> HiddenValues => [21, 22];

        public static IEnumerable<object[]> InstanceHiddenRows() => [[7, 7, 14]];

        public static IEnumerable<object[]> OptionalHiddenRows() => [[1, 1, 2]];

        public static IEnumerable<object[]> ParamsHiddenRows() => [[2, 2, 4]];

        public static IEnumerable<object[]> RequiredArgRows() => [[4, 4, 8]];
    }

    private sealed class HidingDerived : HidingBase
    {
        public static new IEnumerable<object[]> HiddenRows(int count) => [[count, count, count * 2]];

        public static new IEnumerable<int> HiddenValues(int count) => [count];

        public new IEnumerable<object[]> InstanceHiddenRows() => [[8, 8, 16]];

        public static IEnumerable<object[]> OptionalHiddenRows(int count = 1) => [[count, count, count * 2]];

        public static IEnumerable<object[]> ParamsHiddenRows(params int[] counts) => [[3, 3, 6]];

        public static IEnumerable<object[]> RequiredArgRows(int count) => [[count, count, count * 2]];
    }

    private class TokenBase
    {
        public static IEnumerable<object[]> OverloadedRows() => [[3, 4, 7]];
    }

    private sealed class TokenDerived : TokenBase
    {
        public static IEnumerable<object[]> OverloadedRows(CancellationToken cancellationToken) => [[9, 9, 18]];
    }

    private sealed class Target
    {
        public void Add(int a, int b, int expected)
        {
        }

        public void Single(int value)
        {
        }
    }
}
