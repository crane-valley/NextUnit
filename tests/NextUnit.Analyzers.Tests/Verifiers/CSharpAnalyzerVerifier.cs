using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;

namespace NextUnit.Analyzers.Tests.Verifiers;

/// <summary>
/// Helper class for testing Roslyn analyzers.
/// </summary>
/// <typeparam name="TAnalyzer">The analyzer type to test.</typeparam>
public static class CSharpAnalyzerVerifier<TAnalyzer>
    where TAnalyzer : DiagnosticAnalyzer, new()
{
    /// <summary>
    /// Creates a diagnostic result for the specified diagnostic ID.
    /// </summary>
    public static DiagnosticResult Diagnostic(string diagnosticId)
        => CSharpAnalyzerVerifier<TAnalyzer, DefaultVerifier>.Diagnostic(diagnosticId);

    /// <summary>
    /// Creates a diagnostic result for the specified descriptor.
    /// </summary>
    public static DiagnosticResult Diagnostic(DiagnosticDescriptor descriptor)
        => CSharpAnalyzerVerifier<TAnalyzer, DefaultVerifier>.Diagnostic(descriptor);

    /// <summary>
    /// Verifies that the analyzer produces the expected diagnostics.
    /// </summary>
    public static async Task VerifyAnalyzerAsync(string source, params DiagnosticResult[] expected)
    {
        var test = new Test
        {
            TestCode = source,
        };

        test.ExpectedDiagnostics.AddRange(expected);
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>
    /// Verifies the analyzer against a test project that references a second, separately compiled
    /// assembly.
    /// </summary>
    /// <remarks>
    /// The only way to exercise rules that turn on an assembly boundary -- <c>internal</c> and
    /// <c>private protected</c> are visible to a derived class in the declaring assembly and
    /// invisible outside it, and a single-project test cannot tell those two cases apart.
    /// </remarks>
    public static async Task VerifyAnalyzerWithLibraryAsync(
        string source,
        string librarySource,
        params DiagnosticResult[] expected)
    {
        const string LibraryProject = "TestLibrary";

        var test = new Test
        {
            TestCode = source,
        };

        test.TestState.AdditionalProjects.Add(
            LibraryProject,
            new ProjectState(LibraryProject, LanguageNames.CSharp, "/Library/", "cs"));
        test.TestState.AdditionalProjects[LibraryProject].Sources.Add(("Library.cs", librarySource));
        test.TestState.AdditionalProjectReferences.Add(LibraryProject);

        test.ExpectedDiagnostics.AddRange(expected);
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>
    /// Custom test class for analyzer verification.
    /// </summary>
    /// <remarks>
    /// Compiles against the real NextUnit.Core assembly rather than a hand-written attribute
    /// stub, so the analyzers cannot pass against attribute shapes the product does not have.
    /// </remarks>
    public class Test : CSharpAnalyzerTest<TAnalyzer, DefaultVerifier>
    {
        public Test()
        {
            ReferenceAssemblies = TestReferenceAssemblies.Net10;
            TestState.AdditionalReferences.Add(typeof(TestAttribute).Assembly);
        }
    }
}
