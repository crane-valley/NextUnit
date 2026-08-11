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
