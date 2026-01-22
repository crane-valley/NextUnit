using NextUnit.Analyzers.Analyzers;
using NextUnit.Analyzers.Tests.Verifiers;
using Xunit;

namespace NextUnit.Analyzers.Tests;

public class TimeoutValueAnalyzerTests
{
    [Fact]
    public async Task TimeoutZero_ReportsDiagnostic()
    {
        var source = @"
using NextUnit;

public class Tests
{
    [Test]
    [Timeout(0)]
    public void TestMethod()
    {
    }
}";

        var expected = CSharpAnalyzerVerifier<TimeoutValueAnalyzer>
            .Diagnostic("NU0006")
            .WithSpan(7, 6, 7, 16)
            .WithArguments(0);

        await CSharpAnalyzerVerifier<TimeoutValueAnalyzer>.VerifyAnalyzerAsync(source, expected);
    }

    [Fact]
    public async Task TimeoutNegative_ReportsDiagnostic()
    {
        var source = @"
using NextUnit;

public class Tests
{
    [Test]
    [Timeout(-100)]
    public void TestMethod()
    {
    }
}";

        var expected = CSharpAnalyzerVerifier<TimeoutValueAnalyzer>
            .Diagnostic("NU0006")
            .WithSpan(7, 6, 7, 19)
            .WithArguments(-100);

        await CSharpAnalyzerVerifier<TimeoutValueAnalyzer>.VerifyAnalyzerAsync(source, expected);
    }

    [Fact]
    public async Task TimeoutPositive_NoDiagnostic()
    {
        var source = @"
using NextUnit;

public class Tests
{
    [Test]
    [Timeout(1000)]
    public void TestMethod()
    {
    }
}";

        await CSharpAnalyzerVerifier<TimeoutValueAnalyzer>.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task TimeoutOnClass_Positive_NoDiagnostic()
    {
        var source = @"
using NextUnit;

[Timeout(5000)]
public class Tests
{
    [Test]
    public void TestMethod()
    {
    }
}";

        await CSharpAnalyzerVerifier<TimeoutValueAnalyzer>.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task NoTimeoutAttribute_NoDiagnostic()
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

        await CSharpAnalyzerVerifier<TimeoutValueAnalyzer>.VerifyAnalyzerAsync(source);
    }
}
