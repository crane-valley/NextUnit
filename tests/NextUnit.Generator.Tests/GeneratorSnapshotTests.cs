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
            ReadSnapshot($"{caseName}.GeneratedTestRegistry.g.cs.txt", Environment.NewLine)));

        if (emitsEntryPoint)
        {
            test.TestState.GeneratedSources.Add((
                typeof(NextUnitGenerator),
                "Program.g.cs",
                ReadSnapshot("Program.g.cs.txt", "\n")));
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
    /// Reads a baseline and rewrites its line endings to <paramref name="newLine"/>.
    /// </summary>
    /// <remarks>
    /// Baselines are stored with LF because .gitattributes normalizes the working tree, but the
    /// generator's own newline differs per file: the registry is built with StringBuilder.AppendLine
    /// (Environment.NewLine, so CRLF on Windows), while Program.g.cs comes from a verbatim string
    /// literal in an LF-normalized source file. Comparing raw text would therefore fail on Windows
    /// for reasons unrelated to content.
    /// </remarks>
    private static string ReadSnapshot(string fileName, string newLine)
    {
        var text = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Snapshots", fileName))
            .Replace("\r\n", "\n");

        return newLine == "\n" ? text : text.Replace("\n", newLine);
    }
}
