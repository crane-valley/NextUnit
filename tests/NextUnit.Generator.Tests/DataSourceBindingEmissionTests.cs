using Microsoft.CodeAnalysis;

namespace NextUnit.Generator.Tests;

/// <summary>
/// Pins which data source members the generator emits direct access for.
/// </summary>
/// <remarks>
/// Every emitted provider is a direct member reference compiled into the consumer's assembly, so a
/// member the generated registry cannot name, or one whose cancellation token the emitted call has
/// no way to supply, has to produce no provider at all. Emitting one anyway fails the consumer's
/// build inside a file they did not write; emitting none leaves the runtime reflection fallback,
/// which reads non-public members, plus the analyzer diagnostic that names the fix.
/// <para>
/// A source whose declaring type is what the registry cannot name is the exception: its type
/// reference has to go too, which leaves the fallback reflecting over the test class and reading a
/// same-named member there. Those get a provider that throws, so the withheld type is named in a
/// message rather than acted on.
/// </para>
/// </remarks>
public class DataSourceBindingEmissionTests
{
    [Fact]
    public async Task PublicMember_EmitsDirectAccessAsync()
    {
        var registry = await GenerateRegistryAsync("""
            using NextUnit;
            using System.Collections.Generic;

            namespace TestProject;

            public class DataTests
            {
                public static IEnumerable<object[]> Rows => new[] { new object[] { 1 } };

                [Test]
                [TestData("Rows")]
                public void Consumes(int value)
                {
                }
            }
            """);

        Assert.Contains("DataSourceProvider = static () => (object?)global::TestProject.DataTests.Rows", registry);
    }

    /// <summary>
    /// The registry is emitted into the assembly being compiled, so internal is in reach and stays
    /// bound. This is the negative half of the accessibility rule.
    /// </summary>
    [Fact]
    public async Task InternalMember_EmitsDirectAccessAsync()
    {
        var registry = await GenerateRegistryAsync("""
            using NextUnit;
            using System.Collections.Generic;

            namespace TestProject;

            public class DataTests
            {
                internal static IEnumerable<object[]> Rows => new[] { new object[] { 1 } };

                [Test]
                [TestData("Rows")]
                public void Consumes(int value)
                {
                }
            }
            """);

        Assert.Contains("DataSourceProvider = static () => (object?)global::TestProject.DataTests.Rows", registry);
    }

    [Fact]
    public async Task PrivateMember_EmitsNoProviderAsync()
    {
        var registry = await GenerateRegistryAsync("""
            using NextUnit;
            using System.Collections.Generic;

            namespace TestProject;

            public class DataTests
            {
                private static IEnumerable<object[]> Rows => new[] { new object[] { 1 } };

                [Test]
                [TestData("Rows")]
                public void Consumes(int value)
                {
                }
            }
            """);

        // The name still reaches the runtime, which resolves it reflectively with
        // BindingFlags.NonPublic; only the statically bound provider is withheld.
        Assert.Contains("DataSourceName = \"Rows\",", registry);
        Assert.Contains("DataSourceProvider = null,", registry);
        Xunit.Assert.DoesNotContain("DataTests.Rows", registry, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PrivateParameterMember_EmitsNoProviderAsync()
    {
        var registry = await GenerateRegistryAsync("""
            using NextUnit;
            using System.Collections.Generic;

            namespace TestProject;

            public class DataTests
            {
                private static IEnumerable<int> Values => new[] { 1, 2, 3 };

                [Test]
                public void Consumes([ValuesFromMember("Values")] int value)
                {
                }
            }
            """);

        Assert.Contains("MemberProvider = null,", registry);
        Xunit.Assert.DoesNotContain("DataTests.Values", registry, StringComparison.Ordinal);
    }

    /// <summary>
    /// A token-taking member returning a type that implements both element interfaces classifies as
    /// synchronous, and the synchronous provider takes no arguments. Emitting it would produce a
    /// call with no argument for a method that requires one.
    /// </summary>
    [Fact]
    public async Task CancellableDualInterfaceMember_EmitsNoProviderAsync()
    {
        var registry = await GenerateRegistryAsync("""
            using NextUnit;
            using System.Collections;
            using System.Collections.Generic;
            using System.Threading;

            namespace TestProject;

            public sealed class DualRows : IEnumerable<object[]>, IAsyncEnumerable<object[]>
            {
                public IEnumerator<object[]> GetEnumerator() => throw new System.NotImplementedException();
                IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
                public IAsyncEnumerator<object[]> GetAsyncEnumerator(CancellationToken cancellationToken = default) =>
                    throw new System.NotImplementedException();
            }

            public class DataTests
            {
                public static DualRows Rows(CancellationToken cancellationToken) => new();

                [Test]
                [TestData("Rows")]
                public void Consumes(int value)
                {
                }
            }
            """);

        Assert.Contains("DataSourceProvider = null,", registry);
        Xunit.Assert.DoesNotContain("DataTests.Rows", registry, StringComparison.Ordinal);
    }

    /// <summary>
    /// An explicit <c>MemberType</c> the registry cannot name is withheld from the descriptor as
    /// well as from the provider. Naming it in a <c>typeof</c> would fail the consumer's build with
    /// CS0122, which is the failure NU0020 exists to replace.
    /// </summary>
    [Fact]
    public async Task PrivateMemberType_EmitsNoTypeReferenceAsync()
    {
        const string source = """
            using NextUnit;
            using System.Collections.Generic;

            namespace TestProject;

            public class DataTests
            {
                private static class Fixtures
                {
                    public static IEnumerable<object[]> Rows => new[] { new object[] { 1 } };
                }

                [Test]
                [TestData("Rows", MemberType = typeof(Fixtures))]
                public void Consumes(int value)
                {
                }
            }
            """;

        var registry = await GenerateRegistryAsync(source);

        Assert.Contains("DataSourceName = \"Rows\",", registry);

        // The withheld type survives only inside the message, never as a type reference.
        Xunit.Assert.DoesNotContain("typeof(global::TestProject.DataTests.Fixtures)", registry, StringComparison.Ordinal);
        Assert.Contains("DataSourceType = typeof(global::TestProject.DataTests),", registry);

        // A provider that throws, rather than none: with none the runtime would reflect over the
        // test class and a same-named member there would silently supply the wrong rows.
        Assert.Contains("DataSourceProvider = static () => throw new global::System.InvalidOperationException(", registry);
        Assert.Contains("is not accessible from the generated test registry", registry);

        await AssertGeneratedOutputCompilesAsync(source);
    }

    [Fact]
    public async Task PrivateMemberTypeOnParameterSource_EmitsNoTypeReferenceAsync()
    {
        const string source = """
            using NextUnit;
            using System.Collections.Generic;

            namespace TestProject;

            public class DataTests
            {
                private static class Fixtures
                {
                    public static IEnumerable<int> Values => new[] { 1, 2, 3 };
                }

                [Test]
                public void Consumes([ValuesFromMember("Values", MemberType = typeof(Fixtures))] int value)
                {
                }
            }
            """;

        var registry = await GenerateRegistryAsync(source);

        Xunit.Assert.DoesNotContain("typeof(global::TestProject.DataTests.Fixtures)", registry, StringComparison.Ordinal);
        Assert.Contains("MemberType = typeof(global::TestProject.DataTests), ", registry);
        Assert.Contains("MemberProvider = static () => throw new global::System.InvalidOperationException(", registry);

        await AssertGeneratedOutputCompilesAsync(source);
    }

    /// <summary>
    /// The test class declares a member of the same name. Withholding the named type must not let
    /// either attribute read that one: the runtime resolves a source with no provider by reflecting
    /// over the test class, so a provider that throws is what keeps the wrong rows out.
    /// </summary>
    [Fact]
    public async Task PrivateMemberTypeCollidingWithTestClassMember_DoesNotBindTheTestClassMemberAsync()
    {
        const string source = """
            using NextUnit;
            using System.Collections.Generic;

            namespace TestProject;

            public class DataTests
            {
                private static class Fixtures
                {
                    public static IEnumerable<object[]> Rows => new[] { new object[] { 1 } };

                    public static IEnumerable<int> Values => new[] { 1 };
                }

                public static IEnumerable<object[]> Rows => new[] { new object[] { 99 } };

                public static IEnumerable<int> Values => new[] { 99 };

                [Test]
                [TestData("Rows", MemberType = typeof(Fixtures))]
                public void Consumes(int value)
                {
                }

                [Test]
                public void ConsumesParameter([ValuesFromMember("Values", MemberType = typeof(Fixtures))] int value)
                {
                }
            }
            """;

        var registry = await GenerateRegistryAsync(source);

        Xunit.Assert.DoesNotContain("global::TestProject.DataTests.Rows", registry, StringComparison.Ordinal);
        Xunit.Assert.DoesNotContain("global::TestProject.DataTests.Values", registry, StringComparison.Ordinal);
        Assert.Contains("DataSourceProvider = static () => throw new global::System.InvalidOperationException(", registry);
        Assert.Contains("MemberProvider = static () => throw new global::System.InvalidOperationException(", registry);

        await AssertGeneratedOutputCompilesAsync(source);
    }

    /// <summary>
    /// A reachable <c>MemberType</c> is still named, so the withholding is scoped to what would not
    /// compile.
    /// </summary>
    [Fact]
    public async Task InternalMemberType_EmitsTypeReferenceAsync()
    {
        const string source = """
            using NextUnit;
            using System.Collections.Generic;

            namespace TestProject;

            internal static class Fixtures
            {
                internal static IEnumerable<object[]> Rows => new[] { new object[] { 1 } };
            }

            public class DataTests
            {
                [Test]
                [TestData("Rows", MemberType = typeof(Fixtures))]
                public void Consumes(int value)
                {
                }
            }
            """;

        var registry = await GenerateRegistryAsync(source);

        Assert.Contains("DataSourceType = typeof(global::TestProject.Fixtures),", registry);
        Assert.Contains("DataSourceProvider = static () => (object?)global::TestProject.Fixtures.Rows", registry);
        Assert.Contains("typeof(global::TestProject.Fixtures))]", registry);

        await AssertGeneratedOutputCompilesAsync(source);
    }

    /// <summary>
    /// A source declared on a base test class is emitted as direct access through the derived type,
    /// which is how the user names it and how C# resolves it. Compiling the output is the half that
    /// matters: the emitted name has to bind to the inherited member.
    /// </summary>
    [Fact]
    public async Task InheritedMember_EmitsDirectAccessAsync()
    {
        var source = """
            using NextUnit;
            using System.Collections.Generic;

            namespace TestProject;

            public class DataTestsBase
            {
                public static IEnumerable<object[]> Rows => new[] { new object[] { 1 } };
            }

            public class DataTests : DataTestsBase
            {
                [Test]
                [TestData("Rows")]
                public void Consumes(int value)
                {
                }
            }
            """;

        var registry = await GenerateRegistryAsync(source);

        Assert.Contains("DataSourceProvider = static () => (object?)global::TestProject.DataTests.Rows", registry);

        await AssertGeneratedOutputCompilesAsync(source);
    }

    /// <summary>
    /// The parameter-level sources reach inherited members through the same walk.
    /// </summary>
    [Fact]
    public async Task InheritedParameterMember_EmitsDirectAccessAsync()
    {
        var source = """
            using NextUnit;
            using System.Collections.Generic;

            namespace TestProject;

            public class DataTestsBase
            {
                public static IEnumerable<int> Values => new[] { 1, 2, 3 };
            }

            public class DataTests : DataTestsBase
            {
                [Test]
                public void Consumes([ValuesFromMember("Values")] int value)
                {
                }
            }
            """;

        var registry = await GenerateRegistryAsync(source);

        Assert.Contains("global::TestProject.DataTests.Values", registry);

        await AssertGeneratedOutputCompilesAsync(source);
    }

    /// <summary>
    /// An inherited member out of reach of the registry is withheld exactly as a local one is, so
    /// the base chain cannot become a way around the accessibility rule.
    /// </summary>
    [Fact]
    public async Task InheritedProtectedMember_EmitsNoProviderAsync()
    {
        var registry = await GenerateRegistryAsync("""
            using NextUnit;
            using System.Collections.Generic;

            namespace TestProject;

            public class DataTestsBase
            {
                protected static IEnumerable<object[]> Rows => new[] { new object[] { 1 } };
            }

            public class DataTests : DataTestsBase
            {
                [Test]
                [TestData("Rows")]
                public void Consumes(int value)
                {
                }
            }
            """);

        Assert.Contains("DataSourceName = \"Rows\",", registry);
        Assert.Contains("DataSourceProvider = null,", registry);
        Xunit.Assert.DoesNotContain("DataTests.Rows", registry, StringComparison.Ordinal);
    }

    /// <summary>
    /// Compiles the user's source together with everything the generator emitted for it. Asserting
    /// on the registry text alone would not catch a type reference emitted somewhere the assertions
    /// do not look, and CS0122 inside generated code is exactly the failure being prevented.
    /// </summary>
    private static async Task AssertGeneratedOutputCompilesAsync(string source)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var compilation = await GeneratorDriverHarness.CreateCompilationAsync(
            source,
            OutputKind.DynamicallyLinkedLibrary,
            cancellationToken);

        GeneratorDriverHarness.CreateDriver(trackIncrementalGeneratorSteps: false)
            .RunGeneratorsAndUpdateCompilation(compilation, out var updated, out _, cancellationToken);

        var errors = updated.GetDiagnostics(cancellationToken)
            .Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .Select(static diagnostic => diagnostic.ToString())
            .ToArray();

        Assert.Empty(errors);
    }

    private static async Task<string> GenerateRegistryAsync(string source)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var compilation = await GeneratorDriverHarness.CreateCompilationAsync(
            source,
            OutputKind.DynamicallyLinkedLibrary,
            cancellationToken);
        var driver = GeneratorDriverHarness.CreateDriver(trackIncrementalGeneratorSteps: false)
            .RunGenerators(compilation, cancellationToken);

        return driver.GetRunResult().Results.Single().GeneratedSources
            .Single(static generated => generated.HintName == "GeneratedTestRegistry.g.cs")
            .SourceText
            .ToString();
    }
}
