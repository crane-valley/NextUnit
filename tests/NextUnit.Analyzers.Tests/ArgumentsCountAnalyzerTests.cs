using NextUnit.Analyzers.Analyzers;
using NextUnit.Analyzers.Tests.Verifiers;
using Xunit;

namespace NextUnit.Analyzers.Tests;

public class ArgumentsCountAnalyzerTests
{
    [Fact]
    public async Task ArgumentsCountMismatch_TooManyArguments_ReportsDiagnosticAsync()
    {
        var source = @"
using NextUnit;

public class Tests
{
    [Test]
    [Arguments(1, 2, 3)]
    public void TestMethod(int a, int b)
    {
    }
}";

        var expected = CSharpAnalyzerVerifier<ArgumentsCountAnalyzer>
            .Diagnostic("NU0004")
            .WithSpan(7, 6, 7, 24)
            .WithArguments("TestMethod", 2, 3);

        await CSharpAnalyzerVerifier<ArgumentsCountAnalyzer>.VerifyAnalyzerAsync(source, expected);
    }

    [Fact]
    public async Task ArgumentsCountMismatch_TooFewArguments_ReportsDiagnosticAsync()
    {
        var source = @"
using NextUnit;

public class Tests
{
    [Test]
    [Arguments(1)]
    public void TestMethod(int a, int b, int c)
    {
    }
}";

        var expected = CSharpAnalyzerVerifier<ArgumentsCountAnalyzer>
            .Diagnostic("NU0004")
            .WithSpan(7, 6, 7, 18)
            .WithArguments("TestMethod", 3, 1);

        await CSharpAnalyzerVerifier<ArgumentsCountAnalyzer>.VerifyAnalyzerAsync(source, expected);
    }

    [Fact]
    public async Task ArgumentsCountMatches_NoDiagnosticAsync()
    {
        var source = @"
using NextUnit;

public class Tests
{
    [Test]
    [Arguments(1, 2, 3)]
    public void TestMethod(int a, int b, int c)
    {
    }
}";

        await CSharpAnalyzerVerifier<ArgumentsCountAnalyzer>.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task MultipleArgumentsAttributes_AllMatch_NoDiagnosticAsync()
    {
        var source = @"
using NextUnit;

public class Tests
{
    [Test]
    [Arguments(1, 2)]
    [Arguments(3, 4)]
    public void TestMethod(int a, int b)
    {
    }
}";

        await CSharpAnalyzerVerifier<ArgumentsCountAnalyzer>.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task MultipleArgumentsAttributes_OneMismatch_ReportsDiagnosticAsync()
    {
        var source = @"
using NextUnit;

public class Tests
{
    [Test]
    [Arguments(1, 2)]
    [Arguments(3, 4, 5)]
    public void TestMethod(int a, int b)
    {
    }
}";

        var expected = CSharpAnalyzerVerifier<ArgumentsCountAnalyzer>
            .Diagnostic("NU0004")
            .WithSpan(8, 6, 8, 24)
            .WithArguments("TestMethod", 2, 3);

        await CSharpAnalyzerVerifier<ArgumentsCountAnalyzer>.VerifyAnalyzerAsync(source, expected);
    }

    [Fact]
    public async Task NoArgumentsAttribute_NoDiagnosticAsync()
    {
        var source = @"
using NextUnit;

public class Tests
{
    [Test]
    public void TestMethod(int a, int b)
    {
    }
}";

        await CSharpAnalyzerVerifier<ArgumentsCountAnalyzer>.VerifyAnalyzerAsync(source);
    }
}
