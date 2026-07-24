using NextUnit.Analyzers.Analyzers;
using NextUnit.Analyzers.CodeFixes;
using NextUnit.Analyzers.Tests.Verifiers;
using Xunit;

namespace NextUnit.Analyzers.Tests;

public class AsyncVoidTestAnalyzerTests
{
    [Fact]
    public async Task AsyncVoidTest_ReportsDiagnosticAsync()
    {
        var source = @"
using NextUnit;
using System.Threading.Tasks;

public class Tests
{
    [Test]
    public async void TestMethod()
    {
        await Task.Delay(1);
    }
}";

        var expected = CSharpAnalyzerVerifier<AsyncVoidTestAnalyzer>
            .Diagnostic("NU0001")
            .WithSpan(8, 23, 8, 33)
            .WithArguments("TestMethod");

        await CSharpAnalyzerVerifier<AsyncVoidTestAnalyzer>.VerifyAnalyzerAsync(source, expected);
    }

    [Fact]
    public async Task AsyncTaskTest_NoDiagnosticAsync()
    {
        var source = @"
using NextUnit;
using System.Threading.Tasks;

public class Tests
{
    [Test]
    public async Task TestMethod()
    {
        await Task.Delay(1);
    }
}";

        await CSharpAnalyzerVerifier<AsyncVoidTestAnalyzer>.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task SyncVoidTest_NoDiagnosticAsync()
    {
        var source = @"
using NextUnit;

public class Tests
{
    [Test]
    public void TestMethod()
    {
    }
}";

        await CSharpAnalyzerVerifier<AsyncVoidTestAnalyzer>.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task AsyncVoidNonTest_NoDiagnosticAsync()
    {
        var source = @"
using NextUnit;
using System.Threading.Tasks;

public class Tests
{
    public async void HelperMethod()
    {
        await Task.Delay(1);
    }
}";

        await CSharpAnalyzerVerifier<AsyncVoidTestAnalyzer>.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task AsyncVoidBeforeMethod_ReportsDiagnosticAsync()
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

        var expected = CSharpAnalyzerVerifier<AsyncVoidTestAnalyzer>
            .Diagnostic("NU0001")
            .WithSpan(7, 23, 7, 28)
            .WithArguments("Setup");

        await CSharpAnalyzerVerifier<AsyncVoidTestAnalyzer>.VerifyAnalyzerAsync(source, expected);
    }

    [Fact]
    public async Task AsyncVoidAfterMethod_ReportsDiagnosticAsync()
    {
        const string source = """
            using NextUnit;
            using System.Threading.Tasks;

            public class Tests
            {
                [After(LifecycleScope.Test)]
                public async void Cleanup()
                {
                    await Task.Yield();
                }
            }
            """;

        var expected = CSharpAnalyzerVerifier<AsyncVoidTestAnalyzer>
            .Diagnostic("NU0001")
            .WithSpan(7, 23, 7, 30)
            .WithArguments("Cleanup");

        await CSharpAnalyzerVerifier<AsyncVoidTestAnalyzer>.VerifyAnalyzerAsync(source, expected);
    }

    [Fact]
    public async Task CodeFix_ChangesAsyncVoidToAsyncTaskAsync()
    {
        var source = @"
using NextUnit;
using System.Threading.Tasks;

public class Tests
{
    [Test]
    public async void TestMethod()
    {
        await Task.Delay(1);
    }
}";

        var fixedSource = @"
using NextUnit;
using System.Threading.Tasks;

public class Tests
{
    [Test]
    public async Task TestMethod()
    {
        await Task.Delay(1);
    }
}";

        var expected = CSharpCodeFixVerifier<AsyncVoidTestAnalyzer, AsyncVoidTestCodeFixProvider>
            .Diagnostic("NU0001")
            .WithSpan(8, 23, 8, 33)
            .WithArguments("TestMethod");

        await CSharpCodeFixVerifier<AsyncVoidTestAnalyzer, AsyncVoidTestCodeFixProvider>
            .VerifyCodeFixAsync(source, expected, fixedSource);
    }
}
