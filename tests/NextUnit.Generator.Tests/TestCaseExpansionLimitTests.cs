using System.Globalization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using NextUnit.Internal;

namespace NextUnit.Generator.Tests;

/// <summary>
/// Pins the cap on how many test cases one test method may expand into.
/// </summary>
/// <remarks>
/// Every over-limit case here is written so the generator has to reject it from the projected count
/// alone. If the projection were computed by expanding first, or in <c>int</c> arithmetic, these
/// tests would not fail with a wrong assertion -- they would hang or exhaust the runner, which is
/// exactly the behavior the limit exists to remove.
/// </remarks>
public class TestCaseExpansionLimitTests
{
    private const string ExpansionLimitId = "NEXTUNIT013";
    private const string OverrideUnusableId = "NEXTUNIT014";

    [Fact]
    public async Task Repeat_AboveTheDefaultLimit_ReportsAsync()
    {
        var source = """
            using NextUnit;

            namespace TestProject;

            public class RepeatTests
            {
                [Test]
                [Repeat(10001)]
                public void Repeated()
                {
                }
            }
            """;

        await VerifyAsync(source, expectExpansionLimitDiagnostic: true);
    }

    [Fact]
    public async Task Repeat_AtTheDefaultLimit_ReportsNothingAsync()
    {
        // 10000 emitted cases is the boundary the default admits, so it is asserted through the
        // projection rather than by compiling the emitted registry: the point is which side of the
        // limit the count lands on, not that the compiler can chew through 10000 descriptors.
        var source = """
            using NextUnit;

            namespace TestProject;

            public class RepeatTests
            {
                [Test]
                [Repeat(4)]
                public void Repeated()
                {
                }
            }
            """;

        await VerifyAsync(source, expectExpansionLimitDiagnostic: false, configuredLimit: "4");
    }

    [Fact]
    public async Task ArgumentsTimesRepeat_AboveTheDefaultLimit_ReportsAsync()
    {
        // Neither factor alone exceeds the limit: only the product does, which is what the emitter
        // actually writes out.
        var source = """
            using NextUnit;

            namespace TestProject;

            public class ArgumentTests
            {
                [Test]
                [Arguments(1)]
                [Arguments(2)]
                [Arguments(3)]
                [Repeat(4000)]
                public void Repeated(int value)
                {
                }
            }
            """;

        await VerifyAsync(source, expectExpansionLimitDiagnostic: true);
    }

    [Fact]
    public async Task ArgumentsTimesRepeat_OverflowingIntArithmetic_ReportsAsync()
    {
        // 2 x int.MaxValue wraps to -2 in int arithmetic, which is under any limit. Computed in long
        // it is 4294967294, and the emitter would run for the rest of the machine's life.
        var source = """
            using NextUnit;

            namespace TestProject;

            public class OverflowTests
            {
                [Test]
                [Arguments(1)]
                [Arguments(2)]
                [Repeat(2147483647)]
                public void Repeated(int value)
                {
                }
            }
            """;

        await VerifyAsync(source, expectExpansionLimitDiagnostic: true);
    }

    [Fact]
    public async Task Matrix_AboveTheDefaultLimit_ReportsAsync()
    {
        // 11^4 = 14641 combinations from four short attributes.
        var source = MatrixSource(parameterCount: 4, valuesPerParameter: 11);

        await VerifyAsync(source, expectExpansionLimitDiagnostic: true);
    }

    [Fact]
    public async Task Matrix_OverflowingIntArithmetic_ReportsAsync()
    {
        // 256^4 is exactly 2^32, which is 0 in int arithmetic -- the one product that would slip past
        // a wrapped check no matter how low the limit is set.
        var source = MatrixSource(parameterCount: 4, valuesPerParameter: 256);

        await VerifyAsync(source, expectExpansionLimitDiagnostic: true);
    }

    [Fact]
    public async Task Matrix_OversizedBeforeAnEmptyOne_ReportsAsync()
    {
        // MatrixHelper.ComputeCartesianProduct multiplies the running product parameter by parameter
        // and materializes it at every step, so the empty parameter zeroes the result only after the
        // 14641 combinations before it have been allocated. Charging the final product would read
        // that as zero test cases and wave the expansion through -- with 256 values per parameter
        // instead of 11, that is 2^32 combinations built before the zero arrives.
        var source = """
            using NextUnit;

            namespace TestProject;

            public class MatrixTests
            {
                [Test]
                public void Combined(
                    [Matrix(0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10)] int p0,
                    [Matrix(0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10)] int p1,
                    [Matrix(0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10)] int p2,
                    [Matrix(0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10)] int p3,
                    [Matrix()] int p4)
                {
                }
            }
            """;

        await VerifyAsync(source, expectExpansionLimitDiagnostic: true);
    }

    [Fact]
    public async Task Matrix_EmptyBeforeAnOversizedOne_ReportsNothingAsync()
    {
        // The mirror image: the zero arrives first, so every later step multiplies a product that is
        // already empty and the emitter allocates nothing. Charging each parameter at least one value
        // would reject this, which is why the projection tracks the running peak instead.
        var source = """
            using NextUnit;

            namespace TestProject;

            public class MatrixTests
            {
                [Test]
                public void Combined(
                    [Matrix()] int p0,
                    [Matrix(0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10)] int p1,
                    [Matrix(0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10)] int p2,
                    [Matrix(0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10)] int p3,
                    [Matrix(0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10)] int p4)
                {
                }
            }
            """;

        await VerifyAsync(source, expectExpansionLimitDiagnostic: false);
    }

    [Fact]
    public async Task MatrixExclusionTimesRepeat_UnderTheLimitAfterExclusion_ReportsNothingAsync()
    {
        // The emitter excludes first and repeats the survivors, so this is one survivor repeated
        // twice: two emitted cases, under a cap of three. Charging the pre-exclusion count for the
        // repeat as well makes it four and rejects a test that excludes its way under the cap.
        var source = """
            using NextUnit;

            namespace TestProject;

            public class MatrixTests
            {
                [Test]
                [MatrixExclusion(2)]
                [Repeat(2)]
                public void Combined([Matrix(1, 2)] int p0)
                {
                }
            }
            """;

        await VerifyAsync(source, expectExpansionLimitDiagnostic: false, configuredLimit: "3");
    }

    [Fact]
    public async Task MatrixExclusionTimesRepeat_OverTheLimitAfterExclusion_ReportsAsync()
    {
        // The same shape, but the exclusion names a value no parameter offers, so it removes nothing
        // and both combinations survive: four emitted cases against a cap of three. Subtracting an
        // exclusion that never matches would be a way to slip past the cap.
        var source = """
            using NextUnit;

            namespace TestProject;

            public class MatrixTests
            {
                [Test]
                [MatrixExclusion(3)]
                [Repeat(2)]
                public void Combined([Matrix(1, 2)] int p0)
                {
                }
            }
            """;

        await VerifyAsync(source, expectExpansionLimitDiagnostic: true, configuredLimit: "3");
    }

    [Fact]
    public async Task ExpansionLimit_SuppressedInEditorConfig_StillReportsAsync()
    {
        // Reporting is only half of what an over-limit method gets: it is also dropped from the
        // registry, because leaving it in would run the expansion the cap exists to prevent. If the
        // rule could be switched off, the build would go green while silently omitting those tests,
        // which is the shortened suite this whole feature is written to avoid. NEXTUNIT013 carries
        // NotConfigurable so severity = none cannot reach it.
        var source = MatrixSource(parameterCount: 4, valuesPerParameter: 11);

        var test = new CSharpSourceGeneratorVerifier<NextUnitGenerator>.Test
        {
            TestCode = source,
            TestBehaviors = TestBehaviors.SkipGeneratedSourcesCheck,
        };

        test.TestState.AnalyzerConfigFiles.Add((
            "/.globalconfig",
            """
            is_global = true
            dotnet_diagnostic.NEXTUNIT013.severity = none
            """));

        test.ExpectedDiagnostics.Add(new DiagnosticResult(ExpansionLimitId, DiagnosticSeverity.Error));

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Matrix_SaturatingTheProjection_ReportsABoundRatherThanACountAsync()
    {
        // 2^63 is past long.MaxValue, so MultiplyClamped saturates and the projection stops being a
        // count of anything. Printing long.MaxValue as the number of test cases would claim a figure
        // the generator never computed, so the message reports a bound instead -- the same wording
        // discovery uses when it truncates a source before learning its length.
        var source = MatrixSource(parameterCount: 63, valuesPerParameter: 2);

        await VerifyAsync(
            source,
            expectExpansionLimitDiagnostic: true,
            expectedMessage: "Test 'TestProject.MatrixTests.Combined' expands to more than 10000 test cases, " +
                "which exceeds the limit of 10000. Reduce the [Matrix], [Arguments], [Repeat], or [Values] " +
                "values, or raise the limit with <NextUnitMaxTestCasesPerMethod> in the project file.");
    }

    [Fact]
    public async Task Matrix_WithinTheDefaultLimit_ReportsNothingAsync()
    {
        var source = MatrixSource(parameterCount: 2, valuesPerParameter: 3);

        await VerifyAsync(source, expectExpansionLimitDiagnostic: false);
    }

    [Fact]
    public async Task Matrix_AboveAConfiguredLimit_ReportsAsync()
    {
        // 3^2 = 9 is far under the default, so only the configured value can reject it.
        var source = MatrixSource(parameterCount: 2, valuesPerParameter: 3);

        await VerifyAsync(source, expectExpansionLimitDiagnostic: true, configuredLimit: "8");
    }

    [Fact]
    public async Task Matrix_AtAConfiguredLimit_ReportsNothingAsync()
    {
        var source = MatrixSource(parameterCount: 2, valuesPerParameter: 3);

        await VerifyAsync(source, expectExpansionLimitDiagnostic: false, configuredLimit: "9");
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("not-a-number")]
    [InlineData("100O")]
    [InlineData("2147483648")]
    public async Task ConfiguredLimit_Unusable_ReportsAsync(string configuredLimit)
    {
        // Nine cases are far under any cap, so nothing here is over-limit: the only thing that can
        // fail this build is the override itself, which is the point. Falling back would have let
        // "100O" -- written for 1000 -- grant the 10000 default instead of tightening anything.
        var source = MatrixSource(parameterCount: 2, valuesPerParameter: 3);

        await VerifyAsync(
            source,
            expectExpansionLimitDiagnostic: false,
            configuredLimit: configuredLimit,
            expectedUnusableOverride: configuredLimit);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ConfiguredLimit_Blank_UsesTheDefaultSilentlyAsync(string configuredLimit)
    {
        // Blank is how MSBuild spells "unset": it writes every CompilerVisibleProperty into the
        // generated analyzer config whether or not the project defines the property, so reporting
        // blank would fail the build of every consumer that never touched the escape hatch.
        var source = MatrixSource(parameterCount: 2, valuesPerParameter: 3);

        await VerifyAsync(source, expectExpansionLimitDiagnostic: false, configuredLimit: configuredLimit);
    }

    [Fact]
    public async Task ConfiguredLimit_Unusable_StillAppliesTheDefaultAsync()
    {
        // The registry is still emitted under the default cap after NEXTUNIT014, so an over-limit
        // method is reported by name rather than buried under a CS0246 for every symbol a withheld
        // registry would have failed to declare.
        var source = """
            using NextUnit;

            namespace TestProject;

            public class RepeatTests
            {
                [Test]
                [Repeat(10001)]
                public void Repeated()
                {
                }
            }
            """;

        await VerifyAsync(
            source,
            expectExpansionLimitDiagnostic: true,
            configuredLimit: "0",
            expectedUnusableOverride: "0");
    }

    [Fact]
    public async Task ConfiguredLimit_Unusable_ReportsTheValueAndTheDefaultAsync()
    {
        var source = MatrixSource(parameterCount: 2, valuesPerParameter: 3);

        await VerifyAsync(
            source,
            expectExpansionLimitDiagnostic: false,
            configuredLimit: "100O",
            expectedUnusableOverride: "100O",
            expectedOverrideMessage: "The <NextUnitMaxTestCasesPerMethod> value '100O' is not a positive 32-bit " +
                "integer. Set it to a value between 1 and 2147483647, or remove the property to use the default " +
                "limit of 10000.");
    }

    [Fact]
    public async Task OverrideUnusable_SuppressedInEditorConfig_StillReportsAsync()
    {
        // Suppressing NEXTUNIT014 would restore exactly the behavior it replaces -- the unusable
        // value discarded, the default applied, nothing said -- so it carries NotConfigurable and
        // severity = none cannot reach it.
        var source = MatrixSource(parameterCount: 2, valuesPerParameter: 3);

        var test = new CSharpSourceGeneratorVerifier<NextUnitGenerator>.Test
        {
            TestCode = source,
            TestBehaviors = TestBehaviors.SkipGeneratedSourcesCheck,
        };

        test.TestState.AnalyzerConfigFiles.Add((
            "/.globalconfig",
            """
            is_global = true
            build_property.NextUnitMaxTestCasesPerMethod = 100O
            dotnet_diagnostic.NEXTUNIT014.severity = none
            """));

        test.ExpectedDiagnostics.Add(new DiagnosticResult(OverrideUnusableId, DiagnosticSeverity.Error));

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task BothOverrides_AreValidatedWhereEachIsReadAsync()
    {
        // The precedence rule, in executable form: neither override wins over the other, because the
        // two are never read by the same component. The MSBuild property is the compile-time cap and
        // only the generator sees it; NEXTUNIT_MAX_TEST_CASES_PER_METHOD is the discovery-time cap
        // and only the test host sees it, since EnforceExtendedAnalyzerRules bans environment access
        // from the analyzer assembly. So a usable property does not rescue an unusable environment
        // value, and a usable environment value does not rescue an unusable property.
        var source = MatrixSource(parameterCount: 2, valuesPerParameter: 3);

        await VerifyAsync(source, expectExpansionLimitDiagnostic: false, configuredLimit: "50");
        Assert.Throws<InvalidOperationException>(() => TestCaseExpansionLimits.Resolve("100O", registryBaseline: null));

        await VerifyAsync(
            source,
            expectExpansionLimitDiagnostic: false,
            configuredLimit: "100O",
            expectedUnusableOverride: "100O");
        Assert.Equal(50, TestCaseExpansionLimits.Resolve("50", registryBaseline: null));
    }

    [Fact]
    public async Task InlineParameterValues_AboveTheLimit_ReportAsync()
    {
        // [Values] constants are baked into the registry as an object?[] literal that the test host
        // allocates before discovery runs, so the runtime cap is reached too late for them. 4^7 =
        // 16384 combinations, and the arrays themselves are what the compile-time cap bounds.
        var source = ValuesSource(parameterCount: 7, valuesPerParameter: 4);

        await VerifyAsync(source, expectExpansionLimitDiagnostic: true);
    }

    [Fact]
    public async Task InlineParameterValues_BesideAnEmptyOne_ReportsNothingAsync()
    {
        // Every source is inline, so the product is exact: an empty [Values()] zeroes it, exactly as
        // discovery counts it, so the real expansion is zero test cases and the cap must not fire.
        // Charging the empty parameter as one (the earlier floor) rejected a method that expands to
        // nothing.
        var source = """
            using NextUnit;

            namespace TestProject;

            public class ValuesTests
            {
                [Test]
                public void Combined([Values] int none, [Values(1, 2, 3, 4, 5, 6)] int value)
                {
                }
            }
            """;

        await VerifyAsync(source, expectExpansionLimitDiagnostic: false, configuredLimit: "5");
    }

    [Fact]
    public async Task InlineParameterValues_WithinTheLimit_ReportNothingAsync()
    {
        var source = ValuesSource(parameterCount: 2, valuesPerParameter: 3);

        await VerifyAsync(source, expectExpansionLimitDiagnostic: false);
    }

    [Fact]
    public async Task InlineParameterValuesTimesRepeat_AboveTheLimit_ReportsAsync()
    {
        // The inline product is 2 and the repeat is 6, so neither factor alone reaches the cap of 10.
        // Before the count was threaded through, [Repeat] was dropped from a combined method entirely
        // and this projected as 2 -- a build that passed for a suite that then ran 12 test cases.
        var source = """
            using NextUnit;

            namespace TestProject;

            public class ValuesTests
            {
                [Test]
                [Repeat(6)]
                public void Combined([Values(1, 2)] int value)
                {
                }
            }
            """;

        // The count is pinned rather than only the rule, because the discovery-time cap has to reject
        // the same method with the same number; both sides charge the factor through
        // TestCaseExpansionPolicy.ApplyRepeat.
        await VerifyAsync(
            source,
            expectExpansionLimitDiagnostic: true,
            configuredLimit: "10",
            expectedMessage:
                "Test 'TestProject.ValuesTests.Combined' expands to 12 test cases, which exceeds the " +
                "limit of 10. Reduce the [Matrix], [Arguments], [Repeat], or [Values] values, or raise " +
                "the limit with <NextUnitMaxTestCasesPerMethod> in the project file.");
    }

    [Fact]
    public async Task InlineParameterValuesTimesRepeat_AtTheLimit_ReportsNothingAsync()
    {
        var source = """
            using NextUnit;

            namespace TestProject;

            public class ValuesTests
            {
                [Test]
                [Repeat(5)]
                public void Combined([Values(1, 2)] int value)
                {
                }
            }
            """;

        await VerifyAsync(source, expectExpansionLimitDiagnostic: false, configuredLimit: "10");
    }

    [Fact]
    public async Task RepeatBesideARuntimeResolvedParameterSource_IsNotChargedAsync()
    {
        // The repeat factor alone is over the cap, but the member beside it can resolve to nothing,
        // which zeroes the product however many times it would have repeated. Charging the factor on
        // its own here would reject a method whose real expansion is no test cases at all -- the same
        // over-rejection the inline floor used to produce.
        var source = """
            using System.Collections.Generic;
            using NextUnit;

            namespace TestProject;

            public class ValuesTests
            {
                public static IEnumerable<int> Sizes() => new[] { 1, 2, 3 };

                [Test]
                [Repeat(20)]
                public void Combined([ValuesFromMember(nameof(Sizes))] int size)
                {
                }
            }
            """;

        await VerifyAsync(source, expectExpansionLimitDiagnostic: false, configuredLimit: "10");
    }

    [Fact]
    public async Task RuntimeResolvedParameterSource_IsNotChargedForItsRuntimeExpansionAsync()
    {
        // The inline product is 2 and the member contributes an unknown factor, so a configured cap
        // of 2 must still admit this: how many rows the member yields is only knowable at discovery,
        // and charging a guess for it would reject a test that expands to two cases.
        var source = """
            using System.Collections.Generic;
            using NextUnit;

            namespace TestProject;

            public class ValuesTests
            {
                public static IEnumerable<int> Sizes() => new[] { 1, 2, 3, 4, 5 };

                [Test]
                public void Combined([Values(1, 2)] int value, [ValuesFromMember(nameof(Sizes))] int size)
                {
                }
            }
            """;

        await VerifyAsync(source, expectExpansionLimitDiagnostic: false, configuredLimit: "2");
    }

    [Fact]
    public async Task LargeInlineParameterValues_BesideARuntimeSource_ReportNothingAsync()
    {
        // The inline product alone (6 x 6 = 36) exceeds the configured cap of 10, but a runtime
        // [ValuesFromMember] sits beside it and can resolve to nothing. The real expansion is only
        // knowable at discovery, so the compile-time cap must not charge the inline floor and reject a
        // method that may expand to zero cases. This is the shape the earlier floor over-rejected.
        var source = """
            using System.Collections.Generic;
            using NextUnit;

            namespace TestProject;

            public class ValuesTests
            {
                public static IEnumerable<int> Sizes() => new[] { 1, 2, 3 };

                [Test]
                public void Combined(
                    [Values(1, 2, 3, 4, 5, 6)] int a,
                    [Values(1, 2, 3, 4, 5, 6)] int b,
                    [ValuesFromMember(nameof(Sizes))] int size)
                {
                }
            }
            """;

        await VerifyAsync(source, expectExpansionLimitDiagnostic: false, configuredLimit: "10");
    }

    [Fact]
    public async Task LargeInlineParameterValues_BesideAClassValuesSource_ReportNothingAsync()
    {
        // Same shape as the [ValuesFromMember] case but through [ValuesFrom<T>], the other
        // runtime-resolved kind: the class is constructed and enumerated only at discovery and can
        // yield nothing, so the oversized inline product (6 x 6 = 36 over a cap of 10) must not be
        // charged at compile time.
        var source = """
            using System.Collections;
            using System.Collections.Generic;
            using NextUnit;

            namespace TestProject;

            public class Sizes : IEnumerable<int>
            {
                public IEnumerator<int> GetEnumerator() => ((IEnumerable<int>)new[] { 1, 2, 3 }).GetEnumerator();

                IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
            }

            public class ValuesTests
            {
                [Test]
                public void Combined(
                    [Values(1, 2, 3, 4, 5, 6)] int a,
                    [Values(1, 2, 3, 4, 5, 6)] int b,
                    [ValuesFrom<Sizes>] int size)
                {
                }
            }
            """;

        await VerifyAsync(source, expectExpansionLimitDiagnostic: false, configuredLimit: "10");
    }

    [Fact]
    public async Task InlineParameterValues_SaturatingTheProduct_ReportAsync()
    {
        // 2^63 overflows long, so the all-inline product must stay on MultiplyClamped and report the
        // saturated bound rather than wrap to a value below the cap and wave the method through.
        var source = ValuesSource(parameterCount: 63, valuesPerParameter: 2);

        await VerifyAsync(source, expectExpansionLimitDiagnostic: true);
    }

    [Fact]
    public async Task RuntimeResolvedSource_IsNotChargedForItsRuntimeExpansionAsync()
    {
        // [TestData] emits one descriptor and is expanded at discovery instead, so a configured cap
        // of 1 must still let it through.
        var source = """
            using System.Collections.Generic;
            using NextUnit;

            namespace TestProject;

            public class DataTests
            {
                public static IEnumerable<object[]> Rows()
                {
                    yield return new object[] { 1 };
                    yield return new object[] { 2 };
                }

                [Test]
                [TestData(nameof(Rows))]
                public void Row(int value)
                {
                }
            }
            """;

        await VerifyAsync(source, expectExpansionLimitDiagnostic: false, configuredLimit: "1");
    }

    /// <summary>
    /// Builds a test method whose parameters each carry a <c>[Matrix]</c> of consecutive integers.
    /// </summary>
    private static string MatrixSource(int parameterCount, int valuesPerParameter) =>
        ParameterizedSource("Matrix", "MatrixTests", parameterCount, valuesPerParameter);

    /// <summary>
    /// Builds a test method whose parameters each carry a <c>[Values]</c> of consecutive integers.
    /// </summary>
    private static string ValuesSource(int parameterCount, int valuesPerParameter) =>
        ParameterizedSource("Values", "ValuesTests", parameterCount, valuesPerParameter);

    private static string ParameterizedSource(
        string attributeName,
        string className,
        int parameterCount,
        int valuesPerParameter)
    {
        var parameters = string.Join(
            "," + Environment.NewLine,
            Enumerable.Range(0, parameterCount).Select(parameterIndex =>
            {
                var values = string.Join(
                    ", ",
                    Enumerable.Range(0, valuesPerParameter)
                        .Select(value => value.ToString(CultureInfo.InvariantCulture)));

                return $"        [{attributeName}({values})] int p{parameterIndex.ToString(CultureInfo.InvariantCulture)}";
            }));

        return $$"""
            using NextUnit;

            namespace TestProject;

            public class {{className}}
            {
                [Test]
                public void Combined(
            {{parameters}})
                {
                }
            }
            """;
    }

    private static async Task VerifyAsync(
        string source,
        bool expectExpansionLimitDiagnostic,
        string? configuredLimit = null,
        string? expectedMessage = null,
        string? expectedUnusableOverride = null,
        string? expectedOverrideMessage = null)
    {
        var test = new CSharpSourceGeneratorVerifier<NextUnitGenerator>.Test
        {
            TestCode = source,
            TestBehaviors = TestBehaviors.SkipGeneratedSourcesCheck,
        };

        if (configuredLimit is not null)
        {
            // The same file MSBuild writes for CompilerVisibleProperty, so this exercises the real
            // key the generator reads rather than a hand-built options object.
            test.TestState.AnalyzerConfigFiles.Add((
                "/.globalconfig",
                $"""
                is_global = true
                build_property.NextUnitMaxTestCasesPerMethod = {configuredLimit}
                """));
        }

        if (expectedUnusableOverride is not null)
        {
            var expectedOverride = new DiagnosticResult(OverrideUnusableId, DiagnosticSeverity.Error);

            if (expectedOverrideMessage is not null)
            {
                expectedOverride = expectedOverride.WithMessage(expectedOverrideMessage);
            }

            test.ExpectedDiagnostics.Add(expectedOverride);
        }

        if (expectExpansionLimitDiagnostic)
        {
            var expected = new DiagnosticResult(ExpansionLimitId, DiagnosticSeverity.Error);

            if (expectedMessage is not null)
            {
                expected = expected.WithMessage(expectedMessage);
            }

            test.ExpectedDiagnostics.Add(expected);
        }

        await test.RunAsync(TestContext.Current.CancellationToken);
    }
}
