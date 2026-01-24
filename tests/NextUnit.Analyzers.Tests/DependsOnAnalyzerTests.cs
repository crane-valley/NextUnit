using NextUnit.Analyzers.Analyzers;
using NextUnit.Analyzers.Tests.Verifiers;
using Xunit;

namespace NextUnit.Analyzers.Tests;

public class DependsOnAnalyzerTests
{
    [Fact]
    public async Task DependsOnExistingMethod_NoDiagnosticAsync()
    {
        var source = """
            using NextUnit;

            public class Tests
            {
                [Test]
                public void FirstTest()
                {
                }

                [Test]
                [DependsOn("FirstTest")]
                public void SecondTest()
                {
                }
            }
            """;

        await CSharpAnalyzerVerifier<DependsOnAnalyzer>.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task DependsOnNonExistentMethod_ReportsDiagnosticAsync()
    {
        var source = """
            using NextUnit;

            public class Tests
            {
                [Test]
                [DependsOn("NonExistentTest")]
                public void TestMethod()
                {
                }
            }
            """;

        var expected = CSharpAnalyzerVerifier<DependsOnAnalyzer>
            .Diagnostic("NU0007")
            .WithSpan(6, 6, 6, 34)
            .WithArguments("NonExistentTest", "Tests");

        await CSharpAnalyzerVerifier<DependsOnAnalyzer>.VerifyAnalyzerAsync(source, expected);
    }

    [Fact]
    public async Task DependsOnMultiple_OneMissing_ReportsDiagnosticAsync()
    {
        var source = """
            using NextUnit;

            public class Tests
            {
                [Test]
                public void FirstTest()
                {
                }

                [Test]
                [DependsOn("FirstTest", "MissingTest")]
                public void TestMethod()
                {
                }
            }
            """;

        var expected = CSharpAnalyzerVerifier<DependsOnAnalyzer>
            .Diagnostic("NU0007")
            .WithSpan(11, 6, 11, 43)
            .WithArguments("MissingTest", "Tests");

        await CSharpAnalyzerVerifier<DependsOnAnalyzer>.VerifyAnalyzerAsync(source, expected);
    }

    [Fact]
    public async Task DependsOnMultiple_AllExist_NoDiagnosticAsync()
    {
        var source = """
            using NextUnit;

            public class Tests
            {
                [Test]
                public void FirstTest()
                {
                }

                [Test]
                public void SecondTest()
                {
                }

                [Test]
                [DependsOn("FirstTest", "SecondTest")]
                public void ThirdTest()
                {
                }
            }
            """;

        await CSharpAnalyzerVerifier<DependsOnAnalyzer>.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task NoDependsOnAttribute_NoDiagnosticAsync()
    {
        var source = """
            using NextUnit;

            public class Tests
            {
                [Test]
                public void TestMethod()
                {
                }
            }
            """;

        await CSharpAnalyzerVerifier<DependsOnAnalyzer>.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task DependsOnNonTestMethod_ReportsDiagnosticAsync()
    {
        var source = """
            using NextUnit;

            public class Tests
            {
                public void NotATest()
                {
                }

                [Test]
                [DependsOn("NotATest")]
                public void TestMethod()
                {
                }
            }
            """;

        var expected = CSharpAnalyzerVerifier<DependsOnAnalyzer>
            .Diagnostic("NU0007")
            .WithSpan(10, 6, 10, 27)
            .WithArguments("NotATest", "Tests");

        await CSharpAnalyzerVerifier<DependsOnAnalyzer>.VerifyAnalyzerAsync(source, expected);
    }

    [Fact]
    public async Task MultipleDependsOnAttributes_AllExist_NoDiagnosticAsync()
    {
        var source = """
            using NextUnit;

            public class Tests
            {
                [Test]
                public void FirstTest()
                {
                }

                [Test]
                public void SecondTest()
                {
                }

                [Test]
                [DependsOn("FirstTest")]
                [DependsOn("SecondTest")]
                public void ThirdTest()
                {
                }
            }
            """;

        await CSharpAnalyzerVerifier<DependsOnAnalyzer>.VerifyAnalyzerAsync(source);
    }
}
