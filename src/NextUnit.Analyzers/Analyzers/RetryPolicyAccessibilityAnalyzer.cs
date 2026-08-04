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

    private static ITypeSymbol? GetPolicyType(AttributeData attribute) =>
        RetryAttributeMatcher.GetPolicyType(attribute);

    /// <summary>
    /// Reports whether the generated registry, which lives in the compiling assembly, can name the type.
    /// </summary>
    /// <remarks>
    /// The <c>new()</c> constraint already guarantees a public parameterless constructor, so only
    /// visibility is in question -- of the type, of every enclosing type, since a public type nested in
    /// a private one is unreachable all the same, and of every type argument, since a reachable
    /// <c>Policy&lt;T&gt;</c> still cannot be named when <c>T</c> cannot.
    /// </remarks>
    private static bool IsReachableFromGeneratedCode(ITypeSymbol policyType, Compilation compilation)
    {
        // An unresolved type carries no accessibility to judge and already has its own compiler error.
        // Reporting NU0016 on top would add a visibility complaint the user cannot act on and bury the
        // error that actually needs fixing.
        if (policyType.TypeKind == TypeKind.Error)
        {
            return true;
        }

        // An array is named through its element type, and a type parameter is substituted at the use
        // site, so neither has a visibility of its own to check.
        if (policyType is IArrayTypeSymbol array)
        {
            return IsReachableFromGeneratedCode(array.ElementType, compilation);
        }

        if (policyType is not INamedTypeSymbol namedType)
        {
            return true;
        }

        for (INamedTypeSymbol? type = namedType; type is not null; type = type.ContainingType)
        {
            // A file-local type reports internal accessibility but has a mangled metadata name and can
            // only be named inside its own source file, so the generated registry cannot construct it
            // however visible it looks.
            if (type.IsFileLocal)
            {
                return false;
            }

            if (!IsVisibleToCompilingAssembly(type, compilation))
            {
                return false;
            }

            foreach (var typeArgument in type.TypeArguments)
            {
                if (!IsReachableFromGeneratedCode(typeArgument, compilation))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static bool IsVisibleToCompilingAssembly(INamedTypeSymbol type, Compilation compilation)
    {
        switch (type.DeclaredAccessibility)
        {
            case Accessibility.Public:
                return true;

            case Accessibility.Internal:
            case Accessibility.ProtectedOrInternal:
                return SymbolEqualityComparer.Default.Equals(type.ContainingAssembly, compilation.Assembly) ||
                    type.ContainingAssembly?.GivesAccessTo(compilation.Assembly) == true;

            default:
                // Private, protected, and private protected are all out of reach from a type that is
                // neither the declaring type nor derived from it.
                return false;
        }
    }
}
