using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;

namespace NextUnit.Analyzers.Tests.Verifiers;

/// <summary>
/// Helper class for testing Roslyn code fix providers.
/// </summary>
/// <typeparam name="TAnalyzer">The analyzer type that produces the diagnostic.</typeparam>
/// <typeparam name="TCodeFix">The code fix provider type to test.</typeparam>
public static class CSharpCodeFixVerifier<TAnalyzer, TCodeFix>
    where TAnalyzer : DiagnosticAnalyzer, new()
    where TCodeFix : CodeFixProvider, new()
{
    /// <summary>
    /// Creates a diagnostic result for the specified diagnostic ID.
    /// </summary>
    public static DiagnosticResult Diagnostic(string diagnosticId)
        => CSharpCodeFixVerifier<TAnalyzer, TCodeFix, DefaultVerifier>.Diagnostic(diagnosticId);

    /// <summary>
    /// Creates a diagnostic result for the specified descriptor.
    /// </summary>
    public static DiagnosticResult Diagnostic(DiagnosticDescriptor descriptor)
        => CSharpCodeFixVerifier<TAnalyzer, TCodeFix, DefaultVerifier>.Diagnostic(descriptor);

    /// <summary>
    /// Verifies that the code fix transforms the source code correctly.
    /// </summary>
    public static async Task VerifyCodeFixAsync(string source, DiagnosticResult expected, string fixedSource)
    {
        var test = new Test
        {
            TestCode = source,
            FixedCode = fixedSource,
        };

        // Add attribute definitions as a separate source file
        test.TestState.Sources.Add(("Attributes.cs", CSharpAnalyzerVerifier<TAnalyzer>.AttributeDefinitions));
        test.FixedState.Sources.Add(("Attributes.cs", CSharpAnalyzerVerifier<TAnalyzer>.AttributeDefinitions));

        test.ExpectedDiagnostics.Add(expected);
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>
    /// Verifies that the code fix transforms the source code correctly with multiple diagnostics.
    /// </summary>
    public static async Task VerifyCodeFixAsync(string source, DiagnosticResult[] expected, string fixedSource)
    {
        var test = new Test
        {
            TestCode = source,
            FixedCode = fixedSource,
        };

        // Add attribute definitions as a separate source file
        test.TestState.Sources.Add(("Attributes.cs", CSharpAnalyzerVerifier<TAnalyzer>.AttributeDefinitions));
        test.FixedState.Sources.Add(("Attributes.cs", CSharpAnalyzerVerifier<TAnalyzer>.AttributeDefinitions));

        test.ExpectedDiagnostics.AddRange(expected);
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>
    /// Custom test class for code fix verification.
    /// </summary>
    public class Test : CSharpCodeFixTest<TAnalyzer, TCodeFix, DefaultVerifier>
    {
        public Test()
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80;
        }
    }
}
