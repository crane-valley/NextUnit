using Microsoft.CodeAnalysis.Testing;
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
            .WithArguments("NonExistentMember", "Tests", "");

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
            .WithArguments("TestCases", "Tests", "");

        await CSharpAnalyzerVerifier<TestDataMemberAnalyzer>.VerifyAnalyzerAsync(source, expected);
    }

    /// <summary>
    /// The generated registry emits direct member access, so a private member compiles as written
    /// and then fails the consumer's build with CS0122 inside generated code.
    /// </summary>
    [Fact]
    public async Task TestDataWithPrivateStaticMember_ReportsDiagnosticAsync()
    {
        var source = """
            using NextUnit;
            using System.Collections.Generic;

            public class Tests
            {
                private static IEnumerable<object[]> TestCases => new[] { new object[] { 1 } };

                [Test]
                [{|#0:TestData("TestCases")|}]
                public void TestMethod(int value)
                {
                }
            }
            """;

        var expected = CSharpAnalyzerVerifier<TestDataMemberAnalyzer>
            .Diagnostic("NU0020")
            .WithLocation(0)
            .WithArguments("TestCases", "Tests", "");

        await CSharpAnalyzerVerifier<TestDataMemberAnalyzer>.VerifyAnalyzerAsync(source, expected);
    }

    [Fact]
    public async Task TestDataWithProtectedStaticMember_ReportsDiagnosticAsync()
    {
        var source = """
            using NextUnit;
            using System.Collections.Generic;

            public class Tests
            {
                protected static IEnumerable<object[]> TestCases => new[] { new object[] { 1 } };

                [Test]
                [{|#0:TestData("TestCases")|}]
                public void TestMethod(int value)
                {
                }
            }
            """;

        var expected = CSharpAnalyzerVerifier<TestDataMemberAnalyzer>
            .Diagnostic("NU0020")
            .WithLocation(0)
            .WithArguments("TestCases", "Tests", "");

        await CSharpAnalyzerVerifier<TestDataMemberAnalyzer>.VerifyAnalyzerAsync(source, expected);
    }

    /// <summary>
    /// The registry is emitted into the test assembly itself, so internal is reachable and the rule
    /// has to stop short of it. This is the negative half of NU0020.
    /// </summary>
    [Fact]
    public async Task TestDataWithInternalStaticMember_NoDiagnosticAsync()
    {
        var source = """
            using NextUnit;
            using System.Collections.Generic;

            internal static class Fixtures
            {
                internal static IEnumerable<object[]> TestCases => new[] { new object[] { 1 } };
            }

            public class Tests
            {
                [Test]
                [TestData("TestCases", MemberType = typeof(Fixtures))]
                public void TestMethod(int value)
                {
                }
            }
            """;

        await CSharpAnalyzerVerifier<TestDataMemberAnalyzer>.VerifyAnalyzerAsync(source);
    }

    /// <summary>
    /// A public member of a private nested type is just as unreachable, which is why the containing
    /// type chain is walked and not only the member.
    /// </summary>
    [Fact]
    public async Task TestDataWithMemberInPrivateNestedType_ReportsDiagnosticAsync()
    {
        var source = """
            using NextUnit;
            using System.Collections.Generic;

            public class Tests
            {
                private static class Fixtures
                {
                    public static IEnumerable<object[]> TestCases => new[] { new object[] { 1 } };
                }

                [Test]
                [{|#0:TestData("TestCases", MemberType = typeof(Fixtures))|}]
                public void TestMethod(int value)
                {
                }
            }
            """;

        var expected = CSharpAnalyzerVerifier<TestDataMemberAnalyzer>
            .Diagnostic("NU0020")
            .WithLocation(0)
            .WithArguments("TestCases", "Fixtures", "");

        await CSharpAnalyzerVerifier<TestDataMemberAnalyzer>.VerifyAnalyzerAsync(source, expected);
    }

    /// <summary>
    /// A property is read through its getter, so a visible property with a private getter is not
    /// reachable. The generated read fails with CS0271 rather than CS0122, but it fails all the same.
    /// </summary>
    [Fact]
    public async Task TestDataWithPrivateGetterProperty_ReportsDiagnosticAsync()
    {
        var source = """
            using NextUnit;
            using System.Collections.Generic;

            public class Tests
            {
                public static IEnumerable<object[]> TestCases { private get; set; } =
                    new[] { new object[] { 1 } };

                [Test]
                [{|#0:TestData("TestCases")|}]
                public void TestMethod(int value)
                {
                }
            }
            """;

        var expected = CSharpAnalyzerVerifier<TestDataMemberAnalyzer>
            .Diagnostic("NU0020")
            .WithLocation(0)
            .WithArguments("TestCases", "Tests", "");

        await CSharpAnalyzerVerifier<TestDataMemberAnalyzer>.VerifyAnalyzerAsync(source, expected);
    }

    /// <summary>
    /// A reachable generic container still cannot be named when one of its type arguments cannot,
    /// which is why the type arguments are walked as well as the containing chain.
    /// </summary>
    [Fact]
    public async Task TestDataWithInaccessibleTypeArgument_ReportsDiagnosticAsync()
    {
        var source = """
            using NextUnit;
            using System.Collections.Generic;

            public static class Fixtures<T>
            {
                public static IEnumerable<object[]> TestCases => new[] { new object[] { 1 } };
            }

            public class Tests
            {
                private sealed class Secret
                {
                }

                [Test]
                [{|#0:TestData("TestCases", MemberType = typeof(Fixtures<Secret>))|}]
                public void TestMethod(int value)
                {
                }
            }
            """;

        var expected = CSharpAnalyzerVerifier<TestDataMemberAnalyzer>
            .Diagnostic("NU0020")
            .WithLocation(0)
            .WithArguments("TestCases", "Fixtures", "");

        await CSharpAnalyzerVerifier<TestDataMemberAnalyzer>.VerifyAnalyzerAsync(source, expected);
    }

    /// <summary>
    /// Accessibility is judged before the shape, so a member that is both unreachable and
    /// unsupported reports the accessibility once rather than one rule after the other.
    /// </summary>
    [Fact]
    public async Task TestDataWithPrivateCancellableBareTask_ReportsAccessibilityAsync()
    {
        var source = """
            using NextUnit;
            using System.Threading;
            using System.Threading.Tasks;

            public class Tests
            {
                private static Task Rows(CancellationToken cancellationToken) => Task.CompletedTask;

                [Test]
                [{|#0:TestData("Rows")|}]
                public void TestMethod(int value)
                {
                }
            }
            """;

        var expected = CSharpAnalyzerVerifier<TestDataMemberAnalyzer>
            .Diagnostic("NU0020")
            .WithLocation(0)
            .WithArguments("Rows", "Tests", "");

        await CSharpAnalyzerVerifier<TestDataMemberAnalyzer>.VerifyAnalyzerAsync(source, expected);
    }

    /// <summary>
    /// Parameter-level sources are emitted as direct member access too, so accessibility binds them
    /// the same way.
    /// </summary>
    [Fact]
    public async Task ValuesFromMemberWithPrivateStaticMember_ReportsDiagnosticAsync()
    {
        var source = """
            using NextUnit;
            using System.Collections.Generic;

            public class Tests
            {
                private static IEnumerable<int> Values => new[] { 1, 2, 3 };

                [Test]
                public void TestMethod([{|#0:ValuesFromMember("Values")|}] int value)
                {
                }
            }
            """;

        var expected = CSharpAnalyzerVerifier<TestDataMemberAnalyzer>
            .Diagnostic("NU0020")
            .WithLocation(0)
            .WithArguments("Values", "Tests", "");

        await CSharpAnalyzerVerifier<TestDataMemberAnalyzer>.VerifyAnalyzerAsync(source, expected);
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
            .WithArguments("NonExistent", "Tests", "");

        await CSharpAnalyzerVerifier<TestDataMemberAnalyzer>.VerifyAnalyzerAsync(source, expected);
    }

    /// <summary>
    /// A parameter-level source binds only a parameterless member, so a token-taking overload is out
    /// of its reach whatever its accessibility. NU0020 would name a fix that does not work -- widening
    /// it still leaves it unusable -- so the report is NU0003: the name resolves to nothing this
    /// attribute can expand. It used to report nothing at all, which left the source binding nothing
    /// and failing at discovery with a member-not-found message instead.
    /// </summary>
    [Fact]
    public async Task ValuesFromMemberWithPrivateCancellableOverload_ReportsNotFoundAsync()
    {
        var source = """
            using NextUnit;
            using System.Collections.Generic;
            using System.Threading;
            using System.Threading.Tasks;

            public class Tests
            {
                private static async IAsyncEnumerable<int> Values(CancellationToken cancellationToken)
                {
                    await Task.Yield();
                    yield return 1;
                }

                [Test]
                public void TestMethod([{|#0:ValuesFromMember("Values")|}] int value)
                {
                }
            }
            """;

        var expected = CSharpAnalyzerVerifier<TestDataMemberAnalyzer>
            .Diagnostic("NU0003")
            .WithLocation(0)
            .WithArguments("Values", "Tests", "");

        await CSharpAnalyzerVerifier<TestDataMemberAnalyzer>.VerifyAnalyzerAsync(source, expected);
    }

    /// <summary>
    /// The emitted call names no type argument, so a generic overload is not a candidate however it
    /// is declared. C# binds the non-generic overload for the same call, and so does the resolver.
    /// </summary>
    [Fact]
    public async Task TestDataWithPrivateGenericOverload_BindsNonGenericOverloadAsync()
    {
        var source = """
            using NextUnit;
            using System.Collections.Generic;

            public class Tests
            {
                private static IEnumerable<object[]> Rows<T>() => new[] { new object[] { 1 } };

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

    /// <summary>
    /// Nothing supplies the type argument -- neither the generated call nor the reflection fallback
    /// -- so a member that is only ever generic is reported as missing rather than left to fail at
    /// run time.
    /// </summary>
    [Fact]
    public async Task TestDataWithGenericOnlyMember_ReportsNotFoundAsync()
    {
        var source = """
            using NextUnit;
            using System.Collections.Generic;

            public class Tests
            {
                public static IEnumerable<object[]> Rows<T>() => new[] { new object[] { 1 } };

                [Test]
                [TestData("Rows")]
                public void TestMethod(int value)
                {
                }
            }
            """;

        var expected = CSharpAnalyzerVerifier<TestDataMemberAnalyzer>
            .Diagnostic("NU0003")
            .WithSpan(9, 6, 9, 22)
            .WithArguments("Rows", "Tests", "");

        await CSharpAnalyzerVerifier<TestDataMemberAnalyzer>.VerifyAnalyzerAsync(source, expected);
    }

    [Fact]
    public async Task ValuesFromMemberWithGenericOnlyMember_ReportsNotFoundAsync()
    {
        var source = """
            using NextUnit;
            using System.Collections.Generic;

            public class Tests
            {
                public static IEnumerable<int> Values<T>() => new[] { 1, 2, 3 };

                [Test]
                public void TestMethod([{|#0:ValuesFromMember("Values")|}] int value)
                {
                }
            }
            """;

        var expected = CSharpAnalyzerVerifier<TestDataMemberAnalyzer>
            .Diagnostic("NU0003")
            .WithLocation(0)
            .WithArguments("Values", "Tests", "");

        await CSharpAnalyzerVerifier<TestDataMemberAnalyzer>.VerifyAnalyzerAsync(source, expected);
    }

    /// <summary>
    /// Only NextUnit's own <c>TestDataRow&lt;T&gt;</c> is unwrapped. A user type that happens to
    /// share the name is the row itself, so the mismatch names it rather than its type argument.
    /// </summary>
    [Fact]
    public async Task TestDataWithForeignTestDataRowType_DoesNotUnwrapAsync()
    {
        var source = """
            using NextUnit;
            using System.Collections.Generic;

            namespace Other
            {
                public sealed class TestDataRow<T>
                {
                }
            }

            public class Tests
            {
                public static IEnumerable<Other.TestDataRow<string>> Rows => [];

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
            .WithArguments("Rows", "TestDataRow<string>", "TestMethod");

        await CSharpAnalyzerVerifier<TestDataMemberAnalyzer>.VerifyAnalyzerAsync(source, expected);
    }

    /// <summary>
    /// An unresolved type carries no accessibility to judge and already has its own compiler error,
    /// so NU0020 stays quiet rather than burying it. The type argument is reached through the
    /// recursive walk, which is where the guard has to sit.
    /// </summary>
    [Fact]
    public async Task TestDataWithUnresolvedTypeArgument_ReportsNoAccessibilityDiagnosticAsync()
    {
        var source = """
            using NextUnit;
            using System.Collections.Generic;

            public static class Fixtures<T>
            {
                public static IEnumerable<object[]> Rows => new[] { new object[] { 1 } };
            }

            public class Tests
            {
                [Test]
                [TestData("Rows", MemberType = typeof(Fixtures<{|#0:Missing|}>))]
                public void TestMethod(int value)
                {
                }
            }
            """;

        var expected = DiagnosticResult.CompilerError("CS0246")
            .WithLocation(0)
            .WithArguments("Missing");

        await CSharpAnalyzerVerifier<TestDataMemberAnalyzer>.VerifyAnalyzerAsync(source, expected);
    }

    /// <summary>
    /// An unresolved containing type has no members, so the lookup above reports the reference as
    /// unfound and never reaches the accessibility rule. Pinned because the alternative -- NU0020 on
    /// a type whose accessibility nobody can know -- would bury the compiler error.
    /// </summary>
    [Fact]
    public async Task TestDataWithUnresolvedMemberType_ReportsNoAccessibilityDiagnosticAsync()
    {
        var source = """
            using NextUnit;

            public class Tests
            {
                [Test]
                [{|#1:TestData("Rows", MemberType = typeof({|#0:Missing|}))|}]
                public void TestMethod(int value)
                {
                }
            }
            """;

        var compilerError = DiagnosticResult.CompilerError("CS0246")
            .WithLocation(0)
            .WithArguments("Missing");
        var notFound = CSharpAnalyzerVerifier<TestDataMemberAnalyzer>
            .Diagnostic("NU0003")
            .WithLocation(1)
            .WithArguments("Rows", "Missing", "");

        await CSharpAnalyzerVerifier<TestDataMemberAnalyzer>.VerifyAnalyzerAsync(source, compilerError, notFound);
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

    /// <summary>
    /// An unresolved source type classifies as supplying no statically known row type, so row
    /// validation stops before it can report. Pinned for the same reason the sibling
    /// <c>[TestData]</c> cases are: a row-shape complaint about a type nobody can resolve would
    /// bury the compiler error that actually needs fixing.
    /// </summary>
    [Fact]
    public async Task ClassDataSourceWithUnresolvedTypeArgument_ReportsOnlyTheCompilerErrorAsync()
    {
        var source = """
            using NextUnit;
            using System.Collections;
            using System.Collections.Generic;

            public sealed class Rows<T> : IEnumerable<TestDataRow<(int, string)>>
            {
                public IEnumerator<TestDataRow<(int, string)>> GetEnumerator() => throw new System.NotImplementedException();
                IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
            }

            public class Tests
            {
                [Test]
                [ClassDataSource<{|#0:Missing|}>]
                public void TestMethod(int value, int other)
                {
                }
            }
            """;

        var compilerError = DiagnosticResult.CompilerError("CS0246")
            .WithLocation(0)
            .WithArguments("Missing");

        await CSharpAnalyzerVerifier<TestDataMemberAnalyzer>.VerifyAnalyzerAsync(source, compilerError);
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

    /// <summary>
    /// A source type implementing both element interfaces is read synchronously, so the token has
    /// nowhere to go and the member binds to nothing. Before NU0021 the only symptom was a
    /// parameter-count failure from the runtime reflection fallback.
    /// </summary>
    [Fact]
    public async Task TestDataWithCancellableDualInterfaceSource_ReportsDiagnosticAsync()
    {
        var source = """
            using NextUnit;
            using System.Collections;
            using System.Collections.Generic;
            using System.Threading;

            public sealed class DualRows : IEnumerable<object[]>, IAsyncEnumerable<object[]>
            {
                public IEnumerator<object[]> GetEnumerator() => throw new System.NotImplementedException();
                IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
                public IAsyncEnumerator<object[]> GetAsyncEnumerator(CancellationToken cancellationToken = default) =>
                    throw new System.NotImplementedException();
            }

            public class Tests
            {
                public static DualRows Rows(CancellationToken cancellationToken) => new();

                [Test]
                [{|#0:TestData("Rows")|}]
                public void TestMethod(int value)
                {
                }
            }
            """;

        var expected = CSharpAnalyzerVerifier<TestDataMemberAnalyzer>
            .Diagnostic("NU0021")
            .WithLocation(0)
            .WithArguments("Rows", "DualRows");

        await CSharpAnalyzerVerifier<TestDataMemberAnalyzer>.VerifyAnalyzerAsync(source, expected);
    }

    /// <summary>
    /// The same source type without the token keeps binding synchronously, which is what the
    /// sync-first classification rule promises.
    /// </summary>
    [Fact]
    public async Task TestDataWithDualInterfaceSourceAndNoToken_NoDiagnosticAsync()
    {
        var source = """
            using NextUnit;
            using System.Collections;
            using System.Collections.Generic;
            using System.Threading;

            public sealed class DualRows : IEnumerable<object[]>, IAsyncEnumerable<object[]>
            {
                public IEnumerator<object[]> GetEnumerator() => throw new System.NotImplementedException();
                IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
                public IAsyncEnumerator<object[]> GetAsyncEnumerator(CancellationToken cancellationToken = default) =>
                    throw new System.NotImplementedException();
            }

            public class Tests
            {
                public static DualRows Rows() => new();

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
    /// A source offering both <c>IEnumerable&lt;object[]&gt;</c> and
    /// <c>IEnumerable&lt;TestDataRow&lt;T&gt;&gt;</c> is validated against the typed row, whichever
    /// order the interfaces are enumerated in. If the untyped arm won, the row type would be an
    /// array and nothing would be reported.
    /// </summary>
    [Fact]
    public async Task TestDataWithSyncSourceOfferingTestDataRow_ValidatesTypedRowAsync()
    {
        var source = """
            using NextUnit;
            using System.Collections;
            using System.Collections.Generic;

            public sealed class MixedRows : IEnumerable<object[]>, IEnumerable<TestDataRow<string>>
            {
                IEnumerator<object[]> IEnumerable<object[]>.GetEnumerator() => throw new System.NotImplementedException();
                IEnumerator<TestDataRow<string>> IEnumerable<TestDataRow<string>>.GetEnumerator() => throw new System.NotImplementedException();
                IEnumerator IEnumerable.GetEnumerator() => throw new System.NotImplementedException();
            }

            public class Tests
            {
                public static MixedRows Rows => new();

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
    /// The same precedence applies on the asynchronous path, which selects its element type from a
    /// separate interface walk.
    /// </summary>
    [Fact]
    public async Task TestDataWithAsyncSourceOfferingTestDataRow_ValidatesTypedRowAsync()
    {
        var source = """
            using NextUnit;
            using System.Collections.Generic;
            using System.Threading;

            public sealed class MixedRows : IAsyncEnumerable<object[]>, IAsyncEnumerable<TestDataRow<string>>
            {
                IAsyncEnumerator<object[]> IAsyncEnumerable<object[]>.GetAsyncEnumerator(CancellationToken cancellationToken) =>
                    throw new System.NotImplementedException();
                IAsyncEnumerator<TestDataRow<string>> IAsyncEnumerable<TestDataRow<string>>.GetAsyncEnumerator(CancellationToken cancellationToken) =>
                    throw new System.NotImplementedException();
            }

            public class Tests
            {
                public static MixedRows Rows() => new();

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
    /// Neither candidate is a <c>TestDataRow&lt;T&gt;</c>, so the tie falls to the ordinal
    /// comparison of the fully qualified element type names: <c>Alpha</c> before <c>Beta</c>.
    /// </summary>
    [Fact]
    public async Task TestDataWithTiedRowTypes_SelectsTheOrdinallyFirstAsync()
    {
        var source = """
            using NextUnit;
            using System.Collections;
            using System.Collections.Generic;

            public sealed class Alpha
            {
            }

            public sealed class Beta
            {
            }

            public sealed class TiedRows : IEnumerable<Beta>, IEnumerable<Alpha>
            {
                IEnumerator<Beta> IEnumerable<Beta>.GetEnumerator() => throw new System.NotImplementedException();
                IEnumerator<Alpha> IEnumerable<Alpha>.GetEnumerator() => throw new System.NotImplementedException();
                IEnumerator IEnumerable.GetEnumerator() => throw new System.NotImplementedException();
            }

            public class Tests
            {
                public static TiedRows Rows => new();

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
            .WithArguments("Rows", "Alpha", "TestMethod");

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

    /// <summary>
    /// C# binds <c>Tests.Rows</c> to a member declared on the base class, and so does the generated
    /// registry, so lookup that stopped at the declaring type reported a source that works.
    /// </summary>
    [Fact]
    public async Task TestDataWithInheritedStaticMember_NoDiagnosticAsync()
    {
        var source = """
            using NextUnit;
            using System.Collections.Generic;

            public class TestBase
            {
                public static IEnumerable<object[]> TestCases => new[] { new object[] { 1 } };
            }

            public class Tests : TestBase
            {
                [Test]
                [TestData("TestCases")]
                public void TestMethod(int value)
                {
                }
            }
            """;

        await CSharpAnalyzerVerifier<TestDataMemberAnalyzer>.VerifyAnalyzerAsync(source);
    }

    /// <summary>
    /// A derived instance method hides a base static one of the same signature, so
    /// <c>Tests.TestCases</c> is not a static reference at all. Binding the base member would emit
    /// a call the compiler rejects with CS0120 inside generated code; reporting NU0003 is what the
    /// same declaration produced before the base chain was walked.
    /// </summary>
    [Fact]
    public async Task TestDataWithDerivedInstanceMethodHidingBaseStatic_ReportsNotFoundAsync()
    {
        var source = """
            using NextUnit;
            using System.Collections.Generic;

            public class TestBase
            {
                public static IEnumerable<object[]> TestCases() => new[] { new object[] { 1 } };
            }

            public class Tests : TestBase
            {
                public new IEnumerable<object[]> TestCases() => new[] { new object[] { 2 } };

                [Test]
                [TestData("TestCases")]
                public void TestMethod(int value)
                {
                }
            }
            """;

        var expected = CSharpAnalyzerVerifier<TestDataMemberAnalyzer>
            .Diagnostic("NU0003")
            .WithSpan(14, 6, 14, 27)
            .WithArguments("TestCases", "Tests", ShadowedBy("TestBase", "TestCases"));

        await CSharpAnalyzerVerifier<TestDataMemberAnalyzer>.VerifyAnalyzerAsync(source, expected);
    }

    /// <summary>
    /// Signature hiding covers the token-taking overload too, not only the parameterless one. A
    /// derived instance <c>TestCases(CancellationToken)</c> hides the base static overload, so
    /// emitting a call to it would be a CS0120 inside generated code.
    /// </summary>
    [Fact]
    public async Task TestDataWithDerivedInstanceTokenOverloadHidingBaseStatic_ReportsNotFoundAsync()
    {
        var source = """
            using NextUnit;
            using System.Collections.Generic;
            using System.Threading;

            public class TestBase
            {
                public static IAsyncEnumerable<object[]> TestCases(CancellationToken token) => null!;
            }

            public class Tests : TestBase
            {
                public new IAsyncEnumerable<object[]> TestCases(CancellationToken token) => null!;

                [Test]
                [TestData("TestCases")]
                public void TestMethod(int value)
                {
                }
            }
            """;

        var expected = CSharpAnalyzerVerifier<TestDataMemberAnalyzer>
            .Diagnostic("NU0003")
            .WithSpan(15, 6, 15, 27)
            .WithArguments("TestCases", "Tests", ShadowedBy("TestBase", "TestCases"));

        await CSharpAnalyzerVerifier<TestDataMemberAnalyzer>.VerifyAnalyzerAsync(source, expected);
    }

    /// <summary>
    /// C# reduces the applicable candidates to those declared in the most derived type, so
    /// <c>Tests.TestCases()</c> calls the derived overload with its optional parameter filled in,
    /// not the inherited parameterless one. Binding the base member here would classify one
    /// member's rows while the emitted call ran another's, with nothing to warn the user.
    /// </summary>
    [Fact]
    public async Task TestDataWithDerivedOptionalParameterOverload_ReportsNotFoundAsync()
    {
        var source = """
            using NextUnit;
            using System.Collections.Generic;

            public class TestBase
            {
                public static IEnumerable<object[]> TestCases() => new[] { new object[] { 1 } };
            }

            public class Tests : TestBase
            {
                public static IEnumerable<object[]> TestCases(int count = 1) => new[] { new object[] { count } };

                [Test]
                [TestData("TestCases")]
                public void TestMethod(int value)
                {
                }
            }
            """;

        var expected = CSharpAnalyzerVerifier<TestDataMemberAnalyzer>
            .Diagnostic("NU0003")
            .WithSpan(14, 6, 14, 27)
            .WithArguments("TestCases", "Tests", ShadowedBy("TestBase", "TestCases"));

        await CSharpAnalyzerVerifier<TestDataMemberAnalyzer>.VerifyAnalyzerAsync(source, expected);
    }

    /// <summary>
    /// The same reduction applies to a <c>params</c> overload, which C# binds in its expanded form
    /// with no elements.
    /// </summary>
    [Fact]
    public async Task TestDataWithDerivedParamsOverload_ReportsNotFoundAsync()
    {
        var source = """
            using NextUnit;
            using System.Collections.Generic;

            public class TestBase
            {
                public static IEnumerable<object[]> TestCases() => new[] { new object[] { 1 } };
            }

            public class Tests : TestBase
            {
                public static IEnumerable<object[]> TestCases(params int[] counts) => new[] { new object[] { 2 } };

                [Test]
                [TestData("TestCases")]
                public void TestMethod(int value)
                {
                }
            }
            """;

        var expected = CSharpAnalyzerVerifier<TestDataMemberAnalyzer>
            .Diagnostic("NU0003")
            .WithSpan(14, 6, 14, 27)
            .WithArguments("TestCases", "Tests", ShadowedBy("TestBase", "TestCases"));

        await CSharpAnalyzerVerifier<TestDataMemberAnalyzer>.VerifyAnalyzerAsync(source, expected);
    }

    /// <summary>
    /// A derived overload that cannot be called with no arguments still claims the name, because
    /// the nearest declaring level is the only one considered. C# would fall back to the inherited
    /// parameterless member here; the contract reports the source instead, and the fix is to
    /// declare it on the derived type or rename one of the two.
    /// </summary>
    [Fact]
    public async Task TestDataWithDerivedRequiredParameterOverload_ReportsNotFoundAsync()
    {
        var source = """
            using NextUnit;
            using System.Collections.Generic;

            public class TestBase
            {
                public static IEnumerable<object[]> TestCases() => new[] { new object[] { 1 } };
            }

            public class Tests : TestBase
            {
                public static IEnumerable<object[]> TestCases(int count) => new[] { new object[] { count } };

                [Test]
                [TestData("TestCases")]
                public void TestMethod(int value)
                {
                }
            }
            """;

        var expected = CSharpAnalyzerVerifier<TestDataMemberAnalyzer>
            .Diagnostic("NU0003")
            .WithSpan(14, 6, 14, 27)
            .WithArguments("TestCases", "Tests", ShadowedBy("TestBase", "TestCases"));

        await CSharpAnalyzerVerifier<TestDataMemberAnalyzer>.VerifyAnalyzerAsync(source, expected);
    }

    /// <summary>
    /// An event hides a same-named source on a base class. The compile-time walk sees it because
    /// <c>GetMembers</c> returns events, and this is the guard the runtime fallback relies on: its
    /// candidates are methods, properties, and fields only, because asking reflection for events
    /// costs trimming annotations on every test class. The build has to fail before that path runs.
    /// </summary>
    [Fact]
    public async Task TestDataHiddenByDerivedEvent_ReportsNotFoundAsync()
    {
        var source = """
            using NextUnit;
            using System;
            using System.Collections.Generic;

            public class TestBase
            {
                public static IEnumerable<object[]> TestCases => new[] { new object[] { 1 } };
            }

            public class Tests : TestBase
            {
                public new event EventHandler TestCases
                {
                    add { }
                    remove { }
                }

                [Test]
                [TestData("TestCases")]
                public void TestMethod(int value)
                {
                }
            }
            """;

        var expected = CSharpAnalyzerVerifier<TestDataMemberAnalyzer>
            .Diagnostic("NU0003")
            .WithSpan(19, 6, 19, 27)
            .WithArguments("TestCases", "Tests", ShadowedBy("TestBase", "TestCases"));

        await CSharpAnalyzerVerifier<TestDataMemberAnalyzer>.VerifyAnalyzerAsync(source, expected);
    }

    /// <summary>
    /// A nested type on an intermediate base hides a same-named source further up, and the
    /// compile-time walk sees it because <c>GetMembers</c> returns nested types. This is the guard
    /// on the one place the runtime fallback cannot follow: reflection's <c>FlattenHierarchy</c>
    /// does not return a base class's nested types, so the build has to fail before that path runs.
    /// </summary>
    [Fact]
    public async Task TestDataHiddenByNestedTypeOnIntermediateBase_ReportsNotFoundAsync()
    {
        var source = """
            using NextUnit;
            using System.Collections.Generic;

            public class Root
            {
                public static IEnumerable<object[]> TestCases => new[] { new object[] { 1 } };
            }

            public class Middle : Root
            {
                public new class TestCases
                {
                }
            }

            public class Tests : Middle
            {
                [Test]
                [TestData("TestCases")]
                public void TestMethod(int value)
                {
                }
            }
            """;

        var expected = CSharpAnalyzerVerifier<TestDataMemberAnalyzer>
            .Diagnostic("NU0003")
            .WithSpan(19, 6, 19, 27)
            .WithArguments("TestCases", "Tests", ShadowedBy("Root", "TestCases"));

        await CSharpAnalyzerVerifier<TestDataMemberAnalyzer>.VerifyAnalyzerAsync(source, expected);
    }

    /// <summary>
    /// An <c>internal</c> member on an intermediate base in another assembly claims the name, and
    /// the generated registry cannot reach it, so the source is reported rather than resolved
    /// against the public ancestor further up. C# would bind that ancestor, since the intermediate
    /// is out of scope across the assembly boundary; staging candidates by accessibility to match
    /// is exactly the modeling this contract gave up, and NU0020 names the member to widen.
    /// </summary>
    [Fact]
    public async Task TestDataWithInaccessibleInternalOnIntermediateBase_ReportsInaccessibleAsync()
    {
        var library = """
            using System.Collections.Generic;

            public class Root
            {
                public static IEnumerable<object[]> TestCases => new[] { new object[] { 1 } };
            }

            public class Middle : Root
            {
                internal static new IEnumerable<string> TestCases => new[] { "hidden" };
            }
            """;

        var source = """
            using NextUnit;

            public class Tests : Middle
            {
                [Test]
                [{|#0:TestData("TestCases")|}]
                public void TestMethod(int value)
                {
                }
            }
            """;

        var expected = CSharpAnalyzerVerifier<TestDataMemberAnalyzer>
            .Diagnostic("NU0020")
            .WithLocation(0)
            .WithArguments("TestCases", "Tests", ShadowedBy("Root", "TestCases"));

        await CSharpAnalyzerVerifier<TestDataMemberAnalyzer>.VerifyAnalyzerWithLibraryAsync(
            source,
            library,
            expected);
    }

    /// <summary>
    /// The positive half: inside the declaring assembly the same <c>internal</c> member is visible
    /// to the derived class, so it does hide the ancestor and is reported as unreachable from the
    /// generated registry rather than skipped.
    /// </summary>
    [Fact]
    public async Task TestDataWithInternalOnIntermediateBaseInSameAssembly_BindsIntermediateAsync()
    {
        var source = """
            using NextUnit;
            using System.Collections.Generic;

            public class Root
            {
                public static IEnumerable<object[]> TestCases => new[] { new object[] { 1 } };
            }

            public class Middle : Root
            {
                internal static new IEnumerable<object[]> TestCases => new[] { new object[] { 2 } };
            }

            public class Tests : Middle
            {
                [Test]
                [TestData("TestCases")]
                public void TestMethod(int value)
                {
                }
            }
            """;

        await CSharpAnalyzerVerifier<TestDataMemberAnalyzer>.VerifyAnalyzerAsync(source);
    }

    /// <summary>
    /// A <c>private</c> member on an intermediate base claims the name too. C# member lookup never
    /// sees it and would bind the accessible ancestor; the contract stops at the level that
    /// declares the name and reports it, rather than staging candidates by accessibility.
    /// </summary>
    [Fact]
    public async Task TestDataWithPrivateMemberOnIntermediateBase_ReportsInaccessibleAsync()
    {
        var source = """
            using NextUnit;
            using System.Collections.Generic;

            public class Root
            {
                public static IEnumerable<object[]> TestCases => new[] { new object[] { 1 } };
            }

            public class Middle : Root
            {
                private static IEnumerable<string> TestCases => new[] { "hidden" };
            }

            public class Tests : Middle
            {
                [Test]
                [{|#0:TestData("TestCases")|}]
                public void TestMethod(int value)
                {
                }
            }
            """;

        var expected = CSharpAnalyzerVerifier<TestDataMemberAnalyzer>
            .Diagnostic("NU0020")
            .WithLocation(0)
            .WithArguments("TestCases", "Tests", ShadowedBy("Root", "TestCases"));

        await CSharpAnalyzerVerifier<TestDataMemberAnalyzer>.VerifyAnalyzerAsync(source, expected);
    }

    /// <summary>
    /// The walk stops short of <c>object</c>, whose static members are never bindable, so a name
    /// that happens to match one of them is still reported as missing rather than silently
    /// supplying nothing.
    /// </summary>
    [Fact]
    public async Task TestDataNamingAnObjectStaticMember_ReportsNotFoundAsync()
    {
        var source = """
            using NextUnit;

            public class Tests
            {
                [Test]
                [TestData("ReferenceEquals")]
                public void TestMethod(int value)
                {
                }
            }
            """;

        var expected = CSharpAnalyzerVerifier<TestDataMemberAnalyzer>
            .Diagnostic("NU0003")
            .WithSpan(6, 6, 6, 33)
            .WithArguments("ReferenceEquals", "Tests", "");

        await CSharpAnalyzerVerifier<TestDataMemberAnalyzer>.VerifyAnalyzerAsync(source, expected);
    }

    /// <summary>
    /// An inherited member is reached through the base chain by a parameter-level source too.
    /// </summary>
    [Fact]
    public async Task ValuesFromMemberWithInheritedStaticMember_NoDiagnosticAsync()
    {
        var source = """
            using NextUnit;
            using System.Collections.Generic;

            public class TestBase
            {
                public static IEnumerable<int> Values => new[] { 1, 2, 3 };
            }

            public class Tests : TestBase
            {
                [Test]
                public void TestMethod([ValuesFromMember("Values")] int value)
                {
                }
            }
            """;

        await CSharpAnalyzerVerifier<TestDataMemberAnalyzer>.VerifyAnalyzerAsync(source);
    }

    /// <summary>
    /// A member two levels up is still the one C# binds, so the walk cannot stop at the immediate
    /// base type.
    /// </summary>
    [Fact]
    public async Task TestDataWithMemberOnGrandparent_NoDiagnosticAsync()
    {
        var source = """
            using NextUnit;
            using System.Collections.Generic;

            public class Root
            {
                public static IEnumerable<object[]> TestCases => new[] { new object[] { 1 } };
            }

            public class TestBase : Root
            {
            }

            public class Tests : TestBase
            {
                [Test]
                [TestData("TestCases")]
                public void TestMethod(int value)
                {
                }
            }
            """;

        await CSharpAnalyzerVerifier<TestDataMemberAnalyzer>.VerifyAnalyzerAsync(source);
    }

    /// <summary>
    /// An inherited member that is out of reach of the generated registry is named by NU0020 rather
    /// than reported as missing: the fix is to widen it, not to declare it.
    /// </summary>
    [Fact]
    public async Task TestDataWithInheritedProtectedMember_ReportsAccessibilityDiagnosticAsync()
    {
        var source = """
            using NextUnit;
            using System.Collections.Generic;

            public class TestBase
            {
                protected static IEnumerable<object[]> TestCases => new[] { new object[] { 1 } };
            }

            public class Tests : TestBase
            {
                [Test]
                [{|#0:TestData("TestCases")|}]
                public void TestMethod(int value)
                {
                }
            }
            """;

        var expected = CSharpAnalyzerVerifier<TestDataMemberAnalyzer>
            .Diagnostic("NU0020")
            .WithLocation(0)
            .WithArguments("TestCases", "Tests", "");

        await CSharpAnalyzerVerifier<TestDataMemberAnalyzer>.VerifyAnalyzerAsync(source, expected);
    }

    /// <summary>
    /// A derived declaration shadows the base one, so the row type reported is the derived member's.
    /// Pinning it through NU0009 is what proves which member the resolver picked -- both members
    /// exist, and only the message names the winner.
    /// </summary>
    [Fact]
    public async Task TestDataWithShadowedMember_ValidatesMostDerivedAsync()
    {
        var source = """
            using NextUnit;
            using System.Collections.Generic;

            public class TestBase
            {
                public static IEnumerable<int> TestCases => new[] { 1 };
            }

            public class Tests : TestBase
            {
                public static new IEnumerable<string> TestCases => new[] { "one" };

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

    /// <summary>
    /// The nearest declaring level answers the name, so a derived token-taking overload binds and
    /// the inherited parameterless member is not consulted at all. C# would accumulate both into
    /// one method group and prefer the base overload for a no-argument call; the contract does not
    /// model that accumulation, and validates the member it will actually emit.
    /// </summary>
    [Fact]
    public async Task TestDataWithDerivedTokenOverload_BindsDerivedOverloadAsync()
    {
        var source = """
            using NextUnit;
            using System.Collections.Generic;
            using System.Threading;

            public class TestBase
            {
                public static IEnumerable<string> TestCases() => new[] { "one" };
            }

            public class Tests : TestBase
            {
                public static IAsyncEnumerable<int> TestCases(CancellationToken token) => null!;

                [Test]
                [TestData("TestCases")]
                public void TestMethod(int value)
                {
                }
            }
            """;

        await CSharpAnalyzerVerifier<TestDataMemberAnalyzer>.VerifyAnalyzerAsync(source);
    }

    /// <summary>
    /// The escape hatch is named in the message, because the nearest-declaring-level contract is
    /// not guessable from a report that says the member is missing while the user is looking at a
    /// base class that plainly declares it.
    /// </summary>
    [Fact]
    public async Task TestDataShadowingABaseDeclaration_NamesTheMemberTypeEscapeHatchAsync()
    {
        var source = """
            using NextUnit;
            using System.Collections.Generic;

            public class Fixtures
            {
                public static IEnumerable<object[]> Rows() => new[] { new object[] { 1 } };
            }

            public class Tests : Fixtures
            {
                public static IEnumerable<object[]> Rows(int count) => new[] { new object[] { count } };

                [Test]
                [TestData("Rows")]
                public void TestMethod(int value)
                {
                }
            }
            """;

        var expected = CSharpAnalyzerVerifier<TestDataMemberAnalyzer>
            .Diagnostic("NU0003")
            .WithSpan(14, 6, 14, 22)
            .WithArguments("Rows", "Tests", ShadowedBy("Fixtures", "Rows"));

        await CSharpAnalyzerVerifier<TestDataMemberAnalyzer>.VerifyAnalyzerAsync(source, expected);
    }

    /// <summary>
    /// The hint has to name a level that actually resolves. An intermediate base that declares the
    /// name without offering a usable member is another blocking level, so pointing
    /// <c>MemberType</c> at it would reproduce the same report; the search continues to the level
    /// that binds.
    /// </summary>
    [Fact]
    public async Task TestDataShadowedByTwoLevels_NamesTheBindableOneAsync()
    {
        var source = """
            using NextUnit;
            using System.Collections.Generic;

            public class Root
            {
                public static IEnumerable<object[]> Rows() => new[] { new object[] { 1 } };
            }

            public class Middle : Root
            {
                public static IEnumerable<object[]> Rows(int count) => new[] { new object[] { count } };
            }

            public class Tests : Middle
            {
                public static IEnumerable<object[]> Rows(string name) => new[] { new object[] { 2 } };

                [Test]
                [TestData("Rows")]
                public void TestMethod(int value)
                {
                }
            }
            """;

        var expected = CSharpAnalyzerVerifier<TestDataMemberAnalyzer>
            .Diagnostic("NU0003")
            .WithSpan(19, 6, 19, 22)
            .WithArguments("Rows", "Tests", ShadowedBy("Root", "Rows"));

        await CSharpAnalyzerVerifier<TestDataMemberAnalyzer>.VerifyAnalyzerAsync(source, expected);
    }

    /// <summary>
    /// The suggestion has to compile where it is pasted, so the <c>typeof</c> operand carries the
    /// generic argument and a <c>global::</c> qualification.
    /// </summary>
    [Fact]
    public async Task TestDataShadowingAGenericBase_QualifiesTheSuggestedTypeAsync()
    {
        var source = """
            using NextUnit;
            using System.Collections.Generic;

            public class Fixtures<T>
            {
                public static IEnumerable<object[]> Rows() => new[] { new object[] { 1 } };
            }

            public class Tests : Fixtures<int>
            {
                public static IEnumerable<object[]> Rows(int count) => new[] { new object[] { count } };

                [Test]
                [{|#0:TestData("Rows")|}]
                public void TestMethod(int value)
                {
                }
            }
            """;

        var expected = CSharpAnalyzerVerifier<TestDataMemberAnalyzer>
            .Diagnostic("NU0003")
            .WithLocation(0)
            .WithArguments("Rows", "Tests", ShadowedBy("Fixtures<int>", "global::Fixtures<int>", "Rows"));

        await CSharpAnalyzerVerifier<TestDataMemberAnalyzer>.VerifyAnalyzerAsync(source, expected);
    }

    /// <summary>
    /// A nested base type needs its containing type in the operand, which a bare name would drop.
    /// </summary>
    [Fact]
    public async Task TestDataShadowingANestedBase_QualifiesTheSuggestedTypeAsync()
    {
        var source = """
            using NextUnit;
            using System.Collections.Generic;

            public class Outer
            {
                public class Fixtures
                {
                    public static IEnumerable<object[]> Rows() => new[] { new object[] { 1 } };
                }
            }

            public class Tests : Outer.Fixtures
            {
                public static IEnumerable<object[]> Rows(int count) => new[] { new object[] { count } };

                [Test]
                [{|#0:TestData("Rows")|}]
                public void TestMethod(int value)
                {
                }
            }
            """;

        var expected = CSharpAnalyzerVerifier<TestDataMemberAnalyzer>
            .Diagnostic("NU0003")
            .WithLocation(0)
            // The prose name stays short; the operand is what has to be unambiguous.
            .WithArguments("Rows", "Tests", ShadowedBy("Fixtures", "global::Outer.Fixtures", "Rows"));

        await CSharpAnalyzerVerifier<TestDataMemberAnalyzer>.VerifyAnalyzerAsync(source, expected);
    }

    /// <summary>
    /// A base type in a namespace the test file does not import needs the namespace in the operand,
    /// which is the case a short name silently breaks.
    /// </summary>
    [Fact]
    public async Task TestDataShadowingANamespacedBase_QualifiesTheSuggestedTypeAsync()
    {
        var source = """
            using NextUnit;
            using System.Collections.Generic;

            namespace Fixtures.Library
            {
                public class Shared
                {
                    public static IEnumerable<object[]> Rows() => new[] { new object[] { 1 } };
                }
            }

            public class Tests : Fixtures.Library.Shared
            {
                public static IEnumerable<object[]> Rows(int count) => new[] { new object[] { count } };

                [Test]
                [{|#0:TestData("Rows")|}]
                public void TestMethod(int value)
                {
                }
            }
            """;

        var expected = CSharpAnalyzerVerifier<TestDataMemberAnalyzer>
            .Diagnostic("NU0003")
            .WithLocation(0)
            .WithArguments("Rows", "Tests", ShadowedBy("Shared", "global::Fixtures.Library.Shared", "Rows"));

        await CSharpAnalyzerVerifier<TestDataMemberAnalyzer>.VerifyAnalyzerAsync(source, expected);
    }

    /// <summary>
    /// A token-taking member returning a plainly synchronous collection binds to nothing: the
    /// synchronous provider takes no arguments, so the token has nowhere to go. It used to report
    /// nothing at all and fail at discovery with a member-not-found message; NU0021 says what is
    /// actually wrong with it.
    /// </summary>
    [Fact]
    public async Task TestDataWithTokenOnPlainSynchronousSource_ReportsSyncSourceAsync()
    {
        var source = """
            using NextUnit;
            using System.Collections.Generic;
            using System.Threading;

            public class Tests
            {
                public static IEnumerable<int> Rows(CancellationToken token) => new[] { 1 };

                [Test]
                [{|#0:TestData("Rows")|}]
                public void TestMethod(int value)
                {
                }
            }
            """;

        var expected = CSharpAnalyzerVerifier<TestDataMemberAnalyzer>
            .Diagnostic("NU0021")
            .WithLocation(0)
            .WithArguments("Rows", "IEnumerable<int>");

        await CSharpAnalyzerVerifier<TestDataMemberAnalyzer>.VerifyAnalyzerAsync(source, expected);
    }

    /// <summary>
    /// Classification reports a scalar as synchronous too, so NU0021 must not be reached by it: the
    /// member does not return a collection, and dropping the token would not make one. It is
    /// reported as unusable instead.
    /// </summary>
    [Fact]
    public async Task TestDataWithTokenOnScalarReturn_ReportsNotFoundAsync()
    {
        var source = """
            using NextUnit;
            using System.Threading;

            public class Tests
            {
                public static int Rows(CancellationToken token) => 1;

                [Test]
                [{|#0:TestData("Rows")|}]
                public void TestMethod(int value)
                {
                }
            }
            """;

        var expected = CSharpAnalyzerVerifier<TestDataMemberAnalyzer>
            .Diagnostic("NU0003")
            .WithLocation(0)
            .WithArguments("Rows", "Tests", "");

        await CSharpAnalyzerVerifier<TestDataMemberAnalyzer>.VerifyAnalyzerAsync(source, expected);
    }

    /// <summary>
    /// The same for a member that returns nothing at all.
    /// </summary>
    [Fact]
    public async Task TestDataWithTokenOnVoidReturn_ReportsNotFoundAsync()
    {
        var source = """
            using NextUnit;
            using System.Threading;

            public class Tests
            {
                public static void Rows(CancellationToken token)
                {
                }

                [Test]
                [{|#0:TestData("Rows")|}]
                public void TestMethod(int value)
                {
                }
            }
            """;

        var expected = CSharpAnalyzerVerifier<TestDataMemberAnalyzer>
            .Diagnostic("NU0003")
            .WithLocation(0)
            .WithArguments("Rows", "Tests", "");

        await CSharpAnalyzerVerifier<TestDataMemberAnalyzer>.VerifyAnalyzerAsync(source, expected);
    }

    /// <summary>
    /// A parameter-level source cannot expand an awaitable either, and that path never reached the
    /// row-type validation that reports it. Now the issue carries the report, so neither attribute
    /// kind can bind nothing quietly.
    /// </summary>
    [Fact]
    public async Task ValuesFromMemberWithUnsupportedAwaitable_ReportsUnsupportedAwaitableAsync()
    {
        var source = """
            using NextUnit;
            using System.Threading.Tasks;

            public class Tests
            {
                public static Task Values() => Task.CompletedTask;

                [Test]
                public void TestMethod([{|#0:ValuesFromMember("Values")|}] int value)
                {
                }
            }
            """;

        var expected = CSharpAnalyzerVerifier<TestDataMemberAnalyzer>
            .Diagnostic("NU0014")
            .WithLocation(0)
            .WithArguments("Values", "Task");

        await CSharpAnalyzerVerifier<TestDataMemberAnalyzer>.VerifyAnalyzerAsync(source, expected);
    }

    /// <summary>
    /// A base declaring an awaitable that supplies no rows is no fix either: the generator emits
    /// nothing for that shape, so pointing <c>MemberType</c> at it trades one report for another.
    /// The resolution result now carries that as an issue, so the hint declines it without a rule
    /// about awaitables of its own.
    /// </summary>
    [Fact]
    public async Task TestDataShadowingAnUnsupportedAwaitableBase_OmitsTheHintAsync()
    {
        var source = """
            using NextUnit;
            using System.Collections.Generic;
            using System.Threading.Tasks;

            public class Fixtures
            {
                public static Task Rows() => Task.CompletedTask;
            }

            public class Tests : Fixtures
            {
                public static IEnumerable<object[]> Rows(int count) => new[] { new object[] { count } };

                [Test]
                [{|#0:TestData("Rows")|}]
                public void TestMethod(int value)
                {
                }
            }
            """;

        var expected = CSharpAnalyzerVerifier<TestDataMemberAnalyzer>
            .Diagnostic("NU0003")
            .WithLocation(0)
            .WithArguments("Rows", "Tests", "");

        await CSharpAnalyzerVerifier<TestDataMemberAnalyzer>.VerifyAnalyzerAsync(source, expected);
    }

    /// <summary>
    /// The same shape at the level the attribute names still reports NU0014 against the member
    /// itself. Recording it as a binding issue must not cost the diagnostic its subject, which is
    /// the reason the resolver returns the member at all.
    /// </summary>
    [Fact]
    public async Task TestDataWithBareTaskMember_StillReportsUnsupportedAwaitableAsync()
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
    /// A token-taking member whose return type is a plainly synchronous collection binds to
    /// nothing -- the synchronous provider has no token to pass -- so a base declaring only that is
    /// no fix even for a <c>[TestData]</c> source. The hint asks the resolver rather than testing
    /// the member's shape, which is what makes this case fall out without a rule of its own.
    /// </summary>
    [Fact]
    public async Task TestDataShadowingASyncReturningTokenBase_OmitsTheHintAsync()
    {
        var source = """
            using NextUnit;
            using System.Collections.Generic;
            using System.Threading;

            public class Fixtures
            {
                public static IEnumerable<int> Rows(CancellationToken token) => new[] { 1 };
            }

            public class Tests : Fixtures
            {
                public static IEnumerable<object[]> Rows(int count) => new[] { new object[] { count } };

                [Test]
                [{|#0:TestData("Rows")|}]
                public void TestMethod(int value)
                {
                }
            }
            """;

        var expected = CSharpAnalyzerVerifier<TestDataMemberAnalyzer>
            .Diagnostic("NU0003")
            .WithLocation(0)
            .WithArguments("Rows", "Tests", "");

        await CSharpAnalyzerVerifier<TestDataMemberAnalyzer>.VerifyAnalyzerAsync(source, expected);
    }

    /// <summary>
    /// A parameter-level source expands synchronous collections only, so a base declaring nothing
    /// but a token-taking overload is not a fix for it. Suggesting that type would report the same
    /// thing again.
    /// </summary>
    [Fact]
    public async Task ValuesFromMemberShadowingATokenTakingBase_OmitsTheHintAsync()
    {
        var source = """
            using NextUnit;
            using System.Collections.Generic;
            using System.Threading;

            public class Fixtures
            {
                public static IEnumerable<int> Values(CancellationToken token) => new[] { 1 };
            }

            public class Tests : Fixtures
            {
                public static IEnumerable<int> Values(int count) => new[] { count };

                [Test]
                public void TestMethod([{|#0:ValuesFromMember("Values")|}] int value)
                {
                }
            }
            """;

        var expected = CSharpAnalyzerVerifier<TestDataMemberAnalyzer>
            .Diagnostic("NU0003")
            .WithLocation(0)
            .WithArguments("Values", "Tests", "");

        await CSharpAnalyzerVerifier<TestDataMemberAnalyzer>.VerifyAnalyzerAsync(source, expected);
    }

    /// <summary>
    /// The hint is conditional on a farther type actually declaring the name, not merely on the
    /// test class having a base type. A plain misspelling gets the plain message.
    /// </summary>
    [Fact]
    public async Task TestDataWithNoBaseDeclaration_OmitsTheEscapeHatchHintAsync()
    {
        var source = """
            using NextUnit;
            using System.Collections.Generic;

            public class Fixtures
            {
                public static IEnumerable<object[]> Rows() => new[] { new object[] { 1 } };
            }

            public class Tests : Fixtures
            {
                [Test]
                [TestData("Rowz")]
                public void TestMethod(int value)
                {
                }
            }
            """;

        var expected = CSharpAnalyzerVerifier<TestDataMemberAnalyzer>
            .Diagnostic("NU0003")
            .WithSpan(12, 6, 12, 22)
            .WithArguments("Rowz", "Tests", "");

        await CSharpAnalyzerVerifier<TestDataMemberAnalyzer>.VerifyAnalyzerAsync(source, expected);
    }

    /// <summary>
    /// The sentence the analyzer appends when a base type also declares the name, pointing at the
    /// escape hatch. Duplicated from the analyzer on purpose: the message is what the user reads,
    /// so a change to it should have to be made deliberately in both places.
    /// </summary>
    private static string ShadowedBy(string declaringType, string memberName) =>
        ShadowedBy(declaringType, $"global::{declaringType}", memberName);

    /// <summary>
    /// The prose name and the <c>typeof</c> operand differ on purpose: the operand has to compile
    /// where it is pasted, so it is fully qualified.
    /// </summary>
    private static string ShadowedBy(string readableName, string typeOfOperand, string memberName) =>
        $". Type '{readableName}' also declares '{memberName}', but only the nearest type " +
        $"declaring that name is used; set MemberType = typeof({typeOfOperand}) to bind it directly";
}
