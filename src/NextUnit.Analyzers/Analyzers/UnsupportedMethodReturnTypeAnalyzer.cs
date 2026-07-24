using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace NextUnit.Analyzers.Analyzers;

/// <summary>
/// Analyzer that detects unsupported test and lifecycle method return types.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class UnsupportedMethodReturnTypeAnalyzer : DiagnosticAnalyzer
{
    private static readonly ImmutableHashSet<string> _supportedAttributeNames =
        ImmutableHashSet.Create(
            "NextUnit.TestAttribute",
            "NextUnit.BeforeAttribute",
            "NextUnit.AfterAttribute");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(DiagnosticDescriptors.UnsupportedMethodReturnType);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSymbolAction(AnalyzeMethod, SymbolKind.Method);
    }

    private static void AnalyzeMethod(SymbolAnalysisContext context)
    {
        var method = (IMethodSymbol)context.Symbol;

        if (!HasSupportedAttribute(method) || IsSupportedReturnType(method))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            DiagnosticDescriptors.UnsupportedMethodReturnType,
            method.Locations[0],
            method.Name,
            method.ReturnType.ToDisplayString()));
    }

    private static bool HasSupportedAttribute(IMethodSymbol method) =>
        method.GetAttributes().Any(
            attribute => _supportedAttributeNames.Contains(attribute.AttributeClass?.ToDisplayString() ?? ""));

    private static bool IsSupportedReturnType(IMethodSymbol method)
    {
        if (method.ReturnsVoid)
        {
            return true;
        }

        if (method.ReturnType is not INamedTypeSymbol returnType ||
            returnType.ContainingNamespace.ToDisplayString() != "System.Threading.Tasks")
        {
            return false;
        }

        return returnType is
        { Name: "Task", Arity: 0 or 1 } or
        { Name: "ValueTask", Arity: 0 or 1 };
    }
}
