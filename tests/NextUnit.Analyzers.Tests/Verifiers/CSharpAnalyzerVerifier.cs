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
    /// Attribute definitions to include in test source for self-contained compilation.
    /// </summary>
    internal const string AttributeDefinitions = """
        namespace NextUnit
        {
            [System.AttributeUsage(System.AttributeTargets.Method)]
            public sealed class TestAttribute : System.Attribute { }

            [System.AttributeUsage(System.AttributeTargets.Method, AllowMultiple = true)]
            public sealed class ArgumentsAttribute : System.Attribute
            {
                public ArgumentsAttribute(params object?[] args) { Arguments = args; }
                public object?[] Arguments { get; }
            }

            [System.AttributeUsage(System.AttributeTargets.Assembly | System.AttributeTargets.Class | System.AttributeTargets.Method)]
            public sealed class TimeoutAttribute : System.Attribute
            {
                public TimeoutAttribute(int milliseconds) { Milliseconds = milliseconds; }
                public int Milliseconds { get; }
            }

            [System.AttributeUsage(System.AttributeTargets.Parameter)]
            public sealed class MatrixAttribute : System.Attribute
            {
                public MatrixAttribute(params object?[] values) { Values = values; }
                public object?[] Values { get; }
            }

            [System.AttributeUsage(System.AttributeTargets.Method, AllowMultiple = true)]
            public sealed class MatrixExclusionAttribute : System.Attribute
            {
                public MatrixExclusionAttribute(params object?[] values) { Values = values; }
                public object?[] Values { get; }
            }

            [System.AttributeUsage(System.AttributeTargets.Method, AllowMultiple = true)]
            public sealed class DependsOnAttribute : System.Attribute
            {
                public DependsOnAttribute(params string[] dependencies) { Dependencies = dependencies; }
                public string[] Dependencies { get; }
                public bool ProceedOnFailure { get; set; }
            }

            [System.AttributeUsage(System.AttributeTargets.Method, AllowMultiple = true)]
            public sealed class TestDataAttribute : System.Attribute
            {
                public TestDataAttribute(string memberName) { MemberName = memberName; }
                public TestDataAttribute(string memberName, System.Type memberType) { MemberName = memberName; MemberType = memberType; }
                public string MemberName { get; }
                public System.Type? MemberType { get; }
            }

            [System.AttributeUsage(System.AttributeTargets.Parameter)]
            public sealed class ValuesFromMemberAttribute : System.Attribute
            {
                public ValuesFromMemberAttribute(string memberName) { MemberName = memberName; }
                public ValuesFromMemberAttribute(string memberName, System.Type memberType) { MemberName = memberName; MemberType = memberType; }
                public string MemberName { get; }
                public System.Type? MemberType { get; }
            }

            public enum LifecycleScope { Test, Class, Assembly, Session }

            [System.AttributeUsage(System.AttributeTargets.Method)]
            public sealed class BeforeAttribute : System.Attribute
            {
                public BeforeAttribute(LifecycleScope scope) { Scope = scope; }
                public LifecycleScope Scope { get; }
            }

            [System.AttributeUsage(System.AttributeTargets.Method)]
            public sealed class AfterAttribute : System.Attribute
            {
                public AfterAttribute(LifecycleScope scope) { Scope = scope; }
                public LifecycleScope Scope { get; }
            }
        }
        """;

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

        // Add attribute definitions as a separate source file
        test.TestState.Sources.Add(("Attributes.cs", AttributeDefinitions));

        test.ExpectedDiagnostics.AddRange(expected);
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>
    /// Custom test class for analyzer verification.
    /// </summary>
    public class Test : CSharpAnalyzerTest<TAnalyzer, DefaultVerifier>
    {
        public Test()
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80;
        }
    }
}
