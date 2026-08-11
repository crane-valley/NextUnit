using NextUnit.Analyzers.Analyzers;
using NextUnit.Analyzers.Tests.Verifiers;
using Xunit;

namespace NextUnit.Analyzers.Tests;

public class ParallelLimitValueAnalyzerTests
{
    [Fact]
    public async Task ZeroOnAMethod_ReportsDiagnosticAsync()
    {
        var source = @"
using NextUnit;

public class Tests
{
    [Test]
    [ParallelLimit(0)]
    public void TestMethod()
    {
    }
}";

        var expected = CSharpAnalyzerVerifier<ParallelLimitValueAnalyzer>
            .Diagnostic("NU0019")
            .WithSpan(7, 20, 7, 21)
            .WithArguments(0);

        await CSharpAnalyzerVerifier<ParallelLimitValueAnalyzer>.VerifyAnalyzerAsync(source, expected);
    }

    [Fact]
    public async Task ZeroOnAClass_ReportsDiagnosticAsync()
    {
        var source = @"
using NextUnit;

[ParallelLimit(0)]
public class Tests
{
    [Test]
    public void TestMethod()
    {
    }
}";

        var expected = CSharpAnalyzerVerifier<ParallelLimitValueAnalyzer>
            .Diagnostic("NU0019")
            .WithSpan(4, 16, 4, 17)
            .WithArguments(0);

        await CSharpAnalyzerVerifier<ParallelLimitValueAnalyzer>.VerifyAnalyzerAsync(source, expected);
    }

    /// <summary>
    /// An assembly-level attribute is why the rule runs on syntax: no symbol action ever visits one.
    /// It is also the form this rule exists for, because the generator only began resolving it here.
    /// </summary>
    [Fact]
    public async Task ZeroOnTheAssembly_ReportsDiagnosticAsync()
    {
        var source = @"
using NextUnit;

[assembly: ParallelLimit(0)]

public class Tests
{
    [Test]
    public void TestMethod()
    {
    }
}";

        var expected = CSharpAnalyzerVerifier<ParallelLimitValueAnalyzer>
            .Diagnostic("NU0019")
            .WithSpan(4, 26, 4, 27)
            .WithArguments(0);

        await CSharpAnalyzerVerifier<ParallelLimitValueAnalyzer>.VerifyAnalyzerAsync(source, expected);
    }

    [Fact]
    public async Task BelowMinusOne_ReportsDiagnosticAsync()
    {
        var source = @"
using NextUnit;

[assembly: ParallelLimit(-2)]

public class Tests
{
    [Test]
    public void TestMethod()
    {
    }
}";

        var expected = CSharpAnalyzerVerifier<ParallelLimitValueAnalyzer>
            .Diagnostic("NU0019")
            .WithSpan(4, 26, 4, 28)
            .WithArguments(-2);

        await CSharpAnalyzerVerifier<ParallelLimitValueAnalyzer>.VerifyAnalyzerAsync(source, expected);
    }

    /// <summary>
    /// -1 is the one non-positive value the ParallelOptions setter accepts, but Parallel.ForEachAsync
    /// maps it to the processor count, which is what an absent attribute already means, and it still
    /// wins the Min the scheduler takes across a parallel group. It is a limit that raises limits.
    /// </summary>
    [Fact]
    public async Task MinusOne_ReportsDiagnosticAsync()
    {
        var source = @"
using NextUnit;

[assembly: ParallelLimit(-1)]

public class Tests
{
    [Test]
    public void TestMethod()
    {
    }
}";

        var expected = CSharpAnalyzerVerifier<ParallelLimitValueAnalyzer>
            .Diagnostic("NU0019")
            .WithSpan(4, 26, 4, 28)
            .WithArguments(-1);

        await CSharpAnalyzerVerifier<ParallelLimitValueAnalyzer>.VerifyAnalyzerAsync(source, expected);
    }

    /// <summary>
    /// The constant carries the type it was written with, so a widening conversion at the use site
    /// would hide the value from a check that only recognizes <c>int</c>.
    /// </summary>
    [Fact]
    public async Task ZeroWrittenAsAnotherIntegralType_ReportsDiagnosticAsync()
    {
        var source = @"
using NextUnit;

public class Tests
{
    [Test]
    [ParallelLimit((short)0)]
    public void TestMethod()
    {
    }
}";

        var expected = CSharpAnalyzerVerifier<ParallelLimitValueAnalyzer>
            .Diagnostic("NU0019")
            .WithSpan(7, 20, 7, 28)
            .WithArguments(0);

        await CSharpAnalyzerVerifier<ParallelLimitValueAnalyzer>.VerifyAnalyzerAsync(source, expected);
    }

    [Fact]
    public async Task PositiveValues_ReportNoDiagnosticAsync()
    {
        var source = @"
using NextUnit;

[assembly: ParallelLimit(8)]

[ParallelLimit(4)]
public class Tests
{
    [Test]
    [ParallelLimit(1)]
    public void TestMethod()
    {
    }
}";

        await CSharpAnalyzerVerifier<ParallelLimitValueAnalyzer>.VerifyAnalyzerAsync(source);
    }

    /// <summary>
    /// The attribute is matched by symbol, not by how it is spelled, so an alias is still checked.
    /// </summary>
    [Fact]
    public async Task AliasedAttribute_ReportsDiagnosticAsync()
    {
        var source = @"
using NextUnit;
using Throttle = NextUnit.ParallelLimitAttribute;

public class Tests
{
    [Test]
    [Throttle(0)]
    public void TestMethod()
    {
    }
}";

        var expected = CSharpAnalyzerVerifier<ParallelLimitValueAnalyzer>
            .Diagnostic("NU0019")
            .WithSpan(8, 15, 8, 16)
            .WithArguments(0);

        await CSharpAnalyzerVerifier<ParallelLimitValueAnalyzer>.VerifyAnalyzerAsync(source, expected);
    }

    /// <summary>
    /// A same-named attribute from another namespace is not this rule's business.
    /// </summary>
    [Fact]
    public async Task ForeignAttribute_ReportsNoDiagnosticAsync()
    {
        var source = @"
namespace Other
{
    public sealed class ParallelLimitAttribute : System.Attribute
    {
        public ParallelLimitAttribute(int maxDegreeOfParallelism) { }
    }
}

namespace Consumer
{
    using Other;

    [ParallelLimit(0)]
    public class Tests
    {
        public void TestMethod()
        {
        }
    }
}";

        await CSharpAnalyzerVerifier<ParallelLimitValueAnalyzer>.VerifyAnalyzerAsync(source);
    }
}
