using NextUnit.Analyzers.Analyzers;
using NextUnit.Analyzers.Tests.Verifiers;
using Xunit;

namespace NextUnit.Analyzers.Tests;

public class CultureNameAnalyzerTests
{
    [Fact]
    public async Task CultureWithASpace_ReportsDiagnosticAsync()
    {
        var source = @"
using NextUnit;

public class Tests
{
    [Test]
    [Culture(""not a culture"")]
    public void TestMethod()
    {
    }
}";

        var expected = CSharpAnalyzerVerifier<CultureNameAnalyzer>
            .Diagnostic("NU0018")
            .WithSpan(7, 14, 7, 29)
            .WithArguments("not a culture");

        await CSharpAnalyzerVerifier<CultureNameAnalyzer>.VerifyAnalyzerAsync(source, expected);
    }

    [Fact]
    public async Task UICultureWithATrailingSeparator_ReportsDiagnosticAsync()
    {
        var source = @"
using NextUnit;

public class Tests
{
    [Test]
    [UICulture(""ja-"")]
    public void TestMethod()
    {
    }
}";

        var expected = CSharpAnalyzerVerifier<CultureNameAnalyzer>
            .Diagnostic("NU0018")
            .WithSpan(7, 16, 7, 21)
            .WithArguments("ja-");

        await CSharpAnalyzerVerifier<CultureNameAnalyzer>.VerifyAnalyzerAsync(source, expected);
    }

    [Fact]
    public async Task CultureWithConsecutiveSeparators_ReportsDiagnosticAsync()
    {
        var source = @"
using NextUnit;

public class Tests
{
    [Test]
    [Culture(""ja--JP"")]
    public void TestMethod()
    {
    }
}";

        var expected = CSharpAnalyzerVerifier<CultureNameAnalyzer>
            .Diagnostic("NU0018")
            .WithSpan(7, 14, 7, 22)
            .WithArguments("ja--JP");

        await CSharpAnalyzerVerifier<CultureNameAnalyzer>.VerifyAnalyzerAsync(source, expected);
    }

    [Fact]
    public async Task ClassLevelCultureWithPunctuation_ReportsDiagnosticAsync()
    {
        var source = @"
using NextUnit;

[Culture(""ja.JP"")]
public class Tests
{
    [Test]
    public void TestMethod()
    {
    }
}";

        var expected = CSharpAnalyzerVerifier<CultureNameAnalyzer>
            .Diagnostic("NU0018")
            .WithSpan(4, 10, 4, 17)
            .WithArguments("ja.JP");

        await CSharpAnalyzerVerifier<CultureNameAnalyzer>.VerifyAnalyzerAsync(source, expected);
    }

    /// <summary>
    /// An assembly-level attribute is why the rule runs on syntax: no symbol action ever visits one.
    /// </summary>
    [Fact]
    public async Task AssemblyLevelCulture_ReportsDiagnosticAsync()
    {
        var source = @"
using NextUnit;

[assembly: Culture(""en US"")]

public class Tests
{
    [Test]
    public void TestMethod()
    {
    }
}";

        var expected = CSharpAnalyzerVerifier<CultureNameAnalyzer>
            .Diagnostic("NU0018")
            .WithSpan(4, 20, 4, 27)
            .WithArguments("en US");

        await CSharpAnalyzerVerifier<CultureNameAnalyzer>.VerifyAnalyzerAsync(source, expected);
    }

    [Fact]
    public async Task WellFormedNames_ReportNoDiagnosticAsync()
    {
        var source = @"
using NextUnit;

public class Tests
{
    [Test]
    [Culture(""ja-JP"")]
    [UICulture(""sr-Cyrl-RS"")]
    public void Qualified()
    {
    }

    [Test]
    [Culture(""ja"")]
    public void Neutral()
    {
    }

    [Test]
    [Culture(""en_US"")]
    public void Underscore()
    {
    }
}";

        await CSharpAnalyzerVerifier<CultureNameAnalyzer>.VerifyAnalyzerAsync(source);
    }

    /// <summary>
    /// The empty name is the invariant culture, which is exactly what the shorthand expands to.
    /// </summary>
    [Fact]
    public async Task EmptyName_ReportsNoDiagnosticAsync()
    {
        var source = @"
using NextUnit;

public class Tests
{
    [Test]
    [Culture("""")]
    [UICulture("""")]
    public void Invariant()
    {
    }
}";

        await CSharpAnalyzerVerifier<CultureNameAnalyzer>.VerifyAnalyzerAsync(source);
    }

    /// <summary>
    /// Whether a well-formed name matches an installed culture depends on the machine running the
    /// test, so the build must not reject one the build machine happens not to have.
    /// </summary>
    [Fact]
    public async Task UnknownButWellFormedName_ReportsNoDiagnosticAsync()
    {
        var source = @"
using NextUnit;

public class Tests
{
    [Test]
    [Culture(""zz-ZZ"")]
    public void Unknown()
    {
    }
}";

        await CSharpAnalyzerVerifier<CultureNameAnalyzer>.VerifyAnalyzerAsync(source);
    }

    /// <summary>
    /// The generated path never constructs the attribute, so the constructor's
    /// <c>ArgumentNullException</c> never runs. Left unreported, a null name would silently fall
    /// through to the class or assembly culture instead of overriding it.
    /// </summary>
    [Fact]
    public async Task NullName_ReportsDiagnosticAsync()
    {
        var source = @"
#nullable disable
using NextUnit;

public class Tests
{
    [Test]
    [Culture(null)]
    public void TestMethod()
    {
    }
}";

        var expected = CSharpAnalyzerVerifier<CultureNameAnalyzer>
            .Diagnostic("NU0018")
            .WithSpan(8, 14, 8, 18)
            .WithArguments("<null>");

        await CSharpAnalyzerVerifier<CultureNameAnalyzer>.VerifyAnalyzerAsync(source, expected);
    }

    /// <summary>
    /// The attribute is matched by symbol, not by how it is spelled, so an alias is still checked.
    /// </summary>
    [Fact]
    public async Task AliasedCultureAttribute_ReportsDiagnosticAsync()
    {
        var source = @"
using NextUnit;
using Locale = NextUnit.CultureAttribute;

public class Tests
{
    [Test]
    [Locale(""not a culture"")]
    public void TestMethod()
    {
    }
}";

        var expected = CSharpAnalyzerVerifier<CultureNameAnalyzer>
            .Diagnostic("NU0018")
            .WithSpan(8, 13, 8, 28)
            .WithArguments("not a culture");

        await CSharpAnalyzerVerifier<CultureNameAnalyzer>.VerifyAnalyzerAsync(source, expected);
    }

    /// <summary>
    /// A same-named attribute from another namespace is not this rule's business.
    /// </summary>
    [Fact]
    public async Task ForeignCultureAttribute_ReportsNoDiagnosticAsync()
    {
        var source = @"
namespace Other
{
    public sealed class CultureAttribute : System.Attribute
    {
        public CultureAttribute(string name) { }
    }
}

namespace Consumer
{
    using Other;

    public class Tests
    {
        [Culture(""not a culture"")]
        public void TestMethod()
        {
        }
    }
}";

        await CSharpAnalyzerVerifier<CultureNameAnalyzer>.VerifyAnalyzerAsync(source);
    }
}
