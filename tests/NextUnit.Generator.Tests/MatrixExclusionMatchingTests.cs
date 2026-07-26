using Microsoft.CodeAnalysis;

namespace NextUnit.Generator.Tests;

/// <summary>
/// Pins how matrix values are matched against matrix exclusions.
/// </summary>
/// <remarks>
/// The generator compares resolved constants instead of Roslyn <c>TypedConstant</c> values, so the
/// resolved form has to stay distinguishable for values that differ only inside a nested array or
/// only by numeric type.
/// One case the resolved form also handles has no test here: two types that share a fully qualified
/// name in different assemblies, reachable only through an extern alias. This harness compiles a
/// single compilation and cannot express that without building and referencing a second assembly.
/// </remarks>
public class MatrixExclusionMatchingTests
{
    /// <summary>
    /// Two array values whose elements flatten to the same text must not be treated as the same value.
    /// </summary>
    [Fact]
    public async Task ArrayValues_AreNotConfusedByElementTextAsync()
    {
        var source = """
            using NextUnit;

            namespace TestProject;

            public class MatrixTests
            {
                [Test]
                [MatrixExclusion(new object[] { new[] { "a", "b" } })]
                public void Single(
                    [Matrix(new[] { "a", "b" }, new[] { "a,System.String:b" })] string[] values)
                {
                }
            }
            """;

        var generated = await GenerateRegistryAsync(source);

        Assert.Equal(1, CountTestCases(generated));
        Assert.Contains("new string[] { \"a,System.String:b\" }", generated);
    }

    /// <summary>
    /// An exclusion written with a different numeric type than the matrix value does not match,
    /// which mirrors how the boxed values compared before.
    /// </summary>
    [Fact]
    public async Task NumericTypeMismatch_DoesNotExcludeAsync()
    {
        var source = """
            using NextUnit;

            namespace TestProject;

            public class MatrixTests
            {
                [Test]
                [MatrixExclusion(1)]
                public void Single([Matrix(1L, 2L)] long value)
                {
                }
            }
            """;

        var generated = await GenerateRegistryAsync(source);

        Assert.Equal(2, CountTestCases(generated));
    }

    /// <summary>
    /// typeof values are matched by the type they name, including its type arguments.
    /// </summary>
    [Fact]
    public async Task TypeValues_MatchOnlyTheNamedTypeAsync()
    {
        var source = """
            using System;
            using System.Collections.Generic;
            using NextUnit;

            namespace TestProject;

            public class MatrixTests
            {
                [Test]
                [MatrixExclusion(typeof(List<int>))]
                public void Single(
                    [Matrix(typeof(List<int>), typeof(List<long>), typeof(int))] Type value)
                {
                }
            }
            """;

        var generated = await GenerateRegistryAsync(source);

        Assert.Equal(2, CountTestCases(generated));
        Assert.False(
            generated.Contains("List<int>", StringComparison.Ordinal),
            "The excluded typeof(List<int>) combination must not be emitted.");
        Assert.True(
            generated.Contains("List<long>", StringComparison.Ordinal),
            "typeof(List<long>) differs from the exclusion and must still be emitted.");
    }

    /// <summary>
    /// A nested type is identified by the constructed type that contains it.
    /// </summary>
    [Fact]
    public async Task NestedTypeValues_KeepTheirContainingTypeArgumentsAsync()
    {
        var source = """
            using System;
            using NextUnit;

            namespace TestProject;

            public class Outer<T>
            {
                public class Inner
                {
                }
            }

            public class MatrixTests
            {
                [Test]
                [MatrixExclusion(typeof(Outer<int>.Inner))]
                public void Single(
                    [Matrix(typeof(Outer<int>.Inner), typeof(Outer<string>.Inner))] Type value)
                {
                }
            }
            """;

        var generated = await GenerateRegistryAsync(source);

        Assert.Equal(1, CountTestCases(generated));
        Assert.True(
            generated.Contains("Outer<string>.Inner", StringComparison.Ordinal),
            "Only the excluded Outer<int>.Inner combination must be removed.");
    }

    /// <summary>
    /// Floating-point values that differ beyond the default formatting precision stay distinct.
    /// </summary>
    /// <remarks>
    /// The generator can run in-proc under .NET Framework, where the default numeric format rounds to
    /// 15 significant digits and would merge these two values.
    /// </remarks>
    [Fact]
    public async Task CloseFloatingPointValues_AreNotConfusedAsync()
    {
        var source = """
            using NextUnit;

            namespace TestProject;

            public class MatrixTests
            {
                [Test]
                [MatrixExclusion(1.0000000000000002)]
                public void Single([Matrix(1.0000000000000002, 1.0000000000000004)] double value)
                {
                }
            }
            """;

        var generated = await GenerateRegistryAsync(source);

        Assert.Equal(1, CountTestCases(generated));
        Assert.True(
            generated.Contains("1.0000000000000004", StringComparison.Ordinal),
            "Only the excluded value must be removed.");
    }

    /// <summary>
    /// A matching exclusion still removes its combination.
    /// </summary>
    [Fact]
    public async Task MatchingValue_IsExcludedAsync()
    {
        var source = """
            using NextUnit;

            namespace TestProject;

            public class MatrixTests
            {
                [Test]
                [MatrixExclusion(1L)]
                public void Single([Matrix(1L, 2L)] long value)
                {
                }
            }
            """;

        var generated = await GenerateRegistryAsync(source);

        Assert.Equal(1, CountTestCases(generated));
    }

    private static async Task<string> GenerateRegistryAsync(string source)
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        var compilation = await GeneratorDriverHarness.CreateCompilationAsync(
            source,
            OutputKind.DynamicallyLinkedLibrary,
            cancellationToken);

        var driver = GeneratorDriverHarness
            .CreateDriver(trackIncrementalGeneratorSteps: false)
            .RunGenerators(compilation, cancellationToken);

        var registry = driver.GetRunResult().Results
            .SelectMany(result => result.GeneratedSources)
            .Single(generatedSource => generatedSource.HintName == "GeneratedTestRegistry.g.cs");

        return registry.SourceText.ToString();
    }

    private static int CountTestCases(string generated)
    {
        // The generator always emits LF, on every host OS; the trailing newline is what separates
        // the descriptor construction from the array type of the property, which reads the same.
        const string marker = "new global::NextUnit.Internal.TestCaseDescriptor\n";
        var count = 0;

        for (var index = generated.IndexOf(marker, StringComparison.Ordinal);
            index >= 0;
            index = generated.IndexOf(marker, index + marker.Length, StringComparison.Ordinal))
        {
            count++;
        }

        return count;
    }
}
