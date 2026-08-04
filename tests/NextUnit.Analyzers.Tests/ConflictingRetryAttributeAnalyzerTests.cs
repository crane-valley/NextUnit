using Microsoft.CodeAnalysis.Testing;
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
            .WithSpan(14, 6, 14, 27)
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
            .WithSpan(11, 2, 11, 23)
            .WithArguments("Tests");

        await CSharpAnalyzerVerifier<ConflictingRetryAttributeAnalyzer>.VerifyAnalyzerAsync(source, expected);
    }

    /// <summary>
    /// Two constructed generic attributes share one generic definition, so the compiler's own
    /// duplicate check (<c>CS0579</c>) already rejects them.
    /// </summary>
    /// <remarks>
    /// Pinned because it is the boundary of what NU0015 is for: the rule exists for the combination
    /// the compiler cannot see, a plain <c>[Retry]</c> next to a <c>[Retry&lt;TPolicy&gt;]</c>, which
    /// are separate attribute definitions. The analyzer reports this shape too rather than special-
    /// casing it, so a second declaration is ambiguous by one rule however it is spelled.
    /// </remarks>
    [Fact]
    public async Task TwoPolicyRetryAttributes_ReportsDiagnosticAlongsideTheCompilerAsync()
    {
        var source = @"
using NextUnit;
" + Policy + @"
public class NeverRetry : IRetryPolicy
{
    public System.Threading.Tasks.ValueTask<bool> ShouldRetryAsync(RetryContext context) =>
        System.Threading.Tasks.ValueTask.FromResult(false);
}

public class Tests
{
    [Test]
    [Retry<AlwaysRetry>(2)]
    [Retry<NeverRetry>(3)]
    public void TestMethod()
    {
    }
}";

        var expected = CSharpAnalyzerVerifier<ConflictingRetryAttributeAnalyzer>
            .Diagnostic("NU0015")
            .WithSpan(20, 6, 20, 26)
            .WithArguments("TestMethod");

        var duplicateAttribute = DiagnosticResult
            .CompilerError("CS0579")
            .WithSpan(20, 6, 20, 23)
            .WithArguments("Retry<>");

        await CSharpAnalyzerVerifier<ConflictingRetryAttributeAnalyzer>.VerifyAnalyzerAsync(
            source, expected, duplicateAttribute);
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
