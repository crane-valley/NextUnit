using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using NextUnit.CodeAnalysis.Shared;
namespace NextUnit.Analyzers.Analyzers;

/// <summary>
/// Analyzer that detects [MatrixExclusion] attributes with mismatched value count
/// compared to the number of [Matrix] parameters on the method.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class MatrixExclusionAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(DiagnosticDescriptors.MatrixExclusionCountMismatch);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSymbolAction(AnalyzeMethod, SymbolKind.Method);
    }

    private static void AnalyzeMethod(SymbolAnalysisContext context)
    {
        var method = (IMethodSymbol)context.Symbol;

        // Count parameters with [Matrix] attribute using LINQ
        var matrixParameterCount = method.Parameters
            .Count(p => p.GetAttributes().Any(a => a.AttributeClass?.ToDisplayString() == NextUnitAttributeNames.Matrix));

        // If no matrix parameters, nothing to check
        if (matrixParameterCount == 0)
        {
            return;
        }

        // Check each [MatrixExclusion] attribute
        foreach (var attribute in method.GetAttributes())
        {
            if (attribute.AttributeClass?.ToDisplayString() != NextUnitAttributeNames.MatrixExclusion)
            {
                continue;
            }

            // Get the constructor arguments (the params object[] values)
            if (attribute.ConstructorArguments.IsEmpty)
            {
                continue;
            }

            // The first constructor argument is the params array
            var valuesArg = attribute.ConstructorArguments[0];
            var exclusionValueCount = valuesArg.Kind == TypedConstantKind.Array
                ? valuesArg.Values.Length
                : 1;

            if (exclusionValueCount != matrixParameterCount)
            {
                var location = attribute.ApplicationSyntaxReference?.GetSyntax(context.CancellationToken).GetLocation()
                    ?? method.Locations[0];

                context.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.MatrixExclusionCountMismatch,
                    location,
                    exclusionValueCount,
                    matrixParameterCount));
            }
        }
    }
}
