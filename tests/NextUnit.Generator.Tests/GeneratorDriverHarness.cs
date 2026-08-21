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
    // extraReferences are added as MetadataReference instances rather than file paths so their
    // MetadataReferenceProperties survive: a reference whose aliases exclude the global one is the
    // only way to express a type that `extern alias` hides from the generated file.
    public static async Task<CSharpCompilation> CreateCompilationAsync(
        string source,
        OutputKind outputKind,
        CancellationToken cancellationToken,
        IEnumerable<MetadataReference>? extraReferences = null,
        string assemblyName = "TestProject")
    {
        var references = await TestReferenceAssemblies.Net10.ResolveAsync(language: null, cancellationToken);

        return CSharpCompilation.Create(
            assemblyName,
            new[] { CSharpSyntaxTree.ParseText(source, path: "Test0.cs", cancellationToken: cancellationToken) },
            references.AddRange(new MetadataReference[]
            {
                MetadataReference.CreateFromFile(typeof(TestAttribute).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(TestApplication).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(Platform.NextUnitApplicationBuilderExtensions).Assembly.Location),
            }).AddRange(extraReferences ?? []),
            new CSharpCompilationOptions(outputKind, nullableContextOptions: NullableContextOptions.Enable));
    }

    public static GeneratorDriver CreateDriver(bool trackIncrementalGeneratorSteps) =>
        CSharpGeneratorDriver.Create(
            new[] { new NextUnitGenerator().AsSourceGenerator() },
            driverOptions: new GeneratorDriverOptions(
                IncrementalGeneratorOutputKind.None,
                trackIncrementalGeneratorSteps));
}
