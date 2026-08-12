using System.Globalization;
using NextUnit.Internal;

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
/// </remarks>
public sealed class CombinedDataSourceExpansionLimitTests
{
    [Fact]
    public void ExpandSingle_AboveTheDefaultLimit_Throws()
    {
        // 22^3 = 10648 combinations from 66 values.
        var descriptor = CreateDescriptor(parameterCount: 3, valuesPerParameter: 22);

        var exception = Assert.Throws<InvalidOperationException>(
            () => CombinedDataSourceExpander.ExpandSingle(descriptor).ToList());

        Assert.Contains("10648", exception.Message);
        Assert.Contains(
            TestCaseExpansionLimits.DefaultMaxTestCasesPerMethod.ToString(CultureInfo.InvariantCulture),
            exception.Message);

        // The message has to name the escape hatch, or the only way out of a failed discovery is to
        // delete the test.
        Assert.Contains(TestCaseExpansionLimits.EnvironmentVariableName, exception.Message);
    }

    [Fact]
    public void ExpandSingle_AboveTheDefaultLimit_DoesNotTruncate()
    {
        var descriptor = CreateDescriptor(parameterCount: 3, valuesPerParameter: 22);

        // Silently expanding the first 10000 would report a green run over a suite that never ran in
        // full, which is worse than the exhaustion the limit is here to prevent.
        Assert.Throws<InvalidOperationException>(
            () => CombinedDataSourceExpander.ExpandSingle(descriptor).First());
    }

    [Fact]
    public void ExpandSingle_WithinTheDefaultLimit_Expands()
    {
        var descriptor = CreateDescriptor(parameterCount: 3, valuesPerParameter: 21);

        Assert.Equal(21 * 21 * 21, CombinedDataSourceExpander.ExpandSingle(descriptor).Count());
    }

    [Fact]
    public void ExpandSingle_LazySourceLongerThanTheLimit_StopsDrawingAtTheCap()
    {
        var drawn = 0;
        var descriptor = CreateDescriptor(new ParameterDataSource
        {
            ParameterIndex = 0,
            ParameterName = "p0",
            Kind = ParameterDataSourceKind.Member,
            MemberProvider = () => Counted(200_000, () => drawn++),
        });

        var exception = Assert.Throws<InvalidOperationException>(
            () => CombinedDataSourceExpander.ExpandSingle(descriptor).ToList());

        // The sequence is drained before any product exists, so a cap that only guarded the product
        // would have pulled all 200000 values -- and a genuinely unbounded source would never stop.
        // The bound is the cap plus the one extra value drawn to tell "filled the cap" from
        // "ended exactly at it".
        var bound = TestCaseExpansionLimits.DefaultMaxTestCasesPerMethod + 2;
        Assert.True(drawn <= bound, $"Drew {drawn} values from a source bounded at {bound}.");

        // The real length was never learned, so the message reports a bound rather than a count.
        Assert.Contains("more than", exception.Message);
        Assert.False(
            exception.Message.Contains("200000", StringComparison.Ordinal),
            $"The message claims a count the expander never computed: {exception.Message}");
    }

    [Fact]
    public void ExpandSingle_ManyOversizedSources_DrawsNoMoreThanTheLimitInTotal()
    {
        const int sourceCount = 5;
        var drawn = 0;
        var descriptor = CreateDescriptor(Enumerable.Range(0, sourceCount)
            .Select(parameterIndex => new ParameterDataSource
            {
                ParameterIndex = parameterIndex,
                ParameterName = $"p{parameterIndex}",
                Kind = ParameterDataSourceKind.Member,
                MemberProvider = () => Counted(200_000, () => drawn++),
            })
            .ToArray());

        Assert.Throws<InvalidOperationException>(
            () => CombinedDataSourceExpander.ExpandSingle(descriptor).ToList());

        // A fixed per-source cap would allow limit+1 values per parameter, so adding parameters would
        // buy the exhaustion back. The cap shrinks as the running product grows: once the product is
        // over, the remaining sources are only probed for emptiness, so the total stays limit plus a
        // constant per source rather than limit times the source count.
        var bound = TestCaseExpansionLimits.DefaultMaxTestCasesPerMethod + (2 * sourceCount);
        Assert.True(
            drawn <= bound,
            $"Drew {drawn} values across {sourceCount} sources, bounded at {bound}.");
    }

    [Fact]
    public void ExpandSingle_EmptySourceBeforeAnOversizedOne_ExpandsToNothing()
    {
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
                MemberProvider = () => Counted(200_000, static () => { }),
            });

        // Order must not decide the outcome: a zero product is zero whichever source is read first.
        Assert.Empty(CombinedDataSourceExpander.ExpandSingle(descriptor));
    }

    [Fact]
    public void ExpandSingle_EmptySourceBesideAnOversizedOne_ExpandsToNothing()
    {
        var descriptor = CreateDescriptor(
            new ParameterDataSource
            {
                ParameterIndex = 0,
                ParameterName = "p0",
                Kind = ParameterDataSourceKind.Member,
                MemberProvider = () => Counted(200_000, static () => { }),
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
        Assert.Empty(CombinedDataSourceExpander.ExpandSingle(descriptor));
    }

    [Fact]
    public void ExpandSingle_EmptySource_ExpandsToNothing()
    {
        // An empty source short-circuits the projection, so it must still reach the normal
        // "no combinations" result rather than trip the limit.
        var descriptor = CreateDescriptor(parameterCount: 1, valuesPerParameter: 0);

        Assert.Empty(CombinedDataSourceExpander.ExpandSingle(descriptor));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("not-a-number")]
    [InlineData("2147483648")]
    public void Parse_UnusableValue_FallsBackToTheDefault(string? value)
    {
        Assert.Equal(TestCaseExpansionLimits.DefaultMaxTestCasesPerMethod, TestCaseExpansionLimits.Parse(value));
    }

    [Theory]
    [InlineData("1", 1)]
    [InlineData("50", 50)]
    [InlineData("2147483647", int.MaxValue)]
    public void Parse_PositiveValue_IsHonored(string value, int expected)
    {
        Assert.Equal(expected, TestCaseExpansionLimits.Parse(value));
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
