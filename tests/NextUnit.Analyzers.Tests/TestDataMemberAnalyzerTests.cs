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
    public async Task TestDataWithUserDefinedImplicitConversion_ReportsDiagnosticAsync()
    {
        const string source = """
            using NextUnit;
            using System.Collections.Generic;

            public sealed class Source
            {
            }

            public sealed class Target
            {
                public static implicit operator Target(Source source) => new();
            }

            public class Tests
            {
                public static IEnumerable<TestDataRow<Source>> TestCases => [];

                [Test]
                [{|#0:TestData("TestCases")|}]
                public void TestMethod(Target value)
                {
                }
            }
            """;

        var expected = CSharpAnalyzerVerifier<TestDataMemberAnalyzer>
            .Diagnostic("NU0009")
            .WithLocation(0)
            .WithArguments("TestCases", "Source", "TestMethod");

        await CSharpAnalyzerVerifier<TestDataMemberAnalyzer>.VerifyAnalyzerAsync(source, expected);
    }

    [Fact]
    public async Task TestDataWithNestedTupleConversion_ReportsDiagnosticAsync()
    {
        const string source = """
            using NextUnit;
            using System.Collections.Generic;

            public class Tests
            {
                public static IEnumerable<TestDataRow<((int, int), string)>> TestCases => [];

                [Test]
                [{|#0:TestData("TestCases")|}]
                public void TestMethod((long, long) pair, string label)
                {
                }
            }
            """;

        var expected = CSharpAnalyzerVerifier<TestDataMemberAnalyzer>
            .Diagnostic("NU0009")
            .WithLocation(0)
            .WithArguments("TestCases", "((int, int), string)", "TestMethod");

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

    [Fact]
    public async Task TestDataWithCancellableAsyncEnumerable_NoDiagnosticAsync()
    {
        var source = """
            using NextUnit;
            using System.Collections.Generic;
            using System.Runtime.CompilerServices;
            using System.Threading;
            using System.Threading.Tasks;

            public class Tests
            {
                public static async IAsyncEnumerable<object[]> Rows(
                    [EnumeratorCancellation] CancellationToken cancellationToken)
                {
                    await Task.Yield();
                    yield return new object[] { 1 };
                }

                [Test]
                [TestData("Rows")]
                public void TestMethod(int value)
                {
                }
            }
            """;

        await CSharpAnalyzerVerifier<TestDataMemberAnalyzer>.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task TestDataWithTaskWrappedCollection_NoDiagnosticAsync()
    {
        var source = """
            using NextUnit;
            using System.Collections.Generic;
            using System.Threading.Tasks;

            public class Tests
            {
                public static Task<IEnumerable<object[]>> Rows() =>
                    Task.FromResult<IEnumerable<object[]>>(new[] { new object[] { 1 } });

                [Test]
                [TestData("Rows")]
                public void TestMethod(int value)
                {
                }
            }
            """;

        await CSharpAnalyzerVerifier<TestDataMemberAnalyzer>.VerifyAnalyzerAsync(source);
    }

    /// <summary>
    /// The row type of an asynchronous source is checked against the test method exactly as a
    /// synchronous one is.
    /// </summary>
    [Fact]
    public async Task TestDataWithAsyncEnumerableRowTypeMismatch_ReportsDiagnosticAsync()
    {
        var source = """
            using NextUnit;
            using System.Collections.Generic;
            using System.Threading.Tasks;

            public class Tests
            {
                public static async IAsyncEnumerable<string> Rows()
                {
                    await Task.Yield();
                    yield return "value";
                }

                [Test]
                [{|#0:TestData("Rows")|}]
                public void TestMethod(int value)
                {
                }
            }
            """;

        var expected = CSharpAnalyzerVerifier<TestDataMemberAnalyzer>
            .Diagnostic("NU0009")
            .WithLocation(0)
            .WithArguments("Rows", "string", "TestMethod");

        await CSharpAnalyzerVerifier<TestDataMemberAnalyzer>.VerifyAnalyzerAsync(source, expected);
    }

    /// <summary>
    /// A parameterless overload keeps binding even when an unsupported cancellation-aware overload
    /// is declared first, so upgrading cannot turn a working suite into a build error.
    /// </summary>
    [Fact]
    public async Task TestDataWithUnsupportedOverloadFirst_BindsParameterlessOverloadAsync()
    {
        var source = """
            using NextUnit;
            using System.Collections.Generic;
            using System.Threading;
            using System.Threading.Tasks;

            public class Tests
            {
                public static Task Rows(CancellationToken cancellationToken) => Task.CompletedTask;

                public static IEnumerable<object[]> Rows() => new[] { new object[] { 1 } };

                [Test]
                [TestData("Rows")]
                public void TestMethod(int value)
                {
                }
            }
            """;

        await CSharpAnalyzerVerifier<TestDataMemberAnalyzer>.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task TestDataWithBareTask_ReportsUnsupportedAwaitableAsync()
    {
        var source = """
            using NextUnit;
            using System.Threading.Tasks;

            public class Tests
            {
                public static Task Rows() => Task.CompletedTask;

                [Test]
                [{|#0:TestData("Rows")|}]
                public void TestMethod(int value)
                {
                }
            }
            """;

        var expected = CSharpAnalyzerVerifier<TestDataMemberAnalyzer>
            .Diagnostic("NU0014")
            .WithLocation(0)
            .WithArguments("Rows", "Task");

        await CSharpAnalyzerVerifier<TestDataMemberAnalyzer>.VerifyAnalyzerAsync(source, expected);
    }

    /// <summary>
    /// A cancellation-aware member whose awaited value supplies no rows is still reported. Staying
    /// silent would leave nothing but a parameter-count failure from the runtime reflection
    /// fallback, since the generator emits no provider for this shape either.
    /// </summary>
    [Fact]
    public async Task TestDataWithCancellableBareTask_ReportsUnsupportedAwaitableAsync()
    {
        var source = """
            using NextUnit;
            using System.Threading;
            using System.Threading.Tasks;

            public class Tests
            {
                public static Task Rows(CancellationToken cancellationToken) => Task.CompletedTask;

                [Test]
                [{|#0:TestData("Rows")|}]
                public void TestMethod(int value)
                {
                }
            }
            """;

        var expected = CSharpAnalyzerVerifier<TestDataMemberAnalyzer>
            .Diagnostic("NU0014")
            .WithLocation(0)
            .WithArguments("Rows", "Task");

        await CSharpAnalyzerVerifier<TestDataMemberAnalyzer>.VerifyAnalyzerAsync(source, expected);
    }

    [Fact]
    public async Task TestDataWithTaskWrappedScalar_ReportsUnsupportedAwaitableAsync()
    {
        var source = """
            using NextUnit;
            using System.Threading.Tasks;

            public class Tests
            {
                public static ValueTask<int> Rows() => new ValueTask<int>(1);

                [Test]
                [{|#0:TestData("Rows")|}]
                public void TestMethod(int value)
                {
                }
            }
            """;

        var expected = CSharpAnalyzerVerifier<TestDataMemberAnalyzer>
            .Diagnostic("NU0014")
            .WithLocation(0)
            .WithArguments("Rows", "ValueTask<int>");

        await CSharpAnalyzerVerifier<TestDataMemberAnalyzer>.VerifyAnalyzerAsync(source, expected);
    }
}
