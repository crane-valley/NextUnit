using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using NextUnit.Analyzers.Analyzers;
using NextUnit.Analyzers.Tests.Verifiers;
using Xunit;

namespace NextUnit.Analyzers.Tests;

public sealed class UnsupportedMethodReturnTypeAnalyzerTests
{
    [Fact]
    public async Task TestMethodWithUnsupportedReturnType_ReportsDiagnosticAsync()
    {
        const string source = """
            using NextUnit;

            public class Tests
            {
                [Test]
                public int TestMethod() => 42;
            }
            """;

        var expected = CSharpAnalyzerVerifier<UnsupportedMethodReturnTypeAnalyzer>
            .Diagnostic("NU0011")
            .WithSpan(6, 16, 6, 26)
            .WithArguments("TestMethod", "int");

        await CSharpAnalyzerVerifier<UnsupportedMethodReturnTypeAnalyzer>.VerifyAnalyzerAsync(source, expected);
    }

    [Fact]
    public async Task LifecycleMethodWithUnsupportedReturnType_ReportsDiagnosticAsync()
    {
        const string source = """
            using NextUnit;

            public class Tests
            {
                [Before(LifecycleScope.Test)]
                public CustomAwaitable Setup() => default;
            }

            public readonly struct CustomAwaitable
            {
                public Awaiter GetAwaiter() => default;

                public readonly struct Awaiter : System.Runtime.CompilerServices.INotifyCompletion
                {
                    public bool IsCompleted => true;
                    public void OnCompleted(System.Action continuation) => continuation();
                    public void GetResult()
                    {
                    }
                }
            }
            """;

        var expected = CSharpAnalyzerVerifier<UnsupportedMethodReturnTypeAnalyzer>
            .Diagnostic("NU0011")
            .WithSpan(6, 28, 6, 33)
            .WithArguments("Setup", "CustomAwaitable");

        await CSharpAnalyzerVerifier<UnsupportedMethodReturnTypeAnalyzer>.VerifyAnalyzerAsync(source, expected);
    }

    [Fact]
    public async Task SupportedReturnTypes_NoDiagnosticAsync()
    {
        const string source = """
            using NextUnit;
            using System.Threading.Tasks;

            public class Tests
            {
                [Test]
                public void VoidTest()
                {
                }

                [Test]
                public Task TaskTest() => Task.CompletedTask;

                [Test]
                public Task<int> GenericTaskTest() => Task.FromResult(1);

                [Test]
                public ValueTask ValueTaskTest() => default;

                [Test]
                public ValueTask<int> GenericValueTaskTest() => new(1);

                [After(LifecycleScope.Test)]
                public ValueTask Cleanup() => default;
            }
            """;

        await CSharpAnalyzerVerifier<UnsupportedMethodReturnTypeAnalyzer>.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task TaskDerivedReturnType_NoDiagnosticAsync()
    {
        const string source = """
            using NextUnit;
            using System.Threading.Tasks;

            public sealed class CustomTask : Task
            {
                public CustomTask() : base(() => { })
                {
                }
            }

            public class Tests
            {
                [Test]
                public CustomTask DerivedTaskTest() => new();
            }
            """;

        await CSharpAnalyzerVerifier<UnsupportedMethodReturnTypeAnalyzer>.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task AsyncVoidLifecycleMethod_NoDiagnosticAsync()
    {
        const string source = """
            using NextUnit;
            using System.Threading.Tasks;

            public class Tests
            {
                [Before(LifecycleScope.Test)]
                public async void Setup()
                {
                    await Task.Yield();
                }
            }
            """;

        await CSharpAnalyzerVerifier<UnsupportedMethodReturnTypeAnalyzer>.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task NamespaceIdenticalImpostorReturnTypes_ReportDiagnosticsAsync()
    {
        const string fakeTasksSource = """
            namespace System.Threading.Tasks
            {
                public sealed class Task
                {
                }

                public readonly struct ValueTask
                {
                }
            }
            """;

        const string source = """
            extern alias FakeTasks;
            using NextUnit;

            public class Tests
            {
                [Test]
                public FakeTasks::System.Threading.Tasks.Task ImpostorTaskTest() => new();

                [Test]
                public FakeTasks::System.Threading.Tasks.ValueTask ImpostorValueTaskTest() => new();
            }
            """;

        var cancellationToken = TestContext.Current.CancellationToken;
        var platformReferences = GetPlatformReferences();
        var fakeTasksCompilation = CSharpCompilation.Create(
            "FakeTasks",
            [CSharpSyntaxTree.ParseText(fakeTasksSource, cancellationToken: cancellationToken)],
            platformReferences,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        using var fakeTasksStream = new MemoryStream();
        var emitResult = fakeTasksCompilation.Emit(
            fakeTasksStream,
            cancellationToken: cancellationToken);
        Xunit.Assert.True(
            emitResult.Success,
            string.Join(Environment.NewLine, emitResult.Diagnostics));

        var fakeTasksReference = MetadataReference.CreateFromImage(
            fakeTasksStream.ToArray(),
            new MetadataReferenceProperties(
                aliases: ImmutableArray.Create("FakeTasks")));
        var compilation = CSharpCompilation.Create(
            "ImpostorReturnTypes",
            [CSharpSyntaxTree.ParseText(source, cancellationToken: cancellationToken)],
            platformReferences.Append(fakeTasksReference),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var diagnostics = await compilation
            .WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(
                new UnsupportedMethodReturnTypeAnalyzer()),
                options: null)
            .GetAnalyzerDiagnosticsAsync(cancellationToken);

        Xunit.Assert.Equal(2, diagnostics.Count(diagnostic => diagnostic.Id == "NU0011"));
        Xunit.Assert.Contains(
            diagnostics,
            diagnostic => diagnostic.GetMessage().Contains(
                "ImpostorTaskTest",
                StringComparison.Ordinal));
        Xunit.Assert.Contains(
            diagnostics,
            diagnostic => diagnostic.GetMessage().Contains(
                "ImpostorValueTaskTest",
                StringComparison.Ordinal));
    }

    private static MetadataReference[] GetPlatformReferences() =>
        ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
        .Split(Path.PathSeparator)
        .Select(static path => MetadataReference.CreateFromFile(path))
        .ToArray();
}
