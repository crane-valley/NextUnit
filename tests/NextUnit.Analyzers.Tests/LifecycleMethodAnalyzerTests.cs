using NextUnit.Analyzers.Analyzers;
using NextUnit.Analyzers.Tests.Verifiers;
using Xunit;

namespace NextUnit.Analyzers.Tests;

public class LifecycleMethodAnalyzerTests
{
    [Fact]
    public async Task LifecycleMethodWithThrow_ReportsDiagnosticAsync()
    {
        var source = """
            using NextUnit;
            using System;

            public class Tests
            {
                [Before(LifecycleScope.Test)]
                public void Setup()
                {
                    throw new InvalidOperationException("Setup failed");
                }

                [Test]
                public void TestMethod()
                {
                }
            }
            """;

        var expected = CSharpAnalyzerVerifier<LifecycleMethodAnalyzer>
            .Diagnostic("NU0005")
            .WithSpan(7, 17, 7, 22)
            .WithArguments("Setup");

        await CSharpAnalyzerVerifier<LifecycleMethodAnalyzer>.VerifyAnalyzerAsync(source, expected);
    }

    [Fact]
    public async Task LifecycleMethodWithTryCatch_NoDiagnosticAsync()
    {
        var source = """
            using NextUnit;
            using System;

            public class Tests
            {
                [Before(LifecycleScope.Test)]
                public void Setup()
                {
                    try
                    {
                        throw new InvalidOperationException("Setup failed");
                    }
                    catch (Exception)
                    {
                        // Handle exception
                    }
                }

                [Test]
                public void TestMethod()
                {
                }
            }
            """;

        await CSharpAnalyzerVerifier<LifecycleMethodAnalyzer>.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task LifecycleMethodNoThrow_NoDiagnosticAsync()
    {
        var source = """
            using NextUnit;

            public class Tests
            {
                [Before(LifecycleScope.Test)]
                public void Setup()
                {
                    var x = 1 + 1;
                }

                [Test]
                public void TestMethod()
                {
                }
            }
            """;

        await CSharpAnalyzerVerifier<LifecycleMethodAnalyzer>.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task AfterMethodWithThrow_ReportsDiagnosticAsync()
    {
        var source = """
            using NextUnit;
            using System;

            public class Tests
            {
                [After(LifecycleScope.Test)]
                public void Cleanup()
                {
                    throw new InvalidOperationException("Cleanup failed");
                }

                [Test]
                public void TestMethod()
                {
                }
            }
            """;

        var expected = CSharpAnalyzerVerifier<LifecycleMethodAnalyzer>
            .Diagnostic("NU0005")
            .WithSpan(7, 17, 7, 24)
            .WithArguments("Cleanup");

        await CSharpAnalyzerVerifier<LifecycleMethodAnalyzer>.VerifyAnalyzerAsync(source, expected);
    }

    [Fact]
    public async Task NonLifecycleMethodWithThrow_NoDiagnosticAsync()
    {
        var source = """
            using NextUnit;
            using System;

            public class Tests
            {
                public void HelperMethod()
                {
                    throw new InvalidOperationException("Failed");
                }

                [Test]
                public void TestMethod()
                {
                }
            }
            """;

        await CSharpAnalyzerVerifier<LifecycleMethodAnalyzer>.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task LifecycleMethodWithExpressionBodyThrow_ReportsDiagnosticAsync()
    {
        var source = """
            using NextUnit;
            using System;

            public class Tests
            {
                [Before(LifecycleScope.Test)]
                public void Setup() => throw new InvalidOperationException("Setup failed");

                [Test]
                public void TestMethod()
                {
                }
            }
            """;

        var expected = CSharpAnalyzerVerifier<LifecycleMethodAnalyzer>
            .Diagnostic("NU0005")
            .WithSpan(7, 17, 7, 22)
            .WithArguments("Setup");

        await CSharpAnalyzerVerifier<LifecycleMethodAnalyzer>.VerifyAnalyzerAsync(source, expected);
    }

    [Fact]
    public async Task ClassScopeLifecycleMethodWithThrow_ReportsDiagnosticAsync()
    {
        var source = """
            using NextUnit;
            using System;

            public class Tests
            {
                [Before(LifecycleScope.Class)]
                public static void ClassSetup()
                {
                    throw new InvalidOperationException("Class setup failed");
                }

                [Test]
                public void TestMethod()
                {
                }
            }
            """;

        var expected = CSharpAnalyzerVerifier<LifecycleMethodAnalyzer>
            .Diagnostic("NU0005")
            .WithSpan(7, 24, 7, 34)
            .WithArguments("ClassSetup");

        await CSharpAnalyzerVerifier<LifecycleMethodAnalyzer>.VerifyAnalyzerAsync(source, expected);
    }
}
