using NextUnit.Analyzers.Analyzers;
using NextUnit.Analyzers.Tests.Verifiers;
using Xunit;

namespace NextUnit.Analyzers.Tests;

public class RetryCountAnalyzerTests
{
    private const string Policy = @"
public class AlwaysRetry : NextUnit.IRetryPolicy
{
    public System.Threading.Tasks.ValueTask<bool> ShouldRetryAsync(NextUnit.RetryContext context) =>
        System.Threading.Tasks.ValueTask.FromResult(true);
}
";

    [Fact]
    public async Task RetryZero_ReportsDiagnosticAsync()
    {
        var source = @"
using NextUnit;

public class Tests
{
    [Test]
    [Retry(0)]
    public void TestMethod()
    {
    }
}";

        var expected = CSharpAnalyzerVerifier<RetryCountAnalyzer>
            .Diagnostic("NU0017")
            .WithSpan(7, 6, 7, 14)
            .WithArguments(0);

        await CSharpAnalyzerVerifier<RetryCountAnalyzer>.VerifyAnalyzerAsync(source, expected);
    }

    [Fact]
    public async Task RetryNegative_ReportsDiagnosticAsync()
    {
        var source = @"
using NextUnit;

public class Tests
{
    [Test]
    [Retry(-2, 50)]
    public void TestMethod()
    {
    }
}";

        var expected = CSharpAnalyzerVerifier<RetryCountAnalyzer>
            .Diagnostic("NU0017")
            .WithSpan(7, 6, 7, 19)
            .WithArguments(-2);

        await CSharpAnalyzerVerifier<RetryCountAnalyzer>.VerifyAnalyzerAsync(source, expected);
    }

    /// <summary>
    /// The generic form bypasses its constructor guard the same way, so it needs the same check.
    /// </summary>
    [Fact]
    public async Task PolicyRetryZero_ReportsDiagnosticAsync()
    {
        var source = @"
using NextUnit;
" + Policy + @"
public class Tests
{
    [Test]
    [Retry<AlwaysRetry>(0)]
    public void TestMethod()
    {
    }
}";

        var expected = CSharpAnalyzerVerifier<RetryCountAnalyzer>
            .Diagnostic("NU0017")
            .WithSpan(13, 6, 13, 27)
            .WithArguments(0);

        await CSharpAnalyzerVerifier<RetryCountAnalyzer>.VerifyAnalyzerAsync(source, expected);
    }

    [Fact]
    public async Task ClassLevelRetryZero_ReportsDiagnosticAsync()
    {
        var source = @"
using NextUnit;

[Retry(0)]
public class Tests
{
    [Test]
    public void TestMethod()
    {
    }
}";

        var expected = CSharpAnalyzerVerifier<RetryCountAnalyzer>
            .Diagnostic("NU0017")
            .WithSpan(4, 2, 4, 10)
            .WithArguments(0);

        await CSharpAnalyzerVerifier<RetryCountAnalyzer>.VerifyAnalyzerAsync(source, expected);
    }

    [Fact]
    public async Task RetryOne_NoDiagnosticAsync()
    {
        var source = @"
using NextUnit;

public class Tests
{
    [Test]
    [Retry(1)]
    public void TestMethod()
    {
    }
}";

        await CSharpAnalyzerVerifier<RetryCountAnalyzer>.VerifyAnalyzerAsync(source);
    }

    /// <summary>
    /// A negative delay is not flagged: the engine waits only when the delay is greater than zero, so
    /// the value cannot reach <c>Task.Delay</c> and has no runtime effect.
    /// </summary>
    [Fact]
    public async Task NegativeDelay_NoDiagnosticAsync()
    {
        var source = @"
using NextUnit;

public class Tests
{
    [Test]
    [Retry(3, -1)]
    public void TestMethod()
    {
    }
}";

        await CSharpAnalyzerVerifier<RetryCountAnalyzer>.VerifyAnalyzerAsync(source);
    }
}
