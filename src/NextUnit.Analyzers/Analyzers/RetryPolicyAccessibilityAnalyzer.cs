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
    /// visibility is in question, and that question is the same one every other emission site asks --
    /// hence the shared rule rather than a copy of it here.
    /// </remarks>
    private static bool IsReachableFromGeneratedCode(ITypeSymbol policyType, Compilation compilation) =>
        GeneratedRegistryAccess.CanReachType(policyType, compilation.Assembly);
}
