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
}
