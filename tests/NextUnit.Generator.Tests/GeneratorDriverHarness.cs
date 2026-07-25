using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Testing.Platform.Builder;

namespace NextUnit.Generator.Tests;

/// <summary>
/// Runs <see cref="NextUnitGenerator"/> through a raw <see cref="GeneratorDriver"/>.
/// </summary>
/// <remarks>
/// The <c>CSharpSourceGeneratorTest</c> harness cannot expose incremental step tracking,
/// so caching assertions need direct driver access.
/// </remarks>
internal static class GeneratorDriverHarness
{
    public static async Task<CSharpCompilation> CreateCompilationAsync(
        string source,
        OutputKind outputKind,
        CancellationToken cancellationToken)
    {
        var references = await TestReferenceAssemblies.Net10.ResolveAsync(language: null, cancellationToken);

        return CSharpCompilation.Create(
            "TestProject",
            new[] { CSharpSyntaxTree.ParseText(source, path: "Test0.cs", cancellationToken: cancellationToken) },
            references.AddRange(new MetadataReference[]
            {
                MetadataReference.CreateFromFile(typeof(TestAttribute).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(TestApplication).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(Platform.NextUnitApplicationBuilderExtensions).Assembly.Location),
            }),
            new CSharpCompilationOptions(outputKind, nullableContextOptions: NullableContextOptions.Enable));
    }

    public static GeneratorDriver CreateDriver(bool trackIncrementalGeneratorSteps) =>
        CSharpGeneratorDriver.Create(
            new[] { new NextUnitGenerator().AsSourceGenerator() },
            driverOptions: new GeneratorDriverOptions(
                IncrementalGeneratorOutputKind.None,
                trackIncrementalGeneratorSteps));
}
