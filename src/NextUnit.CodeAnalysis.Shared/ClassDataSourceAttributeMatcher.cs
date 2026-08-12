using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace NextUnit.CodeAnalysis.Shared;

/// <summary>
/// Recognizes the two attributes that name a data source class, shared by the generator and the
/// analyzers.
/// </summary>
/// <remarks>
/// Both are emitted the same way -- a <c>typeof(T)</c> and a <c>new T()</c> factory -- so the rule
/// that reports an unreachable <c>T</c> has to select exactly the attributes the generator emits
/// for. Three independent spellings of that selection existed before this type, and a diagnostic
/// drifting away from its emission site is the failure the rule exists to report.
/// <para>
/// Matching is by symbol identity rather than by comparing a formatted display string, for the
/// reason <see cref="RetryAttributeMatcher"/> gives: this runs over every attribute in the
/// compilation, and <c>ToDisplayString</c> allocates per call. Arity is not compared, because the
/// simple name plus <c>IsGenericType</c> already separates these from every other NextUnit
/// attribute, and <c>[ClassDataSource]</c> has four arities.
/// </para>
/// </remarks>
internal static class ClassDataSourceAttributeMatcher
{
    /// <summary>
    /// Returns the data source types named by <c>[ClassDataSource&lt;...&gt;]</c>, or an empty array
    /// for any other attribute.
    /// </summary>
    public static ImmutableArray<ITypeSymbol> GetClassDataSourceTypes(AttributeData attribute) =>
        IsGenericNextUnitAttribute(attribute, NextUnitAttributeNames.SimpleNames.ClassDataSource)
            ? attribute.AttributeClass!.TypeArguments
            : ImmutableArray<ITypeSymbol>.Empty;

    /// <summary>
    /// Returns the data source type named by <c>[ValuesFrom&lt;T&gt;]</c>, or null for any other
    /// attribute.
    /// </summary>
    public static ITypeSymbol? GetValuesFromType(AttributeData attribute) =>
        IsGenericNextUnitAttribute(attribute, NextUnitAttributeNames.SimpleNames.ValuesFrom)
            ? attribute.AttributeClass!.TypeArguments[0]
            : null;

    private static bool IsGenericNextUnitAttribute(AttributeData attribute, string simpleName) =>
        attribute.AttributeClass is
        {
            IsGenericType: true,
            ContainingNamespace:
            {
                Name: NextUnitAttributeNames.Namespace,
                ContainingNamespace.IsGlobalNamespace: true
            }
        } attributeClass &&
        attributeClass.Name == simpleName;
}
