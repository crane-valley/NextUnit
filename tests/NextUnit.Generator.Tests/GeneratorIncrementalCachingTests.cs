using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace NextUnit.Generator.Tests;

/// <summary>
/// Regression test for incremental caching of the generator pipeline.
/// </summary>
public class GeneratorIncrementalCachingTests
{
    private const string OriginalSource = """
        using NextUnit;

        namespace TestProject;

        public class CachingTests
        {
            [Test]
            public void SimpleTest()
            {
            }
        }
        """;

    /// <summary>
    /// The same source plus a member that contributes nothing to the generated registry.
    /// Editing the file must not force the source output to be recomputed.
    /// </summary>
    private const string EditedSource = """
        using NextUnit;

        namespace TestProject;

        public class CachingTests
        {
            private readonly int _unrelated = 42;

            [Test]
            public void SimpleTest()
            {
            }
        }
        """;

    [Fact]
    public async Task UnrelatedEditInSameFile_KeepsSourceOutputCachedAsync()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        var compilation = await GeneratorDriverHarness.CreateCompilationAsync(
            OriginalSource,
            OutputKind.DynamicallyLinkedLibrary,
            cancellationToken);

        var driver = GeneratorDriverHarness.CreateDriver(trackIncrementalGeneratorSteps: true)
            .RunGenerators(compilation, cancellationToken);

        var editedCompilation = compilation.ReplaceSyntaxTree(
            compilation.SyntaxTrees.Single(),
            CSharpSyntaxTree.ParseText(EditedSource, path: "Test0.cs", cancellationToken: cancellationToken));

        driver = driver.RunGenerators(editedCompilation, cancellationToken);

        var outputReasons = driver.GetRunResult().Results.Single()
            .TrackedOutputSteps
            .SelectMany(step => step.Value)
            .SelectMany(step => step.Outputs)
            .Select(output => output.Reason)
            .ToList();

        Assert.NotEmpty(outputReasons);
        Assert.All(
            outputReasons,
            reason => Assert.True(
                reason is IncrementalStepRunReason.Cached or IncrementalStepRunReason.Unchanged,
                $"Source output was recomputed with reason '{reason}' after an edit that cannot affect the generated registry."));
    }
}
