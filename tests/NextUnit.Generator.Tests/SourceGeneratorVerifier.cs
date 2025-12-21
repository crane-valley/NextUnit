using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.Testing.Platform.Builder;

namespace NextUnit.Generator.Tests;

/// <summary>
/// Test helper for NextUnit source generator tests.
/// </summary>
public static class CSharpSourceGeneratorVerifier<TSourceGenerator>
    where TSourceGenerator : IIncrementalGenerator, new()
{
    private static readonly Lazy<ReferenceAssemblies> _net10ReferenceAssemblies = new(CreateNet10ReferenceAssemblies);

    private static ReferenceAssemblies CreateNet10ReferenceAssemblies()
    {
        return new ReferenceAssemblies(
            "net10.0",
            new PackageIdentity("Microsoft.NETCore.App.Ref", "10.0.0"),
            Path.Combine("ref", "net10.0"));
    }

    public class Test : CSharpSourceGeneratorTest<EmptySourceGeneratorProvider, DefaultVerifier>
    {
        public Test()
        {
            ReferenceAssemblies = _net10ReferenceAssemblies.Value;

            // Add required assembly references
            TestState.AdditionalReferences.Add(typeof(TestAttribute).Assembly);
            TestState.AdditionalReferences.Add(typeof(TestApplication).Assembly);
            TestState.AdditionalReferences.Add(typeof(NextUnit.Platform.NextUnitApplicationBuilderExtensions).Assembly);
        }

        protected override IEnumerable<Type> GetSourceGenerators()
        {
            yield return typeof(TSourceGenerator);
        }

        protected override CompilationOptions CreateCompilationOptions()
        {
            var compilationOptions = base.CreateCompilationOptions();
            return compilationOptions.WithSpecificDiagnosticOptions(
                compilationOptions.SpecificDiagnosticOptions.SetItems(GetNullableWarningsFromCompiler()));
        }

        private static ImmutableDictionary<string, ReportDiagnostic> GetNullableWarningsFromCompiler()
        {
            string[] args = { "/warnaserror:nullable" };
            var commandLineArguments = CSharpCommandLineParser.Default.Parse(args, baseDirectory: Environment.CurrentDirectory, sdkDirectory: Environment.CurrentDirectory);
            var nullableWarnings = commandLineArguments.CompilationOptions.SpecificDiagnosticOptions;

            return nullableWarnings;
        }
    }

    public static async Task VerifyGeneratorAsync(string source, params (string filename, string content)[] generatedSources)
    {
        var test = new Test
        {
            TestCode = source,
        };

        foreach (var (filename, content) in generatedSources)
        {
            test.TestState.GeneratedSources.Add((typeof(TSourceGenerator), filename, content));
        }

        await test.RunAsync();
    }
}
