using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using NextUnit.CodeAnalysis.Shared;

namespace NextUnit.Analyzers.Analyzers;

/// <summary>
/// Analyzer that detects unsupported test and lifecycle method return types.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class UnsupportedMethodReturnTypeAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(DiagnosticDescriptors.UnsupportedMethodReturnType);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(startContext =>
        {
            var knownTypes = KnownReturnTypes.Create(startContext.Compilation);
            startContext.RegisterSymbolAction(
                symbolContext => AnalyzeMethod(symbolContext, knownTypes),
                SymbolKind.Method);
        });
    }

    private static void AnalyzeMethod(
        SymbolAnalysisContext context,
        KnownReturnTypes knownTypes)
    {
        var method = (IMethodSymbol)context.Symbol;

        if (!HasSupportedAttribute(method) || IsSupportedReturnType(method, knownTypes))
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
            attribute => NextUnitAttributeNames.TestAndLifecycle.Contains(
                attribute.AttributeClass?.ToDisplayString() ?? ""));

    // async void is classified as Void and therefore accepted here; AsyncVoidTestAnalyzer reports
    // it, so classifying it as unsupported would produce two diagnostics for one method.
    private static bool IsSupportedReturnType(
        IMethodSymbol method,
        KnownReturnTypes knownTypes) =>
        knownTypes.Classify(method) != MethodReturnKind.Unsupported;
}
