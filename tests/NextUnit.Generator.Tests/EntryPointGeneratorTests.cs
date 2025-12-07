using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;

namespace NextUnit.Generator.Tests;

/// <summary>
/// Tests for entry point generation in NextUnitGenerator.
/// </summary>
public class EntryPointGeneratorTests
{
    [Fact]
    public async Task NoExistingEntryPoint_GeneratesEntryPointAsync()
    {
        var source = @"
using NextUnit;

namespace TestProject;

public class TestClass
{
    [Test]
    public void SimpleTest()
    {
    }
}";

        var test = new CSharpSourceGeneratorVerifier<NextUnitGenerator>.Test
        {
            TestCode = source,
            // Skip source verification - we just want to ensure it compiles
            TestBehaviors = TestBehaviors.SkipGeneratedSourcesCheck,
        };

        // The test passing means the generated entry point compiled successfully
        await test.RunAsync();
    }

    [Fact]
    public async Task ExistingEntryPoint_DoesNotGenerateEntryPointAsync()
    {
        var source = @"
using NextUnit;
using System.Threading.Tasks;

namespace TestProject;

public class TestClass
{
    [Test]
    public void SimpleTest()
    {
    }
}

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        return 0;
    }
}";

        var test = new CSharpSourceGeneratorVerifier<NextUnitGenerator>.Test
        {
            TestCode = source,
            // Skip source verification to avoid complex test registry content
            TestBehaviors = TestBehaviors.SkipGeneratedSourcesCheck,
        };

        // Set the output kind to ConsoleApplication so the entry point is recognized
        test.SolutionTransforms.Add((solution, projectId) =>
        {
            var project = solution.GetProject(projectId)!;
            var compilationOptions = project.CompilationOptions!;
            compilationOptions = compilationOptions.WithOutputKind(OutputKind.ConsoleApplication);
            return solution.WithProjectCompilationOptions(projectId, compilationOptions);
        });

        // If Program.g.cs is incorrectly generated, we'll get a CS0017 error (multiple entry points).
        // We don't expect that error, so test should pass if generator correctly skips generation.
        await test.RunAsync();
    }

    // Removed redundant test: GeneratedEntryPoint_CompilesSuccessfullyAsync
}
