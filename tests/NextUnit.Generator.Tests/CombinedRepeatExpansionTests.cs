using System.Globalization;
using Microsoft.CodeAnalysis;
using NextUnit.Internal;
using NextUnit.Shared;

namespace NextUnit.Generator.Tests;

/// <summary>
/// Pins how <c>[Repeat]</c> multiplies a combined data source.
/// </summary>
/// <remarks>
/// The other three buckets expand <c>[Repeat]</c> at compile time, so this is the one combination
/// where the count has to survive as descriptor state and be applied at discovery. Both halves are
/// covered here -- what the generator writes, and what the expander does with it -- because a count
/// emitted but never charged is exactly the silent drop this replaced.
/// </remarks>
public class CombinedRepeatExpansionTests
{
    /// <summary>
    /// The highest configured cap the expansion cases here can still exercise by actually expanding.
    /// </summary>
    private const int MaxExercisableLimit = 1_000_000;

    private static readonly int _limit = TestCaseExpansionLimits.ResolveFromEnvironment(registryBaseline: null);

    [Fact]
    public async Task CombinedSourceWithRepeat_EmitsTheCountAsync()
    {
        var registry = await GenerateRegistryAsync("""
            using System.Collections.Generic;
            using NextUnit;

            namespace TestProject;

            public class CombinedTests
            {
                public static IEnumerable<int> Numbers() => new[] { 1, 2 };

                [Test]
                [Repeat(5)]
                public void Combine(
                    [ValuesFromMember(nameof(Numbers))] int number,
                    [Values("a", "b")] string label)
                {
                }
            }
            """);

        // The descriptor is the only place the count can survive to: the emitter writes no test case
        // for a combined method, so there is nothing here for it to have been folded into.
        Assert.Contains("RepeatCount = 5,", registry);
    }

    [Fact]
    public async Task CombinedSourceWithoutRepeat_EmitsNoCountAsync()
    {
        var registry = await GenerateRegistryAsync("""
            using NextUnit;

            namespace TestProject;

            public class CombinedTests
            {
                [Test]
                public void Combine([Values(1, 2)] int number)
                {
                }
            }
            """);

        // Emitting a null count for every combined descriptor would churn every existing baseline for
        // a property that already defaults to null.
        Assert.False(
            registry.Contains("RepeatCount", StringComparison.Ordinal),
            "A combined test without [Repeat] must not emit a repeat count.");
    }

    [Fact]
    public void ExpandSingle_WithRepeat_MultipliesTheProduct()
    {
        Assert.SkipWhen(_limit < 10, $"A configured cap of {_limit} cannot admit this expansion.");

        var testCases = CombinedDataSourceExpander
            .ExpandSingle(CreateDescriptor(repeatCount: 5, "x", "y"), registryMaxTestCasesPerMethod: null)
            .ToList();

        Assert.Equal(10, testCases.Count);
        Assert.Equal(10, testCases.Select(testCase => testCase.Id.Value).Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void ExpandSingle_WithRepeat_SuffixesTheIdAndTheDisplayName()
    {
        Assert.SkipWhen(_limit < 10, $"A configured cap of {_limit} cannot admit this expansion.");

        var testCases = CombinedDataSourceExpander
            .ExpandSingle(CreateDescriptor(repeatCount: 2, "x", "y"), registryMaxTestCasesPerMethod: null)
            .ToList();

        // The suffix is the one the generator writes for every compile-time expansion, so a repeated
        // combined case is addressable the same way a repeated [Arguments] case is.
        Assert.Equal(
            new[]
            {
                "Tests.Combined.Repeat:Combined[0]#0",
                "Tests.Combined.Repeat:Combined[0]#1",
                "Tests.Combined.Repeat:Combined[1]#0",
                "Tests.Combined.Repeat:Combined[1]#1",
            },
            testCases.Select(testCase => testCase.Id.Value));

        Assert.Equal([0, 1, 0, 1], testCases.Select(testCase => testCase.RepeatIndex));
        Assert.All(testCases, testCase => Assert.EndsWith(
            $" (Repeat #{(testCase.RepeatIndex!.Value + 1).ToString(CultureInfo.InvariantCulture)})",
            testCase.DisplayName));
    }

    [Fact]
    public void ExpandSingle_WithRepeatOfOne_StillSuffixesTheId()
    {
        Assert.SkipWhen(_limit < 2, $"A configured cap of {_limit} cannot admit this expansion.");

        // The suffix tracks the attribute, not the count. Suppressing it at one would rename the first
        // iteration's test case the moment [Repeat(1)] became [Repeat(2)].
        var testCases = CombinedDataSourceExpander
            .ExpandSingle(CreateDescriptor(repeatCount: 1, "x", "y"), registryMaxTestCasesPerMethod: null)
            .ToList();

        Assert.Equal(
            new[] { "Tests.Combined.Repeat:Combined[0]#0", "Tests.Combined.Repeat:Combined[1]#0" },
            testCases.Select(testCase => testCase.Id.Value));
    }

    [Fact]
    public void ExpandSingle_WithoutRepeat_KeepsTheBareIds()
    {
        Assert.SkipWhen(_limit < 2, $"A configured cap of {_limit} cannot admit this expansion.");

        var testCases = CombinedDataSourceExpander
            .ExpandSingle(CreateDescriptor(repeatCount: null, "x", "y"), registryMaxTestCasesPerMethod: null)
            .ToList();

        // Threading the count through must not move an id that no [Repeat] participates in; these are
        // published test case ids that a rerun, a filter, or a CI history refers to by name.
        Assert.Equal(
            new[] { "Tests.Combined.Repeat:Combined[0]", "Tests.Combined.Repeat:Combined[1]" },
            testCases.Select(testCase => testCase.Id.Value));

        Assert.All(testCases, testCase => Assert.Null(testCase.RepeatIndex));
    }

    [Fact]
    public void ExpandSingle_RepeatOverTheCap_ThrowsChargingTheRepeatFactor()
    {
        // Two values are far under any cap; the repeat is what carries the method over it, so a cap
        // that ignored the factor would admit an expansion of 2 x _limit test cases.
        var descriptor = CreateDescriptor(repeatCount: _limit, "x", "y");

        var exception = Assert.Throws<InvalidOperationException>(
            () => CombinedDataSourceExpander.ExpandSingle(descriptor, registryMaxTestCasesPerMethod: null).ToList());

        // Asserted against the shared helper rather than against a literal: this is the same call
        // TestCaseExpansionValidator projects NEXTUNIT013 from, so the two caps cannot report
        // different numbers for the same method without this failing.
        var charged = TestCaseExpansionPolicy.ApplyRepeat(2, _limit);

        Assert.Contains(charged.ToString(CultureInfo.InvariantCulture), exception.Message);
        Assert.Contains(_limit.ToString(CultureInfo.InvariantCulture), exception.Message);
    }

    /// <summary>
    /// A count below one cannot reach a generated registry, so it means a hand-written one. Refused
    /// rather than left to collapse the product: the limit check passes over a zero product because
    /// an empty resolved source has always meant no test cases, and a broken count would ride out on
    /// that as a silently empty method.
    /// </summary>
    [Fact]
    public void ExpandSingle_NonPositiveRepeat_IsRejected()
    {
        var descriptor = CreateDescriptor(repeatCount: 0, "x", "y");

        var exception = Assert.Throws<InvalidOperationException>(
            () => CombinedDataSourceExpander.ExpandSingle(descriptor, registryMaxTestCasesPerMethod: null).ToList());

        Assert.Contains("not positive", exception.Message);
    }

    [Fact]
    public void ExpandSingle_RepeatWithALazySource_BoundsTheDrawByTheRepeatFactor()
    {
        Assert.SkipWhen(
            _limit > MaxExercisableLimit,
            $"A configured cap of {_limit} is larger than this suite can expand.");

        const int repeatCount = 4;
        var drawn = 0;
        var descriptor = new CombinedDataSourceDescriptor
        {
            BaseId = "Tests.Combined.Repeat",
            TestClass = typeof(RepeatTarget),
            MethodName = nameof(RepeatTarget.Run),
            ParameterTypes = [typeof(object)],
            RepeatCount = repeatCount,
            ParameterSources =
            [
                new ParameterDataSource
                {
                    ParameterIndex = 0,
                    ParameterName = "only",
                    Kind = ParameterDataSourceKind.Member,
                    MemberProvider = () => Counted(_limit + 1_000, () => drawn++),
                }
            ],
        };

        Assert.Throws<InvalidOperationException>(
            () => CombinedDataSourceExpander.ExpandSingle(descriptor, registryMaxTestCasesPerMethod: null).ToList());

        // The repeat seeds the running product, so the source may only contribute a quarter of the cap
        // before the product is over it. Charging the repeat after the sources were drawn would have
        // let this one fill the whole cap first.
        var bound = (_limit / repeatCount) + 2;
        Assert.True(drawn <= bound, $"Drew {drawn} values from a source bounded at {bound}.");
    }

    /// <summary>
    /// A lazy sequence that reports each value as it is drawn.
    /// </summary>
    private static IEnumerable<object?> Counted(int length, Action onDraw)
    {
        for (var index = 0; index < length; index++)
        {
            onDraw();
            yield return index;
        }
    }

    private static CombinedDataSourceDescriptor CreateDescriptor(int? repeatCount, params object?[] values) =>
        new()
        {
            BaseId = "Tests.Combined.Repeat",
            TestClass = typeof(RepeatTarget),
            MethodName = nameof(RepeatTarget.Run),
            ParameterTypes = [typeof(object)],
            RepeatCount = repeatCount,
            ParameterSources =
            [
                new ParameterDataSource
                {
                    ParameterIndex = 0,
                    ParameterName = "only",
                    Kind = ParameterDataSourceKind.Inline,
                    InlineValues = values,
                }
            ],
        };

    private static async Task<string> GenerateRegistryAsync(string source)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var compilation = await GeneratorDriverHarness.CreateCompilationAsync(
            source,
            OutputKind.DynamicallyLinkedLibrary,
            cancellationToken);
        var driver = GeneratorDriverHarness.CreateDriver(trackIncrementalGeneratorSteps: false)
            .RunGenerators(compilation, cancellationToken);

        return driver.GetRunResult().Results.Single().GeneratedSources
            .Single(static generated => generated.HintName == "GeneratedTestRegistry.g.cs")
            .SourceText
            .ToString();
    }

    private sealed class RepeatTarget
    {
        public void Run(object? only) => _ = only;
    }
}
