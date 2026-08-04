using Microsoft.CodeAnalysis;

namespace NextUnit.CodeAnalysis.Shared;

/// <summary>
/// Recognizes the two retry attributes, shared by the generator and the analyzers.
/// </summary>
/// <remarks>
/// Matching is done by symbol identity -- simple name, generic arity, and the containing namespace
/// chain -- rather than by comparing a formatted display string. Both run over every attribute on
/// every method and type in the compilation, and <c>ToDisplayString</c> allocates a string per call,
/// so the formatted comparison put an allocation on a hot path to answer a question the symbol
/// already carries. The names still come from <see cref="NextUnitAttributeNames"/> so the spelling
/// stays in one place.
/// </remarks>
internal static class RetryAttributeMatcher
{
    /// <summary>
    /// Reports whether the attribute is <c>[Retry]</c> or <c>[Retry&lt;TPolicy&gt;]</c>.
    /// </summary>
    public static bool IsRetry(AttributeData attribute) =>
        IsPlainRetry(attribute) || GetPolicyType(attribute) is not null;

    /// <summary>
    /// Reports whether the attribute is the policy-free <c>[Retry]</c>.
    /// </summary>
    public static bool IsPlainRetry(AttributeData attribute) =>
        attribute.AttributeClass is { Arity: 0 } attributeClass && IsRetryNamed(attributeClass);

    /// <summary>
    /// Returns the policy type of a <c>[Retry&lt;TPolicy&gt;]</c> attribute, or null for anything else.
    /// </summary>
    public static ITypeSymbol? GetPolicyType(AttributeData attribute)
    {
        if (attribute.AttributeClass is not { IsGenericType: true } attributeClass)
        {
            return null;
        }

        var constructedFrom = attributeClass.ConstructedFrom;
        return constructedFrom.Arity == 1 && IsRetryNamed(constructedFrom)
            ? attributeClass.TypeArguments[0]
            : null;
    }

    /// <summary>
    /// Reports whether the type is <c>NextUnit.RetryAttribute</c> of any arity.
    /// </summary>
    /// <remarks>
    /// <see cref="ISymbol.Name"/> carries no generic arity, so both retry attributes answer to the
    /// same name and the caller separates them by <see cref="INamedTypeSymbol.Arity"/>.
    /// </remarks>
    private static bool IsRetryNamed(INamedTypeSymbol type) =>
        type is
        {
            Name: NextUnitAttributeNames.SimpleNames.Retry,
            ContainingNamespace:
            {
                Name: NextUnitAttributeNames.Namespace,
                ContainingNamespace.IsGlobalNamespace: true
            }
        };
}
