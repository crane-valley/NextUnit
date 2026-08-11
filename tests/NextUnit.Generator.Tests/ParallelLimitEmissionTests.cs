using Microsoft.CodeAnalysis;

namespace NextUnit.Generator.Tests;

/// <summary>
/// Pins how <c>[ParallelLimit]</c> reaches the generated registry, including the
/// method-over-class-over-assembly precedence.
/// </summary>
/// <remarks>
/// Asserted line by line rather than with a full snapshot baseline, matching
/// <see cref="CultureEmissionTests"/>: the emission is a single descriptor field, and the existing
/// snapshots already pin the surrounding descriptor.
/// </remarks>
public class ParallelLimitEmissionTests
{
    [Fact]
    public async Task AssemblyParallelLimit_AppliesWhenNothingNearerDeclaresOneAsync()
    {
        var registry = await GenerateRegistryAsync("""
            using NextUnit;

            [assembly: ParallelLimit(4)]

            namespace TestProject;

            public class ThrottledTests
            {
                [Test]
                public void First()
                {
                }

                [Test]
                public void Second()
                {
                }
            }
            """);

        var occurrences = registry.Split(["ParallelLimit = 4"], StringSplitOptions.None).Length - 1;
        Assert.Equal(2, occurrences);
    }

    [Fact]
    public async Task ClassParallelLimit_OverridesTheAssemblyLimitAsync()
    {
        var registry = await GenerateRegistryAsync("""
            using NextUnit;

            [assembly: ParallelLimit(4)]

            namespace TestProject;

            [ParallelLimit(2)]
            public class ThrottledTests
            {
                [Test]
                public void Overriding()
                {
                }
            }
            """);

        Assert.Contains("ParallelLimit = 2", registry);
        Assert.False(
            registry.Contains("ParallelLimit = 4", StringComparison.Ordinal),
            "The class-level limit must replace the assembly-level one.");
    }

    [Fact]
    public async Task MethodParallelLimit_OverridesTheClassAndAssemblyLimitsAsync()
    {
        var registry = await GenerateRegistryAsync("""
            using NextUnit;

            [assembly: ParallelLimit(4)]

            namespace TestProject;

            [ParallelLimit(2)]
            public class ThrottledTests
            {
                [Test]
                [ParallelLimit(1)]
                public void Overriding()
                {
                }
            }
            """);

        Assert.Contains("ParallelLimit = 1", registry);
        Assert.False(
            registry.Contains("ParallelLimit = 2", StringComparison.Ordinal),
            "The method-level limit must replace the class-level one.");
        Assert.False(
            registry.Contains("ParallelLimit = 4", StringComparison.Ordinal),
            "The method-level limit must replace the assembly-level one.");
    }

    /// <summary>
    /// A class-level limit still reaches every test in the class, and a suite that declares nothing
    /// still emits no limit, so the scheduler keeps falling back to the processor count.
    /// </summary>
    [Fact]
    public async Task WithoutAnAssemblyLimit_ClassAndAbsentDeclarationsAreUnchangedAsync()
    {
        var registry = await GenerateRegistryAsync("""
            using NextUnit;

            namespace TestProject;

            [ParallelLimit(2)]
            public class ThrottledTests
            {
                [Test]
                public void Throttled()
                {
                }
            }

            public class PlainTests
            {
                [Test]
                public void Unbounded()
                {
                }
            }
            """);

        Assert.Contains("ParallelLimit = 2", registry);
        Assert.Contains("ParallelLimit = null", registry);
    }

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
}
