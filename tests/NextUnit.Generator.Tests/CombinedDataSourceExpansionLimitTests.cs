using System.Globalization;
using NextUnit.Internal;
using NextUnit.Shared;

namespace NextUnit.Generator.Tests;

/// <summary>
/// Pins the cap the combined data source expander applies at discovery time.
/// </summary>
/// <remarks>
/// Parameter-level data sources resolve their values while the host is starting, so the generator
/// cannot see this product at compile time and the cap has to be enforced again here. The over-limit
/// cases are written with short sources whose product is large, so a cap that ran after the product
/// was built would exhaust the runner instead of failing an assertion.
/// <para>
/// Nothing here mutates <c>NEXTUNIT_MAX_TEST_CASES_PER_METHOD</c>. The variable is process-wide, and
/// other test classes expand combined descriptors concurrently, so lowering it for one test would
/// reach into theirs. The parsing is covered directly instead.
/// </para>
/// <para>
/// Every size below is derived from <see cref="_limit"/>, the cap actually in effect, rather than
/// written against the default. A developer who exports the variable would otherwise get failures
/// from a suite that is testing the thing they configured, or -- worse -- an over-limit case that
/// quietly stops being one and asserts nothing.
/// </para>
/// </remarks>
public sealed class CombinedDataSourceExpansionLimitTests
{
    /// <summary>
    /// The highest configured cap these tests can still exercise by actually expanding.
    /// </summary>
    /// <remarks>
    /// Sizes derived from a cap in the millions would have the assertions themselves materialize
    /// what the cap exists to prevent -- a cap of <see cref="int.MaxValue"/> asks the within-limit
    /// case for two billion test cases. Past this the tests skip loudly rather than hang, which is
    /// the honest report: the mechanism is unchanged, this suite just cannot afford to demonstrate
    /// it at that setting.
    /// </remarks>
    private const int MaxExercisableLimit = 1_000_000;

    private static readonly int _limit = TestCaseExpansionLimits.ResolveFromEnvironment(registryBaseline: null);

    /// <summary>
    /// The smallest per-parameter length whose cube exceeds <see cref="_limit"/>, so three sources of
    /// it are over the cap and three of one less are under it.
    /// </summary>
    private static readonly int _overLimitCubeRoot = SmallestCubeRootAbove(_limit);

    /// <summary>
    /// A lazy source length comfortably past the cap, so truncation is provable at any limit.
    /// </summary>
    /// <remarks>
    /// Saturating rather than wrapping, because <c>Parse</c> accepts <see cref="int.MaxValue"/> as a
    /// limit: a wrapped length is negative, <c>Take</c> of it yields nothing, and every over-limit
    /// test here would pass while asserting the opposite of what it claims.
    /// </remarks>
    private static readonly int _oversizedSourceLength =
        _limit > int.MaxValue - 1_000 ? int.MaxValue : _limit + 1_000;

    [Fact]
    public void ExpandSingle_AboveTheLimit_Throws()
    {
        Assert.SkipWhen(
            _limit > MaxExercisableLimit,
            $"A configured cap of {_limit} is larger than this suite can expand.");

        var descriptor = CreateDescriptor(parameterCount: 3, valuesPerParameter: _overLimitCubeRoot);

        var exception = Assert.Throws<InvalidOperationException>(
            () => CombinedDataSourceExpander.ExpandSingle(descriptor, registryMaxTestCasesPerMethod: null).ToList());

        var expected = (long)_overLimitCubeRoot * _overLimitCubeRoot * _overLimitCubeRoot;
        Assert.Contains(expected.ToString(CultureInfo.InvariantCulture), exception.Message);
        Assert.Contains(_limit.ToString(CultureInfo.InvariantCulture), exception.Message);

        // The message has to name the escape hatch, or the only way out of a failed discovery is to
        // delete the test.
        Assert.Contains(TestCaseExpansionLimits.EnvironmentVariableName, exception.Message);
    }

    [Fact]
    public void ExpandSingle_AboveTheLimit_DoesNotTruncate()
    {
        Assert.SkipWhen(
            _limit > MaxExercisableLimit,
            $"A configured cap of {_limit} is larger than this suite can expand.");

        var descriptor = CreateDescriptor(parameterCount: 3, valuesPerParameter: _overLimitCubeRoot);

        // Silently expanding the first N would report a green run over a suite that never ran in
        // full, which is worse than the exhaustion the limit is here to prevent.
        Assert.Throws<InvalidOperationException>(
            () => CombinedDataSourceExpander.ExpandSingle(descriptor, registryMaxTestCasesPerMethod: null).First());
    }

    [Fact]
    public void ExpandSingle_WithinTheLimit_Expands()
    {
        Assert.SkipWhen(
            _limit > MaxExercisableLimit,
            $"A configured cap of {_limit} is larger than this suite can expand.");

        var perParameter = _overLimitCubeRoot - 1;
        var descriptor = CreateDescriptor(parameterCount: 3, valuesPerParameter: perParameter);

        Assert.Equal(
            perParameter * perParameter * perParameter,
            CombinedDataSourceExpander.ExpandSingle(descriptor, registryMaxTestCasesPerMethod: null).Count());
    }

    [Fact]
    public void ExpandSingle_LazySourceLongerThanTheLimit_StopsDrawingAtTheCap()
    {
        Assert.SkipWhen(
            _limit > MaxExercisableLimit,
            $"A configured cap of {_limit} is larger than this suite can expand.");

        var drawn = 0;
        var descriptor = CreateDescriptor(new ParameterDataSource
        {
            ParameterIndex = 0,
            ParameterName = "p0",
            Kind = ParameterDataSourceKind.Member,
            MemberProvider = () => Counted(_oversizedSourceLength, () => drawn++),
        });

        var exception = Assert.Throws<InvalidOperationException>(
            () => CombinedDataSourceExpander.ExpandSingle(descriptor, registryMaxTestCasesPerMethod: null).ToList());

        // The sequence is drained before any product exists, so a cap that only guarded the product
        // would have pulled every value -- and a genuinely unbounded source would never stop. The
        // bound is the cap plus the one extra value drawn to tell "filled the cap" from "ended
        // exactly at it".
        var bound = (long)_limit + 2;
        Assert.True(drawn <= bound, $"Drew {drawn} values from a source bounded at {bound}.");

        // The real length was never learned, so the message reports a bound rather than a count.
        Assert.Contains("more than", exception.Message);
        Assert.False(
            exception.Message.Contains(
                _oversizedSourceLength.ToString(CultureInfo.InvariantCulture),
                StringComparison.Ordinal),
            $"The message claims a count the expander never computed: {exception.Message}");
    }

    [Fact]
    public void ExpandSingle_ManyOversizedSources_DrawsNoMoreThanTheLimitInTotal()
    {
        Assert.SkipWhen(
            _limit > MaxExercisableLimit,
            $"A configured cap of {_limit} is larger than this suite can expand.");

        const int sourceCount = 5;
        var drawn = 0;
        var descriptor = CreateDescriptor(Enumerable.Range(0, sourceCount)
            .Select(parameterIndex => new ParameterDataSource
            {
                ParameterIndex = parameterIndex,
                ParameterName = $"p{parameterIndex}",
                Kind = ParameterDataSourceKind.Member,
                MemberProvider = () => Counted(_oversizedSourceLength, () => drawn++),
            })
            .ToArray());

        Assert.Throws<InvalidOperationException>(
            () => CombinedDataSourceExpander.ExpandSingle(descriptor, registryMaxTestCasesPerMethod: null).ToList());

        // A fixed per-source cap would allow limit+1 values per parameter, so adding parameters would
        // buy the exhaustion back. The cap shrinks as the running product grows: once the product is
        // over, the remaining sources are only probed for emptiness, so the total stays limit plus a
        // constant per source rather than limit times the source count.
        var bound = (long)_limit + (2 * sourceCount);
        Assert.True(
            drawn <= bound,
            $"Drew {drawn} values across {sourceCount} sources, bounded at {bound}.");
    }

    [Fact]
    public void ExpandSingle_EmptySourceBeforeAnOversizedOne_ExpandsToNothing()
    {
        Assert.SkipWhen(
            _limit > MaxExercisableLimit,
            $"A configured cap of {_limit} is larger than this suite can expand.");

        var descriptor = CreateDescriptor(
            new ParameterDataSource
            {
                ParameterIndex = 0,
                ParameterName = "p0",
                Kind = ParameterDataSourceKind.Inline,
                InlineValues = [],
            },
            new ParameterDataSource
            {
                ParameterIndex = 1,
                ParameterName = "p1",
                Kind = ParameterDataSourceKind.Member,
                MemberProvider = () => Counted(_oversizedSourceLength, static () => { }),
            });

        // Order must not decide the outcome: a zero product is zero whichever source is read first.
        Assert.Empty(CombinedDataSourceExpander.ExpandSingle(descriptor, registryMaxTestCasesPerMethod: null));
    }

    [Fact]
    public void ExpandSingle_EmptySourceBesideAnOversizedOne_ExpandsToNothing()
    {
        Assert.SkipWhen(
            _limit > MaxExercisableLimit,
            $"A configured cap of {_limit} is larger than this suite can expand.");

        var descriptor = CreateDescriptor(
            new ParameterDataSource
            {
                ParameterIndex = 0,
                ParameterName = "p0",
                Kind = ParameterDataSourceKind.Member,
                MemberProvider = () => Counted(_oversizedSourceLength, static () => { }),
            },
            new ParameterDataSource
            {
                ParameterIndex = 1,
                ParameterName = "p1",
                Kind = ParameterDataSourceKind.Inline,
                InlineValues = [],
            });

        // The product is zero whichever source is read first, and it was zero before the limit
        // existed, so an oversized sibling must not turn "no test cases" into a failed discovery.
        Assert.Empty(CombinedDataSourceExpander.ExpandSingle(descriptor, registryMaxTestCasesPerMethod: null));
    }

    [Fact]
    public void ExpandSingle_EmptySource_ExpandsToNothing()
    {
        // An empty source short-circuits the projection, so it must still reach the normal
        // "no combinations" result rather than trip the limit.
        var descriptor = CreateDescriptor(parameterCount: 1, valuesPerParameter: 0);

        Assert.Empty(CombinedDataSourceExpander.ExpandSingle(descriptor, registryMaxTestCasesPerMethod: null));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void TryResolve_UnsetOverride_UsesTheDefault(string? value)
    {
        // Blank has to read as unset, not as a typo: MSBuild writes every CompilerVisibleProperty
        // into the generated analyzer config whether or not the project defines it, so the generator
        // is handed an empty value by every consumer that never touched the escape hatch.
        Assert.True(TestCaseExpansionPolicy.TryResolve(value, out var cap));
        Assert.Equal(TestCaseExpansionPolicy.DefaultMaxTestCasesPerMethod, cap);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("not-a-number")]
    [InlineData("100O")]
    [InlineData("2147483648")]
    public void TryResolve_UnusableOverride_IsRefused(string value)
    {
        // "100O" is the case the refusal exists for: written for 1000, it used to be discarded, and
        // discarding it granted the 10000 default -- ten times the bound the user was tightening to.
        Assert.False(TestCaseExpansionPolicy.TryResolve(value, out var cap));
        Assert.Equal(TestCaseExpansionPolicy.DefaultMaxTestCasesPerMethod, cap);
    }

    [Theory]
    [InlineData("1", 1)]
    [InlineData("50", 50)]
    [InlineData("2147483647", int.MaxValue)]
    public void TryResolve_PositiveOverride_IsHonored(string value, int expected)
    {
        Assert.True(TestCaseExpansionPolicy.TryResolve(value, out var cap));
        Assert.Equal(expected, cap);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Resolve_UnsetEnvironmentValue_UsesTheDefault(string? value)
    {
        Assert.Equal(TestCaseExpansionPolicy.DefaultMaxTestCasesPerMethod, TestCaseExpansionLimits.Resolve(value, registryBaseline: null));
    }

    [Fact]
    public void Resolve_UsableEnvironmentValue_IsHonored()
    {
        Assert.Equal(50, TestCaseExpansionLimits.Resolve("50", registryBaseline: null));
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("100O")]
    public void Resolve_UnusableEnvironmentValue_Throws(string value)
    {
        // The discovery-time half of the same decision the generator makes with NEXTUNIT014: a
        // mistyped bound stops the run instead of quietly widening it to the default.
        var exception = Assert.Throws<InvalidOperationException>(() => TestCaseExpansionLimits.Resolve(value, registryBaseline: null));

        Assert.Contains(TestCaseExpansionLimits.EnvironmentVariableName, exception.Message);
        Assert.Contains(value, exception.Message);
    }

    /// <summary>
    /// The smallest length whose cube exceeds <paramref name="limit"/>.
    /// </summary>
    /// <remarks>
    /// Stepped rather than computed from <c>Math.Cbrt</c> alone: the cube root of a large limit lands
    /// close enough to an integer that a rounding error either side picks a length whose cube is on
    /// the wrong side of the cap, and every over-limit test here would then assert nothing.
    /// </remarks>
    private static int SmallestCubeRootAbove(int limit)
    {
        var candidate = Math.Max((int)Math.Cbrt(limit) - 1, 1);

        while ((long)candidate * candidate * candidate <= limit)
        {
            candidate++;
        }

        return candidate;
    }

    /// <summary>
    /// A lazy sequence that reports each value as it is drawn, so a test can assert how far a source
    /// was drained rather than only that it eventually failed.
    /// </summary>
    private static IEnumerable<object?> Counted(int length, Action onDraw)
    {
        for (var index = 0; index < length; index++)
        {
            onDraw();
            yield return index;
        }
    }

    private static CombinedDataSourceDescriptor CreateDescriptor(int parameterCount, int valuesPerParameter) =>
        CreateDescriptor(Enumerable.Range(0, parameterCount)
            .Select(parameterIndex => new ParameterDataSource
            {
                ParameterIndex = parameterIndex,
                ParameterName = $"p{parameterIndex}",
                Kind = ParameterDataSourceKind.Inline,
                InlineValues = Enumerable.Range(0, valuesPerParameter).Cast<object?>().ToArray(),
            })
            .ToArray());

    private static CombinedDataSourceDescriptor CreateDescriptor(params ParameterDataSource[] sources) =>
        new()
        {
            BaseId = "Tests.ExpansionLimit.Combined",
            TestClass = typeof(ExpansionTarget),
            MethodName = nameof(ExpansionTarget.Run),
            ParameterTypes = Enumerable.Repeat(typeof(int), sources.Length).ToArray(),
            ParameterSources = sources,
        };

    private sealed class ExpansionTarget
    {
        public void Run(int first, int second, int third) => _ = first + second + third;

        public void Run(int first, int second) => _ = first + second;

        public void Run(int only) => _ = only;
    }
}
