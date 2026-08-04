using NextUnit.Analyzers.Analyzers;
using NextUnit.Analyzers.Tests.Verifiers;
using Xunit;

namespace NextUnit.Analyzers.Tests;

public class ConflictingRetryAttributeAnalyzerTests
{
    private const string Policy = @"
public class AlwaysRetry : NextUnit.IRetryPolicy
{
    public System.Threading.Tasks.ValueTask<bool> ShouldRetryAsync(NextUnit.RetryContext context) =>
        System.Threading.Tasks.ValueTask.FromResult(true);
}
";

    [Fact]
    public async Task BothRetryAttributesOnMethod_ReportsDiagnosticAsync()
    {
        var source = @"
using NextUnit;
" + Policy + @"
public class Tests
{
    [Test]
    [Retry(2)]
    [Retry<AlwaysRetry>(3)]
    public void TestMethod()
    {
    }
}";

        var expected = CSharpAnalyzerVerifier<ConflictingRetryAttributeAnalyzer>
            .Diagnostic("NU0015")
            .WithSpan(13, 6, 13, 14)
            .WithArguments("TestMethod");

        await CSharpAnalyzerVerifier<ConflictingRetryAttributeAnalyzer>.VerifyAnalyzerAsync(source, expected);
    }

    [Fact]
    public async Task BothRetryAttributesOnClass_ReportsDiagnosticAsync()
    {
        var source = @"
using NextUnit;
" + Policy + @"
[Retry(2)]
[Retry<AlwaysRetry>(3)]
public class Tests
{
    [Test]
    public void TestMethod()
    {
    }
}";

        var expected = CSharpAnalyzerVerifier<ConflictingRetryAttributeAnalyzer>
            .Diagnostic("NU0015")
            .WithSpan(10, 2, 10, 10)
            .WithArguments("Tests");

        await CSharpAnalyzerVerifier<ConflictingRetryAttributeAnalyzer>.VerifyAnalyzerAsync(source, expected);
    }

    [Fact]
    public async Task PolicyRetryAlone_NoDiagnosticAsync()
    {
        var source = @"
using NextUnit;
" + Policy + @"
public class Tests
{
    [Test]
    [Retry<AlwaysRetry>(3)]
    public void TestMethod()
    {
    }
}";

        await CSharpAnalyzerVerifier<ConflictingRetryAttributeAnalyzer>.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task PlainRetryAlone_NoDiagnosticAsync()
    {
        var source = @"
using NextUnit;

public class Tests
{
    [Test]
    [Retry(3)]
    public void TestMethod()
    {
    }
}";

        await CSharpAnalyzerVerifier<ConflictingRetryAttributeAnalyzer>.VerifyAnalyzerAsync(source);
    }

    /// <summary>
    /// A class-level budget with a method-level policy is the documented override, not a conflict:
    /// the two attributes are on different symbols.
    /// </summary>
    [Fact]
    public async Task ClassRetryWithMethodPolicyRetry_NoDiagnosticAsync()
    {
        var source = @"
using NextUnit;
" + Policy + @"
[Retry(2)]
public class Tests
{
    [Test]
    [Retry<AlwaysRetry>(3)]
    public void TestMethod()
    {
    }
}";

        await CSharpAnalyzerVerifier<ConflictingRetryAttributeAnalyzer>.VerifyAnalyzerAsync(source);
    }
}
