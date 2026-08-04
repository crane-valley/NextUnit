using Microsoft.CodeAnalysis.Testing;
using NextUnit.Analyzers.Analyzers;
using NextUnit.Analyzers.Tests.Verifiers;
using Xunit;

namespace NextUnit.Analyzers.Tests;

public class RetryPolicyAccessibilityAnalyzerTests
{
    private const string PolicyBody = @"
    public System.Threading.Tasks.ValueTask<bool> ShouldRetryAsync(NextUnit.RetryContext context) =>
        System.Threading.Tasks.ValueTask.FromResult(true);
";

    [Fact]
    public async Task PrivateNestedPolicy_ReportsDiagnosticAsync()
    {
        var source = @"
using NextUnit;

public class Tests
{
    private sealed class AlwaysRetry : IRetryPolicy
    {" + PolicyBody + @"    }

    [Test]
    [Retry<AlwaysRetry>(3)]
    public void TestMethod()
    {
    }
}";

        var expected = CSharpAnalyzerVerifier<RetryPolicyAccessibilityAnalyzer>
            .Diagnostic("NU0016")
            .WithSpan(13, 6, 13, 27)
            .WithArguments("Tests.AlwaysRetry");

        await CSharpAnalyzerVerifier<RetryPolicyAccessibilityAnalyzer>.VerifyAnalyzerAsync(source, expected);
    }

    /// <summary>
    /// A public policy nested in a private type is just as unreachable, which is why the whole
    /// containing chain is walked rather than only the policy itself.
    /// </summary>
    [Fact]
    public async Task PublicPolicyNestedInPrivateType_ReportsDiagnosticAsync()
    {
        var source = @"
using NextUnit;

public class Tests
{
    private static class Container
    {
        public sealed class AlwaysRetry : IRetryPolicy
        {" + PolicyBody + @"        }
    }

    [Test]
    [Retry<Container.AlwaysRetry>(3)]
    public void TestMethod()
    {
    }
}";

        var expected = CSharpAnalyzerVerifier<RetryPolicyAccessibilityAnalyzer>
            .Diagnostic("NU0016")
            .WithSpan(16, 6, 16, 37)
            .WithArguments("Tests.Container.AlwaysRetry");

        await CSharpAnalyzerVerifier<RetryPolicyAccessibilityAnalyzer>.VerifyAnalyzerAsync(source, expected);
    }

    /// <summary>
    /// A file-local type reports internal accessibility but can only be named inside its own file,
    /// so visibility alone is not the whole test.
    /// </summary>
    [Fact]
    public async Task FileLocalPolicy_ReportsDiagnosticAsync()
    {
        var source = @"
using NextUnit;

file sealed class AlwaysRetry : IRetryPolicy
{" + PolicyBody + @"}

public class Tests
{
    [Test]
    [Retry<AlwaysRetry>(3)]
    public void TestMethod()
    {
    }
}";

        var expected = CSharpAnalyzerVerifier<RetryPolicyAccessibilityAnalyzer>
            .Diagnostic("NU0016")
            .WithSpan(13, 6, 13, 27)
            .WithArguments("AlwaysRetry");

        await CSharpAnalyzerVerifier<RetryPolicyAccessibilityAnalyzer>.VerifyAnalyzerAsync(source, expected);
    }

    /// <summary>
    /// A reachable generic policy still cannot be named when one of its type arguments is not, so the
    /// arguments are walked as well as the containing chain.
    /// </summary>
    [Fact]
    public async Task PolicyWithInaccessibleTypeArgument_ReportsDiagnosticAsync()
    {
        var source = @"
using NextUnit;

public class Tests
{
    private sealed class Secret
    {
    }

    public sealed class Wrapper<T> : IRetryPolicy
    {" + PolicyBody + @"    }

    [Test]
    [Retry<Wrapper<Secret>>(3)]
    public void TestMethod()
    {
    }
}";

        var expected = CSharpAnalyzerVerifier<RetryPolicyAccessibilityAnalyzer>
            .Diagnostic("NU0016")
            .WithSpan(17, 6, 17, 31)
            .WithArguments("Tests.Wrapper<Tests.Secret>");

        await CSharpAnalyzerVerifier<RetryPolicyAccessibilityAnalyzer>.VerifyAnalyzerAsync(source, expected);
    }

    [Fact]
    public async Task PolicyWithAccessibleTypeArgument_NoDiagnosticAsync()
    {
        var source = @"
using NextUnit;

public class Tests
{
    public sealed class Visible
    {
    }

    public sealed class Wrapper<T> : IRetryPolicy
    {" + PolicyBody + @"    }

    [Test]
    [Retry<Wrapper<Visible>>(3)]
    public void TestMethod()
    {
    }
}";

        await CSharpAnalyzerVerifier<RetryPolicyAccessibilityAnalyzer>.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task InternalNestedPolicy_NoDiagnosticAsync()
    {
        var source = @"
using NextUnit;

public class Tests
{
    internal sealed class AlwaysRetry : IRetryPolicy
    {" + PolicyBody + @"    }

    [Test]
    [Retry<AlwaysRetry>(3)]
    public void TestMethod()
    {
    }
}";

        await CSharpAnalyzerVerifier<RetryPolicyAccessibilityAnalyzer>.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task TopLevelPolicy_NoDiagnosticAsync()
    {
        var source = @"
using NextUnit;

public sealed class AlwaysRetry : IRetryPolicy
{" + PolicyBody + @"}

public class Tests
{
    [Test]
    [Retry<AlwaysRetry>(3)]
    public void TestMethod()
    {
    }
}";

        await CSharpAnalyzerVerifier<RetryPolicyAccessibilityAnalyzer>.VerifyAnalyzerAsync(source);
    }

    /// <summary>
    /// An unresolved policy type already has its own compiler error, so NU0016 must not pile a
    /// visibility complaint on top of it.
    /// </summary>
    [Fact]
    public async Task UnresolvedPolicyType_ReportsOnlyTheCompilerErrorAsync()
    {
        var source = @"
using NextUnit;

public class Tests
{
    [Test]
    [Retry<Undefined>(3)]
    public void TestMethod()
    {
    }
}";

        var missingType = DiagnosticResult
            .CompilerError("CS0246")
            .WithSpan(7, 12, 7, 21)
            .WithArguments("Undefined");

        await CSharpAnalyzerVerifier<RetryPolicyAccessibilityAnalyzer>.VerifyAnalyzerAsync(source, missingType);
    }

    [Fact]
    public async Task PlainRetry_NoDiagnosticAsync()
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

        await CSharpAnalyzerVerifier<RetryPolicyAccessibilityAnalyzer>.VerifyAnalyzerAsync(source);
    }
}
