using Microsoft.CodeAnalysis;

namespace NextUnit.Generator.Tests;

/// <summary>
/// Pins how <c>[Culture]</c>, <c>[UICulture]</c>, and <c>[InvariantCulture]</c> reach the generated
/// registry, including the per-axis method-over-class-over-assembly precedence.
/// </summary>
/// <remarks>
/// Asserted line by line rather than with a full snapshot baseline because the emission is one
/// conditional block. The existing snapshots already cover the rest of the descriptor, and their
/// staying byte-identical is itself the proof that a test declaring no culture emits nothing new.
/// </remarks>
public class CultureEmissionTests
{
    [Fact]
    public async Task MethodCulture_EmitsTheDeclaredNameAsync()
    {
        var registry = await GenerateRegistryAsync("""
            using NextUnit;

            namespace TestProject;

            public class CultureTests
            {
                [Test]
                [Culture("ja-JP")]
                public void Formats()
                {
                }
            }
            """);

        Assert.Contains("Culture = new global::NextUnit.Internal.TestCultureInfo", registry);
        Assert.Contains("CultureName = \"ja-JP\",", registry);
        Assert.Contains("UICultureName = null", registry);
    }

    [Fact]
    public async Task NoCulture_EmitsNothingAsync()
    {
        var registry = await GenerateRegistryAsync("""
            using NextUnit;

            namespace TestProject;

            public class CultureTests
            {
                [Test]
                public void Plain()
                {
                }
            }
            """);

        // The descriptor already defaults to the shared empty instance, so the common case must not
        // grow the generated file - which is also what keeps every existing snapshot byte-identical.
        Assert.False(
            registry.Contains("TestCultureInfo", StringComparison.Ordinal),
            "A test declaring no culture must not emit a culture block.");
    }

    [Fact]
    public async Task InvariantCulture_EmitsTheEmptyNameOnBothAxesAsync()
    {
        var registry = await GenerateRegistryAsync("""
            using NextUnit;

            namespace TestProject;

            public class CultureTests
            {
                [Test]
                [InvariantCulture]
                public void Formats()
                {
                }
            }
            """);

        Assert.Contains("CultureName = \"\",", registry);
        Assert.Contains("UICultureName = \"\"", registry);
    }

    /// <summary>
    /// The shorthand supplies only the axis left unspecified, so this means invariant formatting with
    /// Japanese resources rather than a conflict.
    /// </summary>
    [Fact]
    public async Task InvariantCultureWithExplicitUICulture_KeepsTheExplicitAxisAsync()
    {
        var registry = await GenerateRegistryAsync("""
            using NextUnit;

            namespace TestProject;

            public class CultureTests
            {
                [Test]
                [InvariantCulture]
                [UICulture("ja-JP")]
                public void Formats()
                {
                }
            }
            """);

        Assert.Contains("CultureName = \"\",", registry);
        Assert.Contains("UICultureName = \"ja-JP\"", registry);
    }

    [Fact]
    public async Task ClassCulture_AppliesToEveryTestInTheClassAsync()
    {
        var registry = await GenerateRegistryAsync("""
            using NextUnit;

            namespace TestProject;

            [Culture("de-DE")]
            public class CultureTests
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

        var occurrences = registry.Split(["CultureName = \"de-DE\","], StringSplitOptions.None).Length - 1;
        Assert.Equal(2, occurrences);
    }

    [Fact]
    public async Task MethodCulture_OverridesTheClassCultureAsync()
    {
        var registry = await GenerateRegistryAsync("""
            using NextUnit;

            namespace TestProject;

            [Culture("de-DE")]
            public class CultureTests
            {
                [Test]
                [Culture("ja-JP")]
                public void Overriding()
                {
                }
            }
            """);

        Assert.Contains("CultureName = \"ja-JP\",", registry);
        Assert.False(
            registry.Contains("de-DE", StringComparison.Ordinal),
            "The method-level culture must replace the class-level one.");
    }

    [Fact]
    public async Task AssemblyCulture_AppliesWhenNothingNearerDeclaresOneAsync()
    {
        var registry = await GenerateRegistryAsync("""
            using NextUnit;

            [assembly: Culture("de-DE")]

            namespace TestProject;

            public class CultureTests
            {
                [Test]
                public void Inheriting()
                {
                }
            }
            """);

        Assert.Contains("CultureName = \"de-DE\",", registry);
    }

    /// <summary>
    /// The two axes resolve independently, so overriding one at a nearer level leaves the other
    /// inherited rather than cleared.
    /// </summary>
    [Fact]
    public async Task AxesResolveIndependentlyAcrossLevelsAsync()
    {
        var registry = await GenerateRegistryAsync("""
            using NextUnit;

            [assembly: UICulture("fr-FR")]

            namespace TestProject;

            [Culture("de-DE")]
            public class CultureTests
            {
                [Test]
                [Culture("ja-JP")]
                public void Mixed()
                {
                }
            }
            """);

        Assert.Contains("CultureName = \"ja-JP\",", registry);
        Assert.Contains("UICultureName = \"fr-FR\"", registry);
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
