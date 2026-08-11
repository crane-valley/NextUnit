using System.Reflection;

namespace NextUnit.Core.Tests;

/// <summary>
/// Pins that every public NextUnit attribute declares <c>Inherited = false</c>.
/// </summary>
/// <remarks>
/// The generator reads directly applied attributes only - <c>ISymbol.GetAttributes()</c>, never the
/// base type or the overridden method - so an attribute that inherits, or that leaves
/// <see cref="AttributeUsageAttribute.Inherited"/> at its default of <see langword="true"/>,
/// advertises a behavior nothing implements. Asserted over the whole assembly rather than one type
/// at a time so that a newly added attribute cannot reintroduce the mismatch unnoticed.
/// </remarks>
public class AttributeInheritanceMetadataTests
{
    [Test]
    public void EveryPublicAttribute_DeclaresInheritedFalse()
    {
        var attributes = PublicAttributeTypes();

        // Without this the assertion below passes on an empty set, which is what a renamed or
        // relocated assembly would produce.
        Assert.NotEmpty(attributes);

        var offenders = Names(attributes.Where(static type => Usage(type)?.Inherited != false));

        Assert.Empty(
            offenders,
            $"These attributes do not declare Inherited = false: {string.Join(", ", offenders)}");
    }

    [Test]
    public void EveryPublicAttribute_DeclaresAttributeUsage()
    {
        var attributes = PublicAttributeTypes();
        Assert.NotEmpty(attributes);

        var offenders = Names(attributes.Where(static type => Usage(type) is null));

        Assert.Empty(
            offenders,
            $"These attributes declare no [AttributeUsage]: {string.Join(", ", offenders)}");
    }

    private static List<Type> PublicAttributeTypes() =>
        typeof(TestAttribute).Assembly.GetExportedTypes()
            .Where(static type => !type.IsAbstract && typeof(Attribute).IsAssignableFrom(type))
            .ToList();

    private static List<string> Names(IEnumerable<Type> types) =>
        types
            .Select(static type => type.FullName ?? type.Name)
            .OrderBy(static name => name, StringComparer.Ordinal)
            .ToList();

    // inherit: false because the declaration under test is the type's own, not one it picked up from
    // a base attribute class.
    private static AttributeUsageAttribute? Usage(Type type) =>
        type.GetCustomAttribute<AttributeUsageAttribute>(inherit: false);
}
