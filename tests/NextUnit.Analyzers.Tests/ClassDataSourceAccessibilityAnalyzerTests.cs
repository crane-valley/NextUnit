using Microsoft.CodeAnalysis.Testing;
using NextUnit.Analyzers.Analyzers;
using NextUnit.Analyzers.Tests.Verifiers;
using Xunit;

namespace NextUnit.Analyzers.Tests;

public class ClassDataSourceAccessibilityAnalyzerTests
{
    /// <summary>
    /// The body of a source supplying whole rows, which is what <c>[ClassDataSource]</c> takes.
    /// </summary>
    private const string RowSourceBody = @"
        public System.Collections.Generic.IEnumerator<object[]> GetEnumerator()
        {
            yield return new object[] { 1 };
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
";

    /// <summary>
    /// The body of a source supplying single parameter values, which is what
    /// <c>[ValuesFrom]</c> takes.
    /// </summary>
    private const string ValueSourceBody = @"
        public System.Collections.Generic.IEnumerator<int> GetEnumerator()
        {
            yield return 1;
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
";

    private static DiagnosticResult Expected(string typeName) =>
        CSharpAnalyzerVerifier<ClassDataSourceAccessibilityAnalyzer>
            .Diagnostic("NU0022")
            .WithLocation(0)
            .WithArguments(typeName);

    [Fact]
    public async Task PrivateNestedClassDataSource_ReportsDiagnosticAsync()
    {
        var source = @"
using NextUnit;

public class Tests
{
    private sealed class Rows : System.Collections.Generic.IEnumerable<object[]>
    {" + RowSourceBody + @"    }

    [Test]
    [{|#0:ClassDataSource<Rows>|}]
    public void TestMethod(int value)
    {
    }
}";

        await CSharpAnalyzerVerifier<ClassDataSourceAccessibilityAnalyzer>.VerifyAnalyzerAsync(
            source,
            Expected("Tests.Rows"));
    }

    /// <summary>
    /// A public source nested in a private type is just as unreachable, which is why the whole
    /// containing chain is walked rather than only the source itself.
    /// </summary>
    [Fact]
    public async Task PublicSourceNestedInPrivateType_ReportsDiagnosticAsync()
    {
        var source = @"
using NextUnit;

public class Tests
{
    private static class Container
    {
        public sealed class Rows : System.Collections.Generic.IEnumerable<object[]>
        {" + RowSourceBody + @"        }
    }

    [Test]
    [{|#0:ClassDataSource<Container.Rows>|}]
    public void TestMethod(int value)
    {
    }
}";

        await CSharpAnalyzerVerifier<ClassDataSourceAccessibilityAnalyzer>.VerifyAnalyzerAsync(
            source,
            Expected("Tests.Container.Rows"));
    }

    /// <summary>
    /// A file-local type reports internal accessibility but can only be named inside its own file,
    /// so visibility alone is not the whole test.
    /// </summary>
    [Fact]
    public async Task FileLocalClassDataSource_ReportsDiagnosticAsync()
    {
        var source = @"
using NextUnit;

file sealed class Rows : System.Collections.Generic.IEnumerable<object[]>
{" + RowSourceBody + @"}

public class Tests
{
    [Test]
    [{|#0:ClassDataSource<Rows>|}]
    public void TestMethod(int value)
    {
    }
}";

        await CSharpAnalyzerVerifier<ClassDataSourceAccessibilityAnalyzer>.VerifyAnalyzerAsync(
            source,
            Expected("Rows"));
    }

    /// <summary>
    /// A reachable generic source still cannot be named when one of its type arguments is not, so
    /// the arguments are walked as well as the containing chain.
    /// </summary>
    [Fact]
    public async Task SourceWithInaccessibleTypeArgument_ReportsDiagnosticAsync()
    {
        var source = @"
using NextUnit;

public class Tests
{
    private sealed class Secret
    {
    }

    public sealed class Rows<T> : System.Collections.Generic.IEnumerable<object[]>
    {" + RowSourceBody + @"    }

    [Test]
    [{|#0:ClassDataSource<Rows<Secret>>|}]
    public void TestMethod(int value)
    {
    }
}";

        await CSharpAnalyzerVerifier<ClassDataSourceAccessibilityAnalyzer>.VerifyAnalyzerAsync(
            source,
            Expected("Tests.Rows<Tests.Secret>"));
    }

    /// <summary>
    /// A protected source is reachable from the test class that names it and not from the
    /// generated registry, which is the whole reason the compiler accepts the attribute and then
    /// the generated code does not build.
    /// </summary>
    [Fact]
    public async Task ProtectedSourceOnBase_ReportsDiagnosticAsync()
    {
        var source = @"
using NextUnit;

public class TestBase
{
    protected sealed class Rows : System.Collections.Generic.IEnumerable<object[]>
    {" + RowSourceBody + @"    }
}

public class Tests : TestBase
{
    [Test]
    [{|#0:ClassDataSource<Rows>|}]
    public void TestMethod(int value)
    {
    }
}";

        await CSharpAnalyzerVerifier<ClassDataSourceAccessibilityAnalyzer>.VerifyAnalyzerAsync(
            source,
            Expected("TestBase.Rows"));
    }

    /// <summary>
    /// Every type argument becomes its own <c>typeof</c> and its own factory, so each is judged
    /// separately: naming the attribute rather than the type would not say which half to widen.
    /// </summary>
    [Fact]
    public async Task CombinedSources_ReportsOnlyTheUnreachableArgumentAsync()
    {
        var source = @"
using NextUnit;

public class Tests
{
    public sealed class Visible : System.Collections.Generic.IEnumerable<object[]>
    {" + RowSourceBody + @"    }

    private sealed class Hidden : System.Collections.Generic.IEnumerable<object[]>
    {" + RowSourceBody + @"    }

    [Test]
    [{|#0:ClassDataSource<Visible, Hidden>|}]
    public void TestMethod(int value)
    {
    }
}";

        await CSharpAnalyzerVerifier<ClassDataSourceAccessibilityAnalyzer>.VerifyAnalyzerAsync(
            source,
            Expected("Tests.Hidden"));
    }

    [Fact]
    public async Task CombinedSources_ReportsEachUnreachableArgumentAsync()
    {
        var source = @"
using NextUnit;

public class Tests
{
    private sealed class First : System.Collections.Generic.IEnumerable<object[]>
    {" + RowSourceBody + @"    }

    private sealed class Second : System.Collections.Generic.IEnumerable<object[]>
    {" + RowSourceBody + @"    }

    [Test]
    [{|#0:ClassDataSource<First, Second>|}]
    public void TestMethod(int value)
    {
    }
}";

        await CSharpAnalyzerVerifier<ClassDataSourceAccessibilityAnalyzer>.VerifyAnalyzerAsync(
            source,
            Expected("Tests.First"),
            Expected("Tests.Second"));
    }

    [Fact]
    public async Task PrivateNestedValuesFromSource_ReportsDiagnosticAsync()
    {
        var source = @"
using NextUnit;

public class Tests
{
    private sealed class Values : System.Collections.Generic.IEnumerable<int>
    {" + ValueSourceBody + @"    }

    [Test]
    public void TestMethod([{|#0:ValuesFrom<Values>|}] int value)
    {
    }
}";

        await CSharpAnalyzerVerifier<ClassDataSourceAccessibilityAnalyzer>.VerifyAnalyzerAsync(
            source,
            Expected("Tests.Values"));
    }

    [Fact]
    public async Task InternalValuesFromSource_NoDiagnosticAsync()
    {
        var source = @"
using NextUnit;

internal sealed class Values : System.Collections.Generic.IEnumerable<int>
{" + ValueSourceBody + @"}

public class Tests
{
    [Test]
    public void TestMethod([ValuesFrom<Values>] int value)
    {
    }
}";

        await CSharpAnalyzerVerifier<ClassDataSourceAccessibilityAnalyzer>.VerifyAnalyzerAsync(source);
    }

    /// <summary>
    /// The registry is emitted into the test assembly, so internal is reachable there.
    /// </summary>
    [Fact]
    public async Task InternalNestedClassDataSource_NoDiagnosticAsync()
    {
        var source = @"
using NextUnit;

public class Tests
{
    internal sealed class Rows : System.Collections.Generic.IEnumerable<object[]>
    {" + RowSourceBody + @"    }

    [Test]
    [ClassDataSource<Rows>]
    public void TestMethod(int value)
    {
    }
}";

        await CSharpAnalyzerVerifier<ClassDataSourceAccessibilityAnalyzer>.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task TopLevelClassDataSource_NoDiagnosticAsync()
    {
        var source = @"
using NextUnit;

public sealed class Rows : System.Collections.Generic.IEnumerable<object[]>
{" + RowSourceBody + @"}

public class Tests
{
    [Test]
    [ClassDataSource<Rows>(Shared = SharedType.PerClass)]
    public void TestMethod(int value)
    {
    }
}";

        await CSharpAnalyzerVerifier<ClassDataSourceAccessibilityAnalyzer>.VerifyAnalyzerAsync(source);
    }

    /// <summary>
    /// An unresolved source type already has its own compiler error, so NU0022 must not pile a
    /// visibility complaint on top of it.
    /// </summary>
    [Fact]
    public async Task UnresolvedSourceType_ReportsOnlyTheCompilerErrorAsync()
    {
        var source = @"
using NextUnit;

public class Tests
{
    [Test]
    [ClassDataSource<{|#0:Undefined|}>]
    public void TestMethod(int value)
    {
    }
}";

        var missingType = DiagnosticResult
            .CompilerError("CS0246")
            .WithLocation(0)
            .WithArguments("Undefined");

        await CSharpAnalyzerVerifier<ClassDataSourceAccessibilityAnalyzer>.VerifyAnalyzerAsync(source, missingType);
    }

    /// <summary>
    /// The suppression covers a type argument too, since reachability recurses into them: an
    /// unresolved argument must not turn a reachable source into a visibility complaint stacked on
    /// top of the compiler error that actually needs fixing.
    /// </summary>
    [Fact]
    public async Task UnresolvedTypeArgument_ReportsOnlyTheCompilerErrorAsync()
    {
        var source = @"
using NextUnit;

public class Tests
{
    public sealed class Rows<T> : System.Collections.Generic.IEnumerable<object[]>
    {" + RowSourceBody + @"    }

    [Test]
    [ClassDataSource<Rows<{|#0:Undefined|}>>]
    public void TestMethod(int value)
    {
    }
}";

        var missingType = DiagnosticResult
            .CompilerError("CS0246")
            .WithLocation(0)
            .WithArguments("Undefined");

        await CSharpAnalyzerVerifier<ClassDataSourceAccessibilityAnalyzer>.VerifyAnalyzerAsync(source, missingType);
    }

    /// <summary>
    /// <c>[ValuesFromMember]</c> names a member rather than a type and is emitted as member access,
    /// so it belongs to NU0020 and must not be drawn in by the name it shares with
    /// <c>[ValuesFrom&lt;T&gt;]</c>.
    /// </summary>
    [Fact]
    public async Task ValuesFromMember_NoDiagnosticAsync()
    {
        var source = @"
using NextUnit;
using System.Collections.Generic;

public class Tests
{
    private static IEnumerable<int> Values => new[] { 1 };

    [Test]
    public void TestMethod([ValuesFromMember(nameof(Values))] int value)
    {
    }
}";

        await CSharpAnalyzerVerifier<ClassDataSourceAccessibilityAnalyzer>.VerifyAnalyzerAsync(source);
    }

    /// <summary>
    /// A source declared in another assembly and reachable there is reachable from the registry
    /// too, which is the case the assembly-boundary check has to keep quiet about.
    /// </summary>
    [Fact]
    public async Task PublicSourceInReferencedAssembly_NoDiagnosticAsync()
    {
        var library = """
            using System.Collections.Generic;

            public sealed class Rows : IEnumerable<object[]>
            {
                public IEnumerator<object[]> GetEnumerator()
                {
                    yield return new object[] { 1 };
                }

                System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
            }
            """;

        var source = """
            using NextUnit;

            public class Tests
            {
                [Test]
                [ClassDataSource<Rows>]
                public void TestMethod(int value)
                {
                }
            }
            """;

        await CSharpAnalyzerVerifier<ClassDataSourceAccessibilityAnalyzer>.VerifyAnalyzerWithLibraryAsync(
            source,
            library);
    }

    /// <summary>
    /// Internal in a referenced assembly is reachable only through InternalsVisibleTo, and a
    /// single-project test cannot tell that case from internal in the test assembly itself.
    /// </summary>
    [Fact]
    public async Task InternalSourceInAssemblyGrantingAccess_NoDiagnosticAsync()
    {
        var library = """
            using System.Collections.Generic;
            using System.Runtime.CompilerServices;

            [assembly: InternalsVisibleTo("TestProject")]

            internal sealed class Rows : IEnumerable<object[]>
            {
                public IEnumerator<object[]> GetEnumerator()
                {
                    yield return new object[] { 1 };
                }

                System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
            }
            """;

        var source = """
            using NextUnit;

            public class Tests
            {
                [Test]
                [ClassDataSource<Rows>]
                public void TestMethod(int value)
                {
                }
            }
            """;

        await CSharpAnalyzerVerifier<ClassDataSourceAccessibilityAnalyzer>.VerifyAnalyzerWithLibraryAsync(
            source,
            library);
    }

    /// <summary>
    /// A protected source on a base class in another assembly is the same trap as the local one:
    /// the derived test class may name it and the registry may not.
    /// </summary>
    [Fact]
    public async Task ProtectedSourceOnBaseInReferencedAssembly_ReportsDiagnosticAsync()
    {
        var library = """
            using System.Collections.Generic;

            public class TestBase
            {
                protected sealed class Rows : IEnumerable<object[]>
                {
                    public IEnumerator<object[]> GetEnumerator()
                    {
                        yield return new object[] { 1 };
                    }

                    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
                }
            }
            """;

        var source = """
            using NextUnit;

            public class Tests : TestBase
            {
                [Test]
                [{|#0:ClassDataSource<Rows>|}]
                public void TestMethod(int value)
                {
                }
            }
            """;

        await CSharpAnalyzerVerifier<ClassDataSourceAccessibilityAnalyzer>.VerifyAnalyzerWithLibraryAsync(
            source,
            library,
            Expected("TestBase.Rows"));
    }

    /// <summary>
    /// The generator's pipeline starts at <c>[Test]</c>, so a data source attribute on a method
    /// without it emits no <c>typeof</c> to fail on. Reporting it would break a build that has no
    /// generated code to break; the ignored attribute is already reported as NU0013.
    /// </summary>
    [Fact]
    public async Task UnreachableSourceWithoutTestAttribute_NoDiagnosticAsync()
    {
        var source = @"
using NextUnit;

public class Tests
{
    private sealed class Rows : System.Collections.Generic.IEnumerable<object[]>
    {" + RowSourceBody + @"    }

    [ClassDataSource<Rows>]
    public void NotATest(int value)
    {
    }
}";

        await CSharpAnalyzerVerifier<ClassDataSourceAccessibilityAnalyzer>.VerifyAnalyzerAsync(source);
    }

    /// <summary>
    /// A parameter takes its values from the first data source attribute that answers, so an
    /// unreachable source the generator never constructs must not be reported: widening it would
    /// not put it back in play.
    /// </summary>
    [Fact]
    public async Task ValuesFromLosingToInlineValues_NoDiagnosticAsync()
    {
        var source = @"
using NextUnit;

public class Tests
{
    private sealed class Values : System.Collections.Generic.IEnumerable<int>
    {" + ValueSourceBody + @"    }

    [Test]
    public void TestMethod([Values(1, 2), ValuesFrom<Values>] int value)
    {
    }
}";

        await CSharpAnalyzerVerifier<ClassDataSourceAccessibilityAnalyzer>.VerifyAnalyzerAsync(source);
    }

    /// <summary>
    /// Selection is not attribute identity alone: a <c>[ValuesFromMember]</c> naming nothing
    /// supplies nothing, the generator passes over it, and the <c>[ValuesFrom&lt;T&gt;]</c> behind
    /// it is the one that gets constructed.
    /// </summary>
    [Fact]
    public async Task ValuesFromBehindNamelessValuesFromMember_ReportsDiagnosticAsync()
    {
        var source = @"
using NextUnit;

public class Tests
{
    private sealed class Values : System.Collections.Generic.IEnumerable<int>
    {" + ValueSourceBody + @"    }

    [Test]
    public void TestMethod([ValuesFromMember(""""), {|#0:ValuesFrom<Values>|}] int value)
    {
    }
}";

        await CSharpAnalyzerVerifier<ClassDataSourceAccessibilityAnalyzer>.VerifyAnalyzerAsync(
            source,
            Expected("Tests.Values"));
    }

    /// <summary>
    /// A nested type reports the namespace of its outermost container, so a user's own
    /// <c>NextUnit.Container.ValuesAttribute</c> answers to the same name and namespace as
    /// <c>[Values]</c>. It must not win the selection, or the real source behind it would go
    /// unreported while the generator still constructs it.
    /// </summary>
    [Fact]
    public async Task ValuesFromBehindNestedLookalikeValues_ReportsDiagnosticAsync()
    {
        var source = @"
using NextUnit;

namespace NextUnit
{
    public static class Container
    {
        public sealed class ValuesAttribute : System.Attribute
        {
            public ValuesAttribute(params object?[] values)
            {
            }
        }

        public sealed class ValuesFromMemberAttribute : System.Attribute
        {
            public ValuesFromMemberAttribute(string memberName)
            {
            }
        }
    }
}

public class Tests
{
    private sealed class Values : System.Collections.Generic.IEnumerable<int>
    {" + ValueSourceBody + @"    }

    [Test]
    public void TestMethod(
        [Container.Values(1), {|#0:ValuesFrom<Values>|}] int first,
        [Container.ValuesFromMember(""Nope""), {|#1:ValuesFrom<Values>|}] int second)
    {
    }
}";

        var onFirst = CSharpAnalyzerVerifier<ClassDataSourceAccessibilityAnalyzer>
            .Diagnostic("NU0022")
            .WithLocation(0)
            .WithArguments("Tests.Values");
        var onSecond = CSharpAnalyzerVerifier<ClassDataSourceAccessibilityAnalyzer>
            .Diagnostic("NU0022")
            .WithLocation(1)
            .WithArguments("Tests.Values");

        await CSharpAnalyzerVerifier<ClassDataSourceAccessibilityAnalyzer>.VerifyAnalyzerAsync(
            source,
            onFirst,
            onSecond);
    }

    /// <summary>
    /// The same nesting trap on the method-level attribute: a lookalike nested in a type is not
    /// NextUnit's, so nothing is emitted for it and nothing is reported.
    /// </summary>
    [Fact]
    public async Task NestedLookalikeClassDataSource_NoDiagnosticAsync()
    {
        var source = @"
using NextUnit;

namespace NextUnit
{
    public static class Container
    {
        public sealed class ClassDataSourceAttribute<T> : System.Attribute
        {
        }
    }
}

public class Tests
{
    private sealed class Rows : System.Collections.Generic.IEnumerable<object[]>
    {" + RowSourceBody + @"    }

    [Test]
    [Container.ClassDataSource<Rows>]
    public void TestMethod(int value)
    {
    }
}";

        await CSharpAnalyzerVerifier<ClassDataSourceAccessibilityAnalyzer>.VerifyAnalyzerAsync(source);
    }

    /// <summary>
    /// A parameter-level source shadows the method-level one: the generator buckets the test by its
    /// combined parameter sources, warns as NEXTUNIT010 that only those are processed, and emits
    /// neither a descriptor, nor a <c>new T()</c> factory, nor a trimming root for the class source.
    /// Nothing names the type, so the <c>CS0122</c> this rule replaces never arrives and reporting
    /// would reject a test that compiles.
    /// </summary>
    /// <remarks>
    /// Pinned as the paired half of <c>RegistryEmitter.EmitDynamicDependencies</c> computing its
    /// roots from the partition. PR #234 gated this rule while that root was still emitted and had
    /// to revert; if the root ever returns, this test has to flip back to expecting the diagnostic.
    /// </remarks>
    [Fact]
    public async Task ClassDataSourceShadowedByParameterValues_NoDiagnosticAsync()
    {
        var source = @"
using NextUnit;

public class Tests
{
    private sealed class Rows : System.Collections.Generic.IEnumerable<object[]>
    {" + RowSourceBody + @"    }

    [Test]
    [ClassDataSource<Rows>]
    public void TestMethod([Values(1, 2)] int value)
    {
    }
}";

        await CSharpAnalyzerVerifier<ClassDataSourceAccessibilityAnalyzer>.VerifyAnalyzerAsync(source);
    }

    /// <summary>
    /// The gate is method-wide but covers only the method-level sources. A parameter's own
    /// <c>[ValuesFrom&lt;T&gt;]</c> is what the registry expands, so it keeps being reported even
    /// though a sibling parameter's <c>[Values]</c> puts the method in the combined bucket.
    /// </summary>
    [Fact]
    public async Task ValuesFromBesideAnotherParametersValues_ReportsDiagnosticAsync()
    {
        var source = @"
using NextUnit;

public class Tests
{
    private sealed class Rows : System.Collections.Generic.IEnumerable<int>
    {" + ValueSourceBody + @"    }

    [Test]
    public void TestMethod([Values(1, 2)] int first, [{|#0:ValuesFrom<Rows>|}] int second)
    {
    }
}";

        await CSharpAnalyzerVerifier<ClassDataSourceAccessibilityAnalyzer>.VerifyAnalyzerAsync(
            source,
            Expected("Tests.Rows"));
    }

    [Fact]
    public async Task NoDataSourceAttribute_NoDiagnosticAsync()
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

        await CSharpAnalyzerVerifier<ClassDataSourceAccessibilityAnalyzer>.VerifyAnalyzerAsync(source);
    }
}
