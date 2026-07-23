using NextUnit.Analyzers.Analyzers;
using NextUnit.Analyzers.Tests.Verifiers;
using Xunit;

namespace NextUnit.Analyzers.Tests;

public class TestDataMemberAnalyzerTests
{
    [Fact]
    public async Task TestDataWithExistingStaticProperty_NoDiagnosticAsync()
    {
        var source = """
            using NextUnit;
            using System.Collections.Generic;

            public class Tests
            {
                public static IEnumerable<object[]> TestCases => new[] { new object[] { 1 } };

                [Test]
                [TestData("TestCases")]
                public void TestMethod(int value)
                {
                }
            }
            """;

        await CSharpAnalyzerVerifier<TestDataMemberAnalyzer>.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task TestDataWithNonExistentMember_ReportsDiagnosticAsync()
    {
        var source = """
            using NextUnit;

            public class Tests
            {
                [Test]
                [TestData("NonExistentMember")]
                public void TestMethod(int value)
                {
                }
            }
            """;

        var expected = CSharpAnalyzerVerifier<TestDataMemberAnalyzer>
            .Diagnostic("NU0003")
            .WithSpan(6, 6, 6, 35)
            .WithArguments("NonExistentMember", "Tests");

        await CSharpAnalyzerVerifier<TestDataMemberAnalyzer>.VerifyAnalyzerAsync(source, expected);
    }

    [Fact]
    public async Task TestDataWithInstanceProperty_ReportsDiagnosticAsync()
    {
        var source = """
            using NextUnit;
            using System.Collections.Generic;

            public class Tests
            {
                public IEnumerable<object[]> TestCases => new[] { new object[] { 1 } };

                [Test]
                [TestData("TestCases")]
                public void TestMethod(int value)
                {
                }
            }
            """;

        var expected = CSharpAnalyzerVerifier<TestDataMemberAnalyzer>
            .Diagnostic("NU0003")
            .WithSpan(9, 6, 9, 27)
            .WithArguments("TestCases", "Tests");

        await CSharpAnalyzerVerifier<TestDataMemberAnalyzer>.VerifyAnalyzerAsync(source, expected);
    }

    [Fact]
    public async Task TestDataWithPrivateStaticMember_NoDiagnosticAsync()
    {
        // Private static members are valid data sources (runtime uses BindingFlags.NonPublic)
        var source = """
            using NextUnit;
            using System.Collections.Generic;

            public class Tests
            {
                private static IEnumerable<object[]> TestCases => new[] { new object[] { 1 } };

                [Test]
                [TestData("TestCases")]
                public void TestMethod(int value)
                {
                }
            }
            """;

        await CSharpAnalyzerVerifier<TestDataMemberAnalyzer>.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task TestDataWithStaticMethod_NoDiagnosticAsync()
    {
        var source = """
            using NextUnit;
            using System.Collections.Generic;

            public class Tests
            {
                public static IEnumerable<object[]> GetTestCases() => new[] { new object[] { 1 } };

                [Test]
                [TestData("GetTestCases")]
                public void TestMethod(int value)
                {
                }
            }
            """;

        await CSharpAnalyzerVerifier<TestDataMemberAnalyzer>.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task ValuesFromMemberWithExistingMember_NoDiagnosticAsync()
    {
        var source = """
            using NextUnit;
            using System.Collections.Generic;

            public class Tests
            {
                public static IEnumerable<int> Values => new[] { 1, 2, 3 };

                [Test]
                public void TestMethod([ValuesFromMember("Values")] int value)
                {
                }
            }
            """;

        await CSharpAnalyzerVerifier<TestDataMemberAnalyzer>.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task ValuesFromMemberWithNonExistentMember_ReportsDiagnosticAsync()
    {
        var source = """
            using NextUnit;

            public class Tests
            {
                [Test]
                public void TestMethod([ValuesFromMember("NonExistent")] int value)
                {
                }
            }
            """;

        var expected = CSharpAnalyzerVerifier<TestDataMemberAnalyzer>
            .Diagnostic("NU0003")
            .WithSpan(6, 29, 6, 60)
            .WithArguments("NonExistent", "Tests");

        await CSharpAnalyzerVerifier<TestDataMemberAnalyzer>.VerifyAnalyzerAsync(source, expected);
    }

    [Fact]
    public async Task NoTestDataAttribute_NoDiagnosticAsync()
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

        await CSharpAnalyzerVerifier<TestDataMemberAnalyzer>.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task TestDataWithStaticField_NoDiagnosticAsync()
    {
        var source = """
            using NextUnit;
            using System.Collections.Generic;

            public class Tests
            {
                public static readonly object[][] TestCases = new[] { new object[] { 1 } };

                [Test]
                [TestData("TestCases")]
                public void TestMethod(int value)
                {
                }
            }
            """;

        await CSharpAnalyzerVerifier<TestDataMemberAnalyzer>.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task TestDataWithCompatibleTypedTupleRow_NoDiagnosticAsync()
    {
        var source = """
            using NextUnit;
            using System.Collections.Generic;

            public class Tests
            {
                public static IEnumerable<TestDataRow<(int, string)>> TestCases => [];

                [Test]
                [TestData("TestCases")]
                public void TestMethod(int value, string text)
                {
                }
            }
            """;

        await CSharpAnalyzerVerifier<TestDataMemberAnalyzer>.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task TestDataWithIncompatibleTypedRow_ReportsDiagnosticAsync()
    {
        var source = """
            using NextUnit;
            using System.Collections.Generic;

            public class Tests
            {
                public static IEnumerable<TestDataRow<string>> TestCases => [];

                [Test]
                [{|#0:TestData("TestCases")|}]
                public void TestMethod(int value)
                {
                }
            }
            """;

        var expected = CSharpAnalyzerVerifier<TestDataMemberAnalyzer>
            .Diagnostic("NU0009")
            .WithLocation(0)
            .WithArguments("TestCases", "string", "TestMethod");

        await CSharpAnalyzerVerifier<TestDataMemberAnalyzer>.VerifyAnalyzerAsync(source, expected);
    }

    [Fact]
    public async Task ClassDataSourceWithIncompatibleTypedTupleRow_ReportsDiagnosticAsync()
    {
        var source = """
            using NextUnit;
            using System.Collections;
            using System.Collections.Generic;

            public sealed class Rows : IEnumerable<TestDataRow<(int, string)>>
            {
                public IEnumerator<TestDataRow<(int, string)>> GetEnumerator() => throw new System.NotImplementedException();
                IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
            }

            public class Tests
            {
                [Test]
                [{|#0:ClassDataSource<Rows>|}]
                public void TestMethod(int value, int other)
                {
                }
            }
            """;

        var expected = CSharpAnalyzerVerifier<TestDataMemberAnalyzer>
            .Diagnostic("NU0009")
            .WithLocation(0)
            .WithArguments("Rows", "(int, string)", "TestMethod");

        await CSharpAnalyzerVerifier<TestDataMemberAnalyzer>.VerifyAnalyzerAsync(source, expected);
    }
}
