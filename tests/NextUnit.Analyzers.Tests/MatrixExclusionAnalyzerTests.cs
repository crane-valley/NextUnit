using NextUnit.Analyzers.Analyzers;
using NextUnit.Analyzers.Tests.Verifiers;
using Xunit;

namespace NextUnit.Analyzers.Tests;

public class MatrixExclusionAnalyzerTests
{
    [Fact]
    public async Task MatrixExclusionCountMatches_NoDiagnosticAsync()
    {
        var source = """
            using NextUnit;

            public class Tests
            {
                [Test]
                [MatrixExclusion(1, "a")]
                public void TestMethod([Matrix(1, 2)] int a, [Matrix("a", "b")] string b)
                {
                }
            }
            """;

        await CSharpAnalyzerVerifier<MatrixExclusionAnalyzer>.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task MatrixExclusionTooFewValues_ReportsDiagnosticAsync()
    {
        var source = """
            using NextUnit;

            public class Tests
            {
                [Test]
                [MatrixExclusion(1)]
                public void TestMethod([Matrix(1, 2)] int a, [Matrix("a", "b")] string b)
                {
                }
            }
            """;

        var expected = CSharpAnalyzerVerifier<MatrixExclusionAnalyzer>
            .Diagnostic("NU0008")
            .WithSpan(6, 6, 6, 24)
            .WithArguments(1, 2);

        await CSharpAnalyzerVerifier<MatrixExclusionAnalyzer>.VerifyAnalyzerAsync(source, expected);
    }

    [Fact]
    public async Task MatrixExclusionTooManyValues_ReportsDiagnosticAsync()
    {
        var source = """
            using NextUnit;

            public class Tests
            {
                [Test]
                [MatrixExclusion(1, "a", true)]
                public void TestMethod([Matrix(1, 2)] int a, [Matrix("a", "b")] string b)
                {
                }
            }
            """;

        var expected = CSharpAnalyzerVerifier<MatrixExclusionAnalyzer>
            .Diagnostic("NU0008")
            .WithSpan(6, 6, 6, 35)
            .WithArguments(3, 2);

        await CSharpAnalyzerVerifier<MatrixExclusionAnalyzer>.VerifyAnalyzerAsync(source, expected);
    }

    [Fact]
    public async Task MultipleMatrixExclusions_OneMismatch_ReportsDiagnosticAsync()
    {
        var source = """
            using NextUnit;

            public class Tests
            {
                [Test]
                [MatrixExclusion(1, "a")]
                [MatrixExclusion(2)]
                public void TestMethod([Matrix(1, 2)] int a, [Matrix("a", "b")] string b)
                {
                }
            }
            """;

        var expected = CSharpAnalyzerVerifier<MatrixExclusionAnalyzer>
            .Diagnostic("NU0008")
            .WithSpan(7, 6, 7, 24)
            .WithArguments(1, 2);

        await CSharpAnalyzerVerifier<MatrixExclusionAnalyzer>.VerifyAnalyzerAsync(source, expected);
    }

    [Fact]
    public async Task NoMatrixParameters_NoExclusion_NoDiagnosticAsync()
    {
        var source = """
            using NextUnit;

            public class Tests
            {
                [Test]
                public void TestMethod(int a, string b)
                {
                }
            }
            """;

        await CSharpAnalyzerVerifier<MatrixExclusionAnalyzer>.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task NoMatrixParameters_WithExclusion_NoDiagnosticAsync()
    {
        // If there are no matrix parameters, we don't check exclusions
        // (this would be caught by other validation)
        var source = """
            using NextUnit;

            public class Tests
            {
                [Test]
                [MatrixExclusion(1, "a")]
                public void TestMethod(int a, string b)
                {
                }
            }
            """;

        await CSharpAnalyzerVerifier<MatrixExclusionAnalyzer>.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task SingleMatrixParameter_ExclusionMatches_NoDiagnosticAsync()
    {
        var source = """
            using NextUnit;

            public class Tests
            {
                [Test]
                [MatrixExclusion(1)]
                public void TestMethod([Matrix(1, 2, 3)] int value)
                {
                }
            }
            """;

        await CSharpAnalyzerVerifier<MatrixExclusionAnalyzer>.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task ThreeMatrixParameters_AllExclusionsMatch_NoDiagnosticAsync()
    {
        var source = """
            using NextUnit;

            public class Tests
            {
                [Test]
                [MatrixExclusion(1, "a", true)]
                [MatrixExclusion(2, "b", false)]
                public void TestMethod([Matrix(1, 2)] int a, [Matrix("a", "b")] string b, [Matrix(true, false)] bool c)
                {
                }
            }
            """;

        await CSharpAnalyzerVerifier<MatrixExclusionAnalyzer>.VerifyAnalyzerAsync(source);
    }
}
