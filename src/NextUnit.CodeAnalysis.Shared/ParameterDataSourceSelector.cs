using Microsoft.CodeAnalysis;

namespace NextUnit.CodeAnalysis.Shared;

/// <summary>
/// Which parameter-level data source attribute the generator reads for a parameter.
/// </summary>
internal enum ParameterDataSourceAttributeKind
{
    None,
    Values,
    ValuesFromMember,
    ValuesFrom
}

/// <summary>
/// Selects the one attribute the generator reads as a parameter's data source.
/// </summary>
/// <remarks>
/// A parameter supplies its values from the first attribute that answers, in declaration order,
/// and everything after it is ignored: <c>[Values(1)] [ValuesFrom&lt;T&gt;]</c> expands the inline
/// values and never constructs <c>T</c>. Shared so that a rule reporting on <c>T</c> reports only
/// when <c>T</c> is the one the generator would emit -- naming the ignored attribute would offer a
/// fix that does not put it back in play.
/// <para>
/// Recognition is not attribute identity alone. A <c>[ValuesFromMember]</c> naming nothing supplies
/// nothing, so the search passes over it and a later attribute can still win. The same is true of a
/// <c>[Values]</c> carrying no array, which the <c>params</c> constructor makes hard to write but
/// which a malformed attribute still presents.
/// </para>
/// </remarks>
internal static class ParameterDataSourceSelector
{
    public static (ParameterDataSourceAttributeKind Kind, AttributeData? Attribute) Select(IParameterSymbol parameter)
    {
        foreach (var attribute in parameter.GetAttributes())
        {
            var kind = Classify(attribute);
            if (kind != ParameterDataSourceAttributeKind.None)
            {
                return (kind, attribute);
            }
        }

        return (ParameterDataSourceAttributeKind.None, null);
    }

    private static ParameterDataSourceAttributeKind Classify(AttributeData attribute)
    {
        if (IsNonGenericNextUnitAttribute(attribute, NextUnitAttributeNames.SimpleNames.Values) &&
            attribute.ConstructorArguments.Length > 0 &&
            attribute.ConstructorArguments[0].Kind == TypedConstantKind.Array)
        {
            return ParameterDataSourceAttributeKind.Values;
        }

        if (IsNonGenericNextUnitAttribute(attribute, NextUnitAttributeNames.SimpleNames.ValuesFromMember) &&
            attribute.ConstructorArguments.Length > 0 &&
            attribute.ConstructorArguments[0].Value is string memberName &&
            !string.IsNullOrEmpty(memberName))
        {
            return ParameterDataSourceAttributeKind.ValuesFromMember;
        }

        return ClassDataSourceAttributeMatcher.GetValuesFromType(attribute) is not null
            ? ParameterDataSourceAttributeKind.ValuesFrom
            : ParameterDataSourceAttributeKind.None;
    }

    /// <summary>
    /// Reports whether the attribute is the named NextUnit attribute of arity zero.
    /// </summary>
    /// <remarks>
    /// Arity is compared because the generic siblings are separate attributes with separate
    /// meanings: <c>[ValuesFrom&lt;T&gt;]</c> must not answer to <c>[ValuesFromMember]</c>'s arm.
    /// <c>ContainingType</c> is compared because a nested type reports the namespace of its
    /// outermost container, so <c>NextUnit.Container.ValuesAttribute</c> would otherwise pass as
    /// <c>[Values]</c> and win the selection ahead of a real data source attribute behind it.
    /// </remarks>
    private static bool IsNonGenericNextUnitAttribute(AttributeData attribute, string simpleName) =>
        attribute.AttributeClass is
        {
            Arity: 0,
            ContainingType: null,
            ContainingNamespace:
            {
                Name: NextUnitAttributeNames.Namespace,
                ContainingNamespace.IsGlobalNamespace: true
            }
        } attributeClass &&
        attributeClass.Name == simpleName;
}
