using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using NextUnit.CodeAnalysis.Shared;

namespace NextUnit.Analyzers.Analyzers;

/// <summary>
/// Analyzer that detects a <c>[Retry&lt;TPolicy&gt;]</c> policy the generated registry cannot construct.
/// </summary>
/// <remarks>
/// The generator emits <c>new TPolicy()</c> inside <c>NextUnit.Generated.GeneratedTestRegistry</c>
/// rather than reflecting, which is what keeps the retry path AOT-safe. The cost is that the policy
/// has to be visible from there: a private or protected nested policy satisfies the <c>new()</c>
/// constraint at the attribute and then fails the consumer's build with <c>CS0122</c> inside
/// generated code. Reported here so the error names the policy and the fix instead of pointing at a
/// file the user did not write.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class RetryPolicyAccessibilityAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(DiagnosticDescriptors.RetryPolicyNotAccessible);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSymbolAction(AnalyzeSymbol, SymbolKind.Method);
        context.RegisterSymbolAction(AnalyzeSymbol, SymbolKind.NamedType);
    }

    private static void AnalyzeSymbol(SymbolAnalysisContext context)
    {
        foreach (var attribute in context.Symbol.GetAttributes())
        {
            if (GetPolicyType(attribute) is not { } policyType ||
                IsReachableFromGeneratedCode(policyType, context.Compilation))
            {
                continue;
            }

            var location = attribute.ApplicationSyntaxReference?.GetSyntax(context.CancellationToken).GetLocation()
                ?? context.Symbol.Locations[0];

            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.RetryPolicyNotAccessible,
                location,
                policyType.ToDisplayString()));
        }
    }

    private static INamedTypeSymbol? GetPolicyType(AttributeData attribute)
    {
        if (attribute.AttributeClass is not { IsGenericType: true } attributeClass)
        {
            return null;
        }

        var constructedFrom = attributeClass.ConstructedFrom;
        if (constructedFrom.MetadataName != NextUnitAttributeNames.MetadataNames.RetryAttributeGeneric ||
            constructedFrom.ContainingNamespace.ToDisplayString() != NextUnitAttributeNames.Namespace)
        {
            return null;
        }

        return attributeClass.TypeArguments[0] as INamedTypeSymbol;
    }

    /// <summary>
    /// Reports whether the generated registry, which lives in the compiling assembly, can name the type.
    /// </summary>
    /// <remarks>
    /// The <c>new()</c> constraint already guarantees a public parameterless constructor, so only the
    /// type's own visibility is in question - and every enclosing type's, since a public type nested in
    /// a private one is unreachable all the same.
    /// </remarks>
    private static bool IsReachableFromGeneratedCode(INamedTypeSymbol policyType, Compilation compilation)
    {
        var internalsVisible =
            SymbolEqualityComparer.Default.Equals(policyType.ContainingAssembly, compilation.Assembly) ||
            policyType.ContainingAssembly?.GivesAccessTo(compilation.Assembly) == true;

        for (INamedTypeSymbol? type = policyType; type is not null; type = type.ContainingType)
        {
            switch (type.DeclaredAccessibility)
            {
                case Accessibility.Public:
                    continue;

                case Accessibility.Internal:
                case Accessibility.ProtectedOrInternal:
                    if (!internalsVisible)
                    {
                        return false;
                    }

                    continue;

                default:
                    // Private, protected, and private protected are all out of reach from a type that
                    // is neither the declaring type nor derived from it.
                    return false;
            }
        }

        return true;
    }
}
