using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
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

    public static GeneratorDriver CreateDriver(
        bool trackIncrementalGeneratorSteps,
        AnalyzerConfigOptionsProvider? optionsProvider = null) =>
        CSharpGeneratorDriver.Create(
            new[] { new NextUnitGenerator().AsSourceGenerator() },
            additionalTexts: null,
            parseOptions: null,
            optionsProvider: optionsProvider,
            driverOptions: new GeneratorDriverOptions(
                IncrementalGeneratorOutputKind.None,
                trackIncrementalGeneratorSteps));

    /// <summary>
    /// Supplies MSBuild properties to the generator the way the generated <c>.globalconfig</c> does.
    /// </summary>
    /// <remarks>
    /// Hand-built rather than routed through <c>CSharpSourceGeneratorVerifier</c>'s
    /// <c>AnalyzerConfigFiles</c>, because that harness asserts against expected generated text and
    /// these tests need the emitted text back to assert on one line of it.
    /// </remarks>
    internal sealed class GlobalOptionsProvider(IReadOnlyDictionary<string, string> properties)
        : AnalyzerConfigOptionsProvider
    {
        public override AnalyzerConfigOptions GlobalOptions { get; } = new Options(properties);

        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => GlobalOptions;

        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => GlobalOptions;

        private sealed class Options(IReadOnlyDictionary<string, string> properties) : AnalyzerConfigOptions
        {
            public override bool TryGetValue(string key, out string value)
            {
                if (properties.TryGetValue(key, out var found))
                {
                    value = found;
                    return true;
                }

                value = string.Empty;
                return false;
            }
        }
    }
}
