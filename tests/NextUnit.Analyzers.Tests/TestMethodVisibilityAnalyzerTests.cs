using NextUnit.Analyzers.Analyzers;
using NextUnit.Analyzers.CodeFixes;
using NextUnit.Analyzers.Tests.Verifiers;
using Xunit;

namespace NextUnit.Analyzers.Tests;

public class TestMethodVisibilityAnalyzerTests
{
    [Fact]
    public async Task PrivateTestMethod_ReportsDiagnostic()
    {
        var source = @"
using NextUnit;

public class Tests
{
    [Test]
    private void TestMethod()
    {
    }
}";

        var expected = CSharpAnalyzerVerifier<TestMethodVisibilityAnalyzer>
            .Diagnostic("NU0002")
            .WithSpan(7, 18, 7, 28)
            .WithArguments("TestMethod");

        await CSharpAnalyzerVerifier<TestMethodVisibilityAnalyzer>.VerifyAnalyzerAsync(source, expected);
    }

    [Fact]
    public async Task InternalTestMethod_ReportsDiagnostic()
    {
        var source = @"
using NextUnit;

public class Tests
{
    [Test]
    internal void TestMethod()
    {
    }
}";

        var expected = CSharpAnalyzerVerifier<TestMethodVisibilityAnalyzer>
            .Diagnostic("NU0002")
            .WithSpan(7, 19, 7, 29)
            .WithArguments("TestMethod");

        await CSharpAnalyzerVerifier<TestMethodVisibilityAnalyzer>.VerifyAnalyzerAsync(source, expected);
    }

    [Fact]
    public async Task ProtectedTestMethod_ReportsDiagnostic()
    {
        var source = @"
using NextUnit;

public class Tests
{
    [Test]
    protected void TestMethod()
    {
    }
}";

        var expected = CSharpAnalyzerVerifier<TestMethodVisibilityAnalyzer>
            .Diagnostic("NU0002")
            .WithSpan(7, 20, 7, 30)
            .WithArguments("TestMethod");

        await CSharpAnalyzerVerifier<TestMethodVisibilityAnalyzer>.VerifyAnalyzerAsync(source, expected);
    }

    [Fact]
    public async Task PublicTestMethod_NoDiagnostic()
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

        await CSharpAnalyzerVerifier<TestMethodVisibilityAnalyzer>.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task PrivateNonTestMethod_NoDiagnostic()
    {
        var source = @"
using NextUnit;

public class Tests
{
    private void HelperMethod()
    {
    }
}";

        await CSharpAnalyzerVerifier<TestMethodVisibilityAnalyzer>.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task CodeFix_MakesPrivateMethodPublic()
    {
        var source = @"
using NextUnit;

public class Tests
{
    [Test]
    private void TestMethod()
    {
    }
}";

        var fixedSource = @"
using NextUnit;

public class Tests
{
    [Test]
    public void TestMethod()
    {
    }
}";

        var expected = CSharpCodeFixVerifier<TestMethodVisibilityAnalyzer, TestMethodVisibilityCodeFixProvider>
            .Diagnostic("NU0002")
            .WithSpan(7, 18, 7, 28)
            .WithArguments("TestMethod");

        await CSharpCodeFixVerifier<TestMethodVisibilityAnalyzer, TestMethodVisibilityCodeFixProvider>
            .VerifyCodeFixAsync(source, expected, fixedSource);
    }
}
