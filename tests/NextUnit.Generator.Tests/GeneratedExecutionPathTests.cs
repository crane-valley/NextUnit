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

        var (generatedRegistry, outputCompilation) = RunGenerator(source);
        var cancellationToken = TestContext.Current.CancellationToken;

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

    [Fact]
    public void Generator_ValueTaskMethodsAndLifecycle_EmitAsTaskAndCompile()
    {
        const string source = """
            using System.Collections.Generic;
            using System.Threading.Tasks;
            using NextUnit;

            public sealed class ValueTaskTests
            {
                public static IEnumerable<object?[]> Rows()
                {
                    yield return new object?[] { 1 };
                }

                [Test]
                public ValueTask SyncValueTask() => default;

                [Test]
                public async ValueTask AsyncValueTask()
                {
                    await Task.Yield();
                }

                [Test]
                public ValueTask<int> SyncGenericValueTask() => new(42);

                [Test]
                public async ValueTask<int> AsyncGenericValueTask()
                {
                    await Task.Yield();
                    return 42;
                }

                [Test]
                [Arguments(1)]
                public ValueTask InlineValueTask(int value) => default;

                [Test]
                [TestData(nameof(Rows))]
                public ValueTask RuntimeValueTask(double value) => default;

                [Before(LifecycleScope.Test)]
                public ValueTask Setup() => default;
            }
            """;

        var (generatedRegistry, outputCompilation) = RunGenerator(source);
        var cancellationToken = TestContext.Current.CancellationToken;

        Xunit.Assert.Contains("((global::ValueTaskTests)instance).SyncValueTask().AsTask()", generatedRegistry);
        Xunit.Assert.Contains("((global::ValueTaskTests)instance).AsyncValueTask().AsTask()", generatedRegistry);
        Xunit.Assert.Contains("((global::ValueTaskTests)instance).SyncGenericValueTask().AsTask()", generatedRegistry);
        Xunit.Assert.Contains("((global::ValueTaskTests)instance).AsyncGenericValueTask().AsTask()", generatedRegistry);
        Xunit.Assert.Contains("((global::ValueTaskTests)instance).InlineValueTask(1).AsTask()", generatedRegistry);
        Xunit.Assert.Contains("((global::ValueTaskTests)instance).Setup().AsTask()", generatedRegistry);
        Xunit.Assert.Contains(
            "global::NextUnit.Internal.ArgumentConverter.Convert<double>(arguments[0], \"value\", \"RuntimeValueTask\")",
            generatedRegistry);
        Xunit.Assert.Contains("((global::ValueTaskTests)instance).RuntimeValueTask(", generatedRegistry);
        Xunit.Assert.Contains(".AsTask()", generatedRegistry);
        Xunit.Assert.DoesNotContain(
            outputCompilation.GetDiagnostics(cancellationToken),
            static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
    }

    private static (string GeneratedRegistry, Compilation OutputCompilation) RunGenerator(string source)
    {
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
            $"GeneratedExecutionPath_{Guid.NewGuid():N}",
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

        return (generatedRegistry, outputCompilation);
    }
}
