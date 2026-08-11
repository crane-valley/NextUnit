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

        Xunit.Assert.DoesNotContain("Fixtures", registry, StringComparison.Ordinal);
        Assert.Contains("DataSourceName = \"Rows\",", registry);
        Assert.Contains("DataSourceProvider = null,", registry);

        // The descriptor falls back to the test class, which the runtime already does for a
        // descriptor carrying no type of its own, so the id stays stable and the lookup reports the
        // member as missing rather than the build failing in a file the user did not write.
        Assert.Contains("DataSourceType = typeof(global::TestProject.DataTests),", registry);

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

        Xunit.Assert.DoesNotContain("Fixtures", registry, StringComparison.Ordinal);
        Assert.Contains("MemberType = typeof(global::TestProject.DataTests), ", registry);
        Assert.Contains("MemberProvider = null, ", registry);

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
