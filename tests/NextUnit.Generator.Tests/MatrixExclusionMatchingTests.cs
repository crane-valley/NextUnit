using Microsoft.CodeAnalysis;

namespace NextUnit.Generator.Tests;

/// <summary>
/// Pins how matrix values are matched against matrix exclusions.
/// </summary>
/// <remarks>
/// The generator compares resolved constants instead of Roslyn <c>TypedConstant</c> values, so the
/// resolved form has to stay distinguishable for values that differ only inside a nested array or
/// only by numeric type.
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
        // The registry is built with StringBuilder.AppendLine, so the declaration ends with the
        // platform newline; the type name alone also appears in the array type of the property.
        var marker = "new global::NextUnit.Internal.TestCaseDescriptor" + Environment.NewLine;
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
