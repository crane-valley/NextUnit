using NextUnit.Analyzers.Analyzers;
using NextUnit.Analyzers.Tests.Verifiers;
using Xunit;

namespace NextUnit.Analyzers.Tests;

public class MissingTestAttributeAnalyzerTests
{
    [Fact]
    public async Task ArgumentsWithoutTest_ReportsDiagnosticAsync()
    {
        var source = @"
using NextUnit;

public class Tests
{
    [Arguments(1)]
    public void TestMethod(int value)
    {
    }
}";

        var expected = CSharpAnalyzerVerifier<MissingTestAttributeAnalyzer>
            .Diagnostic("NU0013")
            .WithSpan(7, 17, 7, 27)
            .WithArguments("TestMethod", "Arguments");

        await CSharpAnalyzerVerifier<MissingTestAttributeAnalyzer>.VerifyAnalyzerAsync(source, expected);
    }

    [Fact]
    public async Task TestDataWithoutTest_ReportsDiagnosticAsync()
    {
        var source = @"
using NextUnit;

public class Tests
{
    public static int[] Data => new[] { 1 };

    [TestData(nameof(Data))]
    public void TestMethod(int value)
    {
    }
}";

        var expected = CSharpAnalyzerVerifier<MissingTestAttributeAnalyzer>
            .Diagnostic("NU0013")
            .WithSpan(9, 17, 9, 27)
            .WithArguments("TestMethod", "TestData");

        await CSharpAnalyzerVerifier<MissingTestAttributeAnalyzer>.VerifyAnalyzerAsync(source, expected);
    }

    [Fact]
    public async Task MatrixWithoutTest_ReportsDiagnosticAsync()
    {
        var source = @"
using NextUnit;

public class Tests
{
    public void TestMethod([Matrix(1, 2)] int value)
    {
    }
}";

        var expected = CSharpAnalyzerVerifier<MissingTestAttributeAnalyzer>
            .Diagnostic("NU0013")
            .WithSpan(6, 17, 6, 27)
            .WithArguments("TestMethod", "Matrix");

        await CSharpAnalyzerVerifier<MissingTestAttributeAnalyzer>.VerifyAnalyzerAsync(source, expected);
    }

    [Fact]
    public async Task MultipleArgumentsWithoutTest_ReportsOneDiagnosticAsync()
    {
        var source = @"
using NextUnit;

public class Tests
{
    [Arguments(1)]
    [Arguments(2)]
    public void TestMethod(int value)
    {
    }
}";

        var expected = CSharpAnalyzerVerifier<MissingTestAttributeAnalyzer>
            .Diagnostic("NU0013")
            .WithSpan(8, 17, 8, 27)
            .WithArguments("TestMethod", "Arguments");

        await CSharpAnalyzerVerifier<MissingTestAttributeAnalyzer>.VerifyAnalyzerAsync(source, expected);
    }

    [Fact]
    public async Task ClassDataSourceWithoutTest_ReportsDiagnosticAsync()
    {
        var source = @"
using NextUnit;

public class RowSource : System.Collections.Generic.List<int>
{
}

public class Tests
{
    [ClassDataSource<RowSource>]
    public void TestMethod(int value)
    {
    }
}";

        var expected = CSharpAnalyzerVerifier<MissingTestAttributeAnalyzer>
            .Diagnostic("NU0013")
            .WithSpan(11, 17, 11, 27)
            .WithArguments("TestMethod", "ClassDataSource");

        await CSharpAnalyzerVerifier<MissingTestAttributeAnalyzer>.VerifyAnalyzerAsync(source, expected);
    }

    [Fact]
    public async Task ValuesWithoutTest_ReportsDiagnosticAsync()
    {
        var source = @"
using NextUnit;

public class Tests
{
    public void TestMethod([Values(1, 2)] int value)
    {
    }
}";

        var expected = CSharpAnalyzerVerifier<MissingTestAttributeAnalyzer>
            .Diagnostic("NU0013")
            .WithSpan(6, 17, 6, 27)
            .WithArguments("TestMethod", "Values");

        await CSharpAnalyzerVerifier<MissingTestAttributeAnalyzer>.VerifyAnalyzerAsync(source, expected);
    }

    [Fact]
    public async Task ValuesFromMemberWithoutTest_ReportsDiagnosticAsync()
    {
        var source = @"
using NextUnit;

public class Tests
{
    public static int[] Data => new[] { 1 };

    public void TestMethod([ValuesFromMember(nameof(Data))] int value)
    {
    }
}";

        var expected = CSharpAnalyzerVerifier<MissingTestAttributeAnalyzer>
            .Diagnostic("NU0013")
            .WithSpan(8, 17, 8, 27)
            .WithArguments("TestMethod", "ValuesFromMember");

        await CSharpAnalyzerVerifier<MissingTestAttributeAnalyzer>.VerifyAnalyzerAsync(source, expected);
    }

    [Fact]
    public async Task ValuesFromWithoutTest_ReportsDiagnosticAsync()
    {
        var source = @"
using NextUnit;

public class RowSource : System.Collections.Generic.List<int>
{
}

public class Tests
{
    public void TestMethod([ValuesFrom<RowSource>] int value)
    {
    }
}";

        var expected = CSharpAnalyzerVerifier<MissingTestAttributeAnalyzer>
            .Diagnostic("NU0013")
            .WithSpan(10, 17, 10, 27)
            .WithArguments("TestMethod", "ValuesFrom");

        await CSharpAnalyzerVerifier<MissingTestAttributeAnalyzer>.VerifyAnalyzerAsync(source, expected);
    }

    [Fact]
    public async Task ConstructorWithMatrixParameter_NoDiagnosticAsync()
    {
        // Constructors can never carry [Test]; a [Matrix] parameter on one (e.g. a
        // primary-constructor-style parameter) must not be flagged as a false positive.
        var source = @"
using NextUnit;

public class Tests
{
    public Tests([Matrix(1, 2)] int value)
    {
    }
}";

        await CSharpAnalyzerVerifier<MissingTestAttributeAnalyzer>.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task TestWithArguments_NoDiagnosticAsync()
    {
        var source = @"
using NextUnit;

public class Tests
{
    [Test]
    [Arguments(1)]
    public void TestMethod(int value)
    {
    }
}";

        await CSharpAnalyzerVerifier<MissingTestAttributeAnalyzer>.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task TestWithoutDataSource_NoDiagnosticAsync()
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

        await CSharpAnalyzerVerifier<MissingTestAttributeAnalyzer>.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task MethodWithoutAttributes_NoDiagnosticAsync()
    {
        var source = @"
using NextUnit;

public class Tests
{
    public void HelperMethod()
    {
    }
}";

        await CSharpAnalyzerVerifier<MissingTestAttributeAnalyzer>.VerifyAnalyzerAsync(source);
    }
}
