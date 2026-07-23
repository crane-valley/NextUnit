using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Testing.Platform.Builder;

namespace NextUnit.Generator.Tests;

public sealed class GeneratedExecutionPathTests
{
    [Fact]
    public void Generator_EmitsDirectFactoryProviderAndInvoker()
    {
        const string source = """
            using System.Collections.Generic;
            using NextUnit;
            using NextUnit.Core;

            public sealed class DataTests
            {
                public DataTests(ITestOutput output)
                {
                }

                public static IEnumerable<object?[]> Rows()
                {
                    yield return new object?[] { 42 };
                }

                [Test]
                [TestData(nameof(Rows))]
                public void Run(int value)
                {
                }

                [Test]
                public static void StaticRun()
                {
                }
            }
            """;

        var cancellationToken = TestContext.Current.CancellationToken;
        var syntaxTree = CSharpSyntaxTree.ParseText(source, cancellationToken: cancellationToken);
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Select(static path => MetadataReference.CreateFromFile(path))
            .Concat(
            [
                MetadataReference.CreateFromFile(typeof(TestAttribute).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(TestApplication).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(NextUnit.Platform.NextUnitApplicationBuilderExtensions).Assembly.Location)
            ]);
        var compilation = CSharpCompilation.Create(
            "GeneratedExecutionPath",
            [syntaxTree],
            references,
            new CSharpCompilationOptions(OutputKind.ConsoleApplication));

        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            new NextUnitGenerator().AsSourceGenerator());
        driver = driver.RunGeneratorsAndUpdateCompilation(
            compilation,
            out var outputCompilation,
            out _,
            cancellationToken);

        var generatedRegistry = driver.GetRunResult()
            .Results
            .SelectMany(static result => result.GeneratedSources)
            .Single(static sourceResult => sourceResult.HintName == "GeneratedTestRegistry.g.cs")
            .SourceText
            .ToString();

        Xunit.Assert.Contains("TestClassFactory = static (output, context) => new global::DataTests(output)", generatedRegistry);
        Xunit.Assert.Contains("DataSourceProvider = static () => (object?)global::DataTests.Rows()", generatedRegistry);
        Xunit.Assert.Contains("TestMethodWithArguments = static (instance, arguments, ct)", generatedRegistry);
        Xunit.Assert.Contains("TestClassFactory = static (output, context) => null!", generatedRegistry);
        Xunit.Assert.DoesNotContain("GetMethod", generatedRegistry, StringComparison.Ordinal);
        Xunit.Assert.DoesNotContain("MethodInfo.Invoke", generatedRegistry, StringComparison.Ordinal);
        Xunit.Assert.DoesNotContain("InvokeTestMethodAsync", generatedRegistry, StringComparison.Ordinal);
        Xunit.Assert.DoesNotContain(
            outputCompilation.GetDiagnostics(cancellationToken),
            static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
    }
}
