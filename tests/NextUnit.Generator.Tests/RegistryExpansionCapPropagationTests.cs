using Microsoft.CodeAnalysis;
using NextUnit.Internal;
using NextUnit.Shared;

namespace NextUnit.Generator.Tests;

/// <summary>
/// Pins the one path that keeps the compile-time and discovery-time caps from being configured
/// independently: the generator emits the cap it enforced, and discovery reads it as the baseline
/// the environment variable overrides.
/// </summary>
/// <remarks>
/// Before this, <c>&lt;NextUnitMaxTestCasesPerMethod&gt;50000&lt;/NextUnitMaxTestCasesPerMethod&gt;</c>
/// let the generator emit 50000 cases while discovery still rejected a combined data source at the
/// 10000 default, and the run was the first place that showed up.
/// </remarks>
public sealed class RegistryExpansionCapPropagationTests
{
    private const string PlainSource = """
        using NextUnit;

        namespace TestProject;

        public class CapTests
        {
            [Test]
            public void SimpleTest() { }
        }
        """;

    [Fact]
    public async Task Emit_WithoutTheProperty_CarriesTheDefaultAsync()
    {
        var registry = await GenerateRegistryAsync(configuredCap: null);

        Xunit.Assert.Contains(
            $"public static int MaxTestCasesPerMethod {{ get; }} = {TestCaseExpansionPolicy.DefaultMaxTestCasesPerMethod};",
            registry);
    }

    [Fact]
    public async Task Emit_WithTheProperty_CarriesTheConfiguredCapAsync()
    {
        var registry = await GenerateRegistryAsync(configuredCap: "50000");

        Xunit.Assert.Contains("public static int MaxTestCasesPerMethod { get; } = 50000;", registry);
    }

    /// <summary>
    /// The registry provider is what discovery reads through, so emitting the static property
    /// without wiring it to the interface would leave the cap unreachable.
    /// </summary>
    [Fact]
    public async Task Emit_WiresTheCapThroughTheRegistryProviderAsync()
    {
        var registry = await GenerateRegistryAsync(configuredCap: "50000");

        Xunit.Assert.Contains(
            "public int MaxTestCasesPerMethod => GeneratedTestRegistry.MaxTestCasesPerMethod;",
            registry);
    }

    [Fact]
    public void Resolve_RegistryBaseline_IsHonoredWhenTheEnvironmentIsUnset()
    {
        Assert.Equal(50_000, TestCaseExpansionLimits.Resolve(rawValue: null, registryBaseline: 50_000));
    }

    [Fact]
    public void Resolve_NoRegistryBaseline_UsesTheBuiltInDefault()
    {
        Assert.Equal(
            TestCaseExpansionPolicy.DefaultMaxTestCasesPerMethod,
            TestCaseExpansionLimits.Resolve(rawValue: null, registryBaseline: null));
    }

    /// <summary>
    /// The override is explicit and per run, so it wins in both directions -- narrowing a project
    /// that raised its cap as well as widening one that did not.
    /// </summary>
    [Theory]
    [InlineData("70000", 70_000)]
    [InlineData("25", 25)]
    public void Resolve_EnvironmentOverride_BeatsTheRegistryBaseline(string rawValue, int expected)
    {
        Assert.Equal(expected, TestCaseExpansionLimits.Resolve(rawValue, registryBaseline: 50_000));
    }

    /// <summary>
    /// A baseline does not soften the refusal from #244: falling back to it would still be the
    /// looser value the user did not type.
    /// </summary>
    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("100O")]
    public void Resolve_UnusableEnvironmentOverride_StillThrowsWithABaseline(string rawValue)
    {
        var exception = Assert.Throws<InvalidOperationException>(
            () => TestCaseExpansionLimits.Resolve(rawValue, registryBaseline: 50_000));

        Xunit.Assert.Contains("50000", exception.Message);
    }

    /// <summary>
    /// A registry NextUnit generated cannot report this, so it is a contract violation rather than
    /// an unset baseline, and substituting the default for it would be the fail-open swap
    /// <c>NEXTUNIT014</c> exists to refuse.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Resolve_NonPositiveRegistryBaseline_IsRefused(int registryBaseline)
    {
        var exception = Assert.Throws<InvalidOperationException>(
            () => TestCaseExpansionLimits.Resolve(rawValue: null, registryBaseline));

        Xunit.Assert.Contains("not positive", exception.Message);
    }

    /// <summary>
    /// A usable override does not rescue a broken registry, because each configured value is judged
    /// where it is read.
    /// </summary>
    [Fact]
    public void Resolve_NonPositiveRegistryBaseline_IsRefusedEvenWithAUsableOverride()
    {
        Assert.Throws<InvalidOperationException>(
            () => TestCaseExpansionLimits.Resolve("50", registryBaseline: 0));
    }

    /// <summary>
    /// A registry generated before the member existed is already compiled without it, so the default
    /// interface implementation is the only thing that keeps it readable.
    /// </summary>
    [Fact]
    public void RegistryWithoutTheMember_ReportsTheBuiltInDefault()
    {
        IGeneratedTestRegistry registry = new RegistryPredatingTheCap();

        Assert.Equal(TestCaseExpansionPolicy.DefaultMaxTestCasesPerMethod, registry.MaxTestCasesPerMethod);
    }

    /// <summary>
    /// The cap the expander applies is the one it was handed, not one it looks up: that is what lets
    /// two assemblies in one VSTest process be bounded by their own settings.
    /// </summary>
    [Fact]
    public void ExpandSingle_AppliesThePassedBaseline()
    {
        var descriptor = CreateDescriptor(valuesPerParameter: 40);

        Assert.Equal(40, CombinedDataSourceExpander.ExpandSingle(descriptor, registryMaxTestCasesPerMethod: 40).Count());
        Assert.Throws<InvalidOperationException>(
            () => CombinedDataSourceExpander.ExpandSingle(descriptor, registryMaxTestCasesPerMethod: 39).ToList());
    }

    private static CombinedDataSourceDescriptor CreateDescriptor(int valuesPerParameter) => new()
    {
        BaseId = "Tests.CapPropagation.Combined",
        TestClass = typeof(CombinedTarget),
        MethodName = nameof(CombinedTarget.Run),
        ParameterTypes = [typeof(int)],
        ParameterSources =
        [
            new ParameterDataSource
            {
                ParameterIndex = 0,
                ParameterName = "only",
                Kind = ParameterDataSourceKind.Inline,
                InlineValues = Enumerable.Range(0, valuesPerParameter).Cast<object?>().ToArray(),
            }
        ],
    };

    private static async Task<string> GenerateRegistryAsync(string? configuredCap)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var compilation = await GeneratorDriverHarness.CreateCompilationAsync(
            PlainSource,
            OutputKind.DynamicallyLinkedLibrary,
            cancellationToken);

        var optionsProvider = configuredCap is null
            ? null
            : new GeneratorDriverHarness.GlobalOptionsProvider(
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["build_property.NextUnitMaxTestCasesPerMethod"] = configuredCap
                });

        var driver = GeneratorDriverHarness
            .CreateDriver(trackIncrementalGeneratorSteps: false, optionsProvider)
            .RunGenerators(compilation, cancellationToken);

        return driver.GetRunResult().Results.Single().GeneratedSources
            .Single(static generated => generated.HintName == "GeneratedTestRegistry.g.cs")
            .SourceText
            .ToString();
    }

    private sealed class CombinedTarget
    {
        public void Run(int only) => _ = only;
    }

    /// <summary>
    /// Declares no <c>MaxTestCasesPerMethod</c>, so it compiles only because of the default
    /// interface implementation and dispatches to it -- the shape an already-compiled registry from
    /// an earlier NextUnit has.
    /// </summary>
    private sealed class RegistryPredatingTheCap : IGeneratedTestRegistry
    {
        public IReadOnlyList<TestCaseDescriptor> TestCases => [];

        public IReadOnlyList<TestDataDescriptor> TestDataDescriptors => [];

        public IReadOnlyList<ClassDataSourceDescriptor> ClassDataSourceDescriptors => [];

        public IReadOnlyList<CombinedDataSourceDescriptor> CombinedDataSourceDescriptors => [];

        public LifecycleMethodDelegate[] GlobalBeforeAssemblyMethods => [];

        public LifecycleMethodDelegate[] GlobalAfterAssemblyMethods => [];

        public LifecycleMethodDelegate[] GlobalBeforeSessionMethods => [];

        public LifecycleMethodDelegate[] GlobalAfterSessionMethods => [];
    }
}
