using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;

namespace NextUnit.Generator.Tests;

/// <summary>
/// Pins the full text of every file the generator emits for a representative matrix
/// of emission paths.
/// </summary>
/// <remarks>
/// These snapshots are the safety net for generator refactoring: emitted output must stay
/// byte-identical. To update a baseline, run the failing test and copy the actual text from
/// the assertion diff into the matching file under <c>Snapshots/</c>.
/// </remarks>
public class GeneratorSnapshotTests
{
    [Fact]
    public Task PlainTest_MatchesSnapshotAsync() =>
        VerifySnapshotAsync("PlainTest", GeneratorSnapshotSources.PlainTest);

    [Fact]
    public Task ArgumentsTest_MatchesSnapshotAsync() =>
        VerifySnapshotAsync("ArgumentsTest", GeneratorSnapshotSources.ArgumentsTest);

    [Fact]
    public Task MatrixTest_MatchesSnapshotAsync() =>
        VerifySnapshotAsync("MatrixTest", GeneratorSnapshotSources.MatrixTest);

    [Fact]
    public Task TestDataTest_MatchesSnapshotAsync() =>
        VerifySnapshotAsync("TestDataTest", GeneratorSnapshotSources.TestDataTest);

    [Fact]
    public Task AsyncTestDataTest_MatchesSnapshotAsync() =>
        VerifySnapshotAsync("AsyncTestDataTest", GeneratorSnapshotSources.AsyncTestDataTest);

    [Fact]
    public Task DeferredTestDataTest_MatchesSnapshotAsync() =>
        VerifySnapshotAsync("DeferredTestDataTest", GeneratorSnapshotSources.DeferredTestDataTest);

    [Fact]
    public Task ClassDataSourceTest_MatchesSnapshotAsync() =>
        VerifySnapshotAsync("ClassDataSourceTest", GeneratorSnapshotSources.ClassDataSourceTest);

    [Fact]
    public Task MemberSourceRepeatTest_MatchesSnapshotAsync() =>
        VerifySnapshotAsync("MemberSourceRepeatTest", GeneratorSnapshotSources.MemberSourceRepeatTest);

    [Fact]
    public Task CombinedDataSourceTest_MatchesSnapshotAsync() =>
        VerifySnapshotAsync("CombinedDataSourceTest", GeneratorSnapshotSources.CombinedDataSourceTest);

    [Fact]
    public Task CombinedRepeatDataSourceTest_MatchesSnapshotAsync() =>
        VerifySnapshotAsync("CombinedRepeatDataSourceTest", GeneratorSnapshotSources.CombinedRepeatDataSourceTest);

    [Fact]
    public Task LifecycleScopesTest_MatchesSnapshotAsync() =>
        VerifySnapshotAsync("LifecycleScopesTest", GeneratorSnapshotSources.LifecycleScopesTest);

    [Fact]
    public Task DependencyMetadataTest_MatchesSnapshotAsync() =>
        VerifySnapshotAsync("DependencyMetadataTest", GeneratorSnapshotSources.DependencyMetadataTest);

    [Fact]
    public async Task DependencyMetadataTest_EmitsAlignedDependencyViewsAsync()
    {
        var registry = await GenerateRegistryAsync(GeneratorSnapshotSources.DependencyMetadataTest);
        var lines = registry.Split('\n');
        var dependenciesLine = lines.Single(static line =>
            line.Contains(
                "Dependencies = new global::NextUnit.Internal.TestCaseId[]",
                StringComparison.Ordinal));
        var dependencyInfosLine = lines.Single(static line =>
            line.Contains(
                "DependencyInfos = new global::NextUnit.Internal.DependencyInfo[]",
                StringComparison.Ordinal));

        var dependencies = ExtractQuotedValues(dependenciesLine);
        var dependencyInfoIds = ExtractQuotedValues(dependencyInfosLine);

        Assert.Equal(
            new[]
            {
                "TestProject.DependencyTests.First",
                "TestProject.DependencyTests.Second",
                "TestProject.DependencyTests.First",
                "TestProject.ExternalTests.External",
            },
            dependencies);
        Assert.Equal(dependencies, dependencyInfoIds);
    }

    [Fact]
    public Task ConstructorInjectionTest_MatchesSnapshotAsync() =>
        VerifySnapshotAsync("ConstructorInjectionTest", GeneratorSnapshotSources.ConstructorInjectionTest);

    /// <summary>
    /// A compilation that already has an entry point must not receive Program.g.cs.
    /// The exact-sources check fails if the generator emits it anyway.
    /// </summary>
    [Fact]
    public Task UserEntryPointTest_MatchesSnapshotAsync() =>
        VerifySnapshotAsync("UserEntryPointTest", GeneratorSnapshotSources.UserEntryPointTest, emitsEntryPoint: false);

    private static async Task VerifySnapshotAsync(string caseName, string source, bool emitsEntryPoint = true)
    {
        var test = new CSharpSourceGeneratorVerifier<NextUnitGenerator>.Test
        {
            TestCode = source,
        };

        test.TestState.GeneratedSources.Add((
            typeof(NextUnitGenerator),
            "GeneratedTestRegistry.g.cs",
            ReadSnapshot($"{caseName}.GeneratedTestRegistry.g.cs.txt")));

        if (emitsEntryPoint)
        {
            test.TestState.GeneratedSources.Add((
                typeof(NextUnitGenerator),
                "Program.g.cs",
                ReadSnapshot("Program.g.cs.txt")));
        }
        else
        {
            test.SolutionTransforms.Add(static (solution, projectId) =>
            {
                var project = solution.GetProject(projectId)!;
                return solution.WithProjectCompilationOptions(
                    projectId,
                    project.CompilationOptions!.WithOutputKind(OutputKind.ConsoleApplication));
            });
        }

        await test.RunAsync(TestContext.Current.CancellationToken);
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

    private static string[] ExtractQuotedValues(string line) =>
        Regex.Matches(line, "\"([^\"]+)\"")
            .Select(static match => match.Groups[1].Value)
            .ToArray();

    /// <summary>
    /// Reads a baseline as LF-terminated text.
    /// </summary>
    /// <remarks>
    /// Every file the generator emits ends its lines with LF by construction, on every host OS and
    /// regardless of how the generator's own sources were checked out, so one convention covers all
    /// baselines. The read still normalizes CRLF because a baseline file can reach the build output
    /// with CRLF if .gitattributes normalization is bypassed.
    /// </remarks>
    private static string ReadSnapshot(string fileName) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Snapshots", fileName))
            .Replace("\r\n", "\n");
}
