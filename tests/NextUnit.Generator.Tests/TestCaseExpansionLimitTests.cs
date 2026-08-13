using System.Globalization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;

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
    [InlineData("")]
    public async Task ConfiguredLimit_Unusable_FallsBackToTheDefaultAsync(string configuredLimit)
    {
        // A cap of 0 or a typo must not reject a nine-case matrix the default plainly allows.
        var source = MatrixSource(parameterCount: 2, valuesPerParameter: 3);

        await VerifyAsync(source, expectExpansionLimitDiagnostic: false, configuredLimit: configuredLimit);
    }

    [Fact]
    public async Task ConfiguredLimit_Unusable_StillAppliesTheDefaultAsync()
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

        await VerifyAsync(source, expectExpansionLimitDiagnostic: true, configuredLimit: "0");
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
    public async Task InlineParameterValues_BesideAnEmptyOne_StillReportAsync()
    {
        // The expansion is zero test cases because one parameter has no values, but the emitter still
        // writes every inline array out, so an empty parameter must not become a way to smuggle an
        // arbitrarily large literal past the cap.
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

        await VerifyAsync(source, expectExpansionLimitDiagnostic: true, configuredLimit: "5");
    }

    [Fact]
    public async Task InlineParameterValues_WithinTheLimit_ReportNothingAsync()
    {
        var source = ValuesSource(parameterCount: 2, valuesPerParameter: 3);

        await VerifyAsync(source, expectExpansionLimitDiagnostic: false);
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
        string? configuredLimit = null)
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

        if (expectExpansionLimitDiagnostic)
        {
            test.ExpectedDiagnostics.Add(new DiagnosticResult(ExpansionLimitId, DiagnosticSeverity.Error));
        }

        await test.RunAsync(TestContext.Current.CancellationToken);
    }
}
