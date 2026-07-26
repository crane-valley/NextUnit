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
    public Task ClassDataSourceTest_MatchesSnapshotAsync() =>
        VerifySnapshotAsync("ClassDataSourceTest", GeneratorSnapshotSources.ClassDataSourceTest);

    [Fact]
    public Task CombinedDataSourceTest_MatchesSnapshotAsync() =>
        VerifySnapshotAsync("CombinedDataSourceTest", GeneratorSnapshotSources.CombinedDataSourceTest);

    [Fact]
    public Task LifecycleScopesTest_MatchesSnapshotAsync() =>
        VerifySnapshotAsync("LifecycleScopesTest", GeneratorSnapshotSources.LifecycleScopesTest);

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
