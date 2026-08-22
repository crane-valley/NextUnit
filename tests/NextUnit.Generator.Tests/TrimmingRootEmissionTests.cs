using Microsoft.CodeAnalysis;

namespace NextUnit.Generator.Tests;

/// <summary>
/// Pins which types the registry roots for trimming with <c>[DynamicDependency]</c>.
/// </summary>
/// <remarks>
/// A root holds every member of its type against the trimmer, and it is also a <c>typeof(T)</c>
/// compiled into the consumer's assembly. So a root for a source no row comes from costs published
/// size and, when the source is a private or protected nested type, fails the consumer's build with
/// <c>CS0122</c> in a file they did not write. The roots therefore follow the partition that decides
/// which sources are emitted, not the raw list of declared ones.
/// </remarks>
public class TrimmingRootEmissionTests
{
    [Fact]
    public async Task ClassDataSource_IsRootedAsync()
    {
        var registry = await GenerateRegistryAsync("""
            using NextUnit;
            using System.Collections;
            using System.Collections.Generic;

            namespace TestProject;

            public sealed class Rows : IEnumerable<object[]>
            {
                public IEnumerator<object[]> GetEnumerator() => throw new System.NotImplementedException();

                IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
            }

            public class DataTests
            {
                [Test]
                [ClassDataSource<Rows>]
                public void Consumes(int value)
                {
                }
            }
            """);

        Assert.Contains(RootFor("global::TestProject.Rows"), registry);
    }

    /// <summary>
    /// The parameter-level source wins the partition, so the method-level one expands nothing and is
    /// written into no descriptor. Rooting it would hold a dead type.
    /// </summary>
    [Fact]
    public async Task ClassDataSourceShadowedByParameterSource_IsNotRootedAsync()
    {
        var registry = await GenerateRegistryAsync("""
            using NextUnit;
            using System.Collections;
            using System.Collections.Generic;

            namespace TestProject;

            public sealed class Rows : IEnumerable<object[]>
            {
                public IEnumerator<object[]> GetEnumerator() => throw new System.NotImplementedException();

                IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
            }

            public class DataTests
            {
                [Test]
                [ClassDataSource<Rows>]
                public void Consumes([Values(1, 2)] int value)
                {
                }
            }
            """);

        Xunit.Assert.DoesNotContain(RootFor("global::TestProject.Rows"), registry, StringComparison.Ordinal);
        Assert.Contains(RootFor("global::TestProject.DataTests"), registry);
    }

    /// <summary>
    /// The same shadowing applies to a <c>[TestData]</c> member type, which the partition passes over
    /// for the identical reason.
    /// </summary>
    [Fact]
    public async Task TestDataMemberTypeShadowedByParameterSource_IsNotRootedAsync()
    {
        var registry = await GenerateRegistryAsync("""
            using NextUnit;
            using System.Collections.Generic;

            namespace TestProject;

            public static class Fixtures
            {
                public static IEnumerable<object[]> Rows => new[] { new object[] { 1 } };
            }

            public class DataTests
            {
                [Test]
                [TestData("Rows", MemberType = typeof(Fixtures))]
                public void Consumes([Values(1, 2)] int value)
                {
                }
            }
            """);

        Xunit.Assert.DoesNotContain(RootFor("global::TestProject.Fixtures"), registry, StringComparison.Ordinal);
    }

    /// <summary>
    /// The whole point of dropping the root: a private nested source the registry no longer names
    /// cannot fail the consumer's build with <c>CS0122</c>. Asserting on the registry text alone
    /// would not catch a reference emitted somewhere the assertions do not look.
    /// </summary>
    [Fact]
    public async Task ShadowedPrivateNestedClassDataSource_CompilesAsync()
    {
        await AssertGeneratedOutputCompilesAsync("""
            using NextUnit;
            using System.Collections;
            using System.Collections.Generic;

            namespace TestProject;

            public class DataTests
            {
                private sealed class Rows : IEnumerable<object[]>
                {
                    public IEnumerator<object[]> GetEnumerator() => throw new System.NotImplementedException();

                    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
                }

                [Test]
                [ClassDataSource<Rows>]
                public void Consumes([Values(1, 2)] int value)
                {
                }
            }
            """);
    }

    private static string RootFor(string typeName) =>
        $"DynamicallyAccessedMemberTypes.All, typeof({typeName}))]";

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
}
