using System.Reflection;

namespace NextUnit.Core.Tests;

/// <summary>
/// Pins every public NextUnit attribute to the <c>Inherited</c> value the generator implements.
/// </summary>
/// <remarks>
/// The rule the map encodes: an attribute that configures how a test runs or how it is labelled is
/// inherited, and an attribute that decides what the test set is -- whether a method is a test, what
/// data it runs with, how many cases it expands to, what it depends on -- is not. Asserted as an
/// explicit map rather than one blanket value so that adding an attribute is a deliberate choice
/// between the two, and asserted over the whole assembly so a new attribute cannot skip the choice.
/// <para>
/// <c>[Before]</c> and <c>[After]</c> are deliberately on the false side. Hooks declared on a base
/// class do run for derived classes, but that is a rule about declarations, not CLR attribute
/// inheritance: the attributes allow multiple, a derived override can re-declare one scope and not
/// another, and <c>Inherited = true</c> would advertise a merge the generator does not perform.
/// </para>
/// </remarks>
public class AttributeInheritanceMetadataTests
{
    private static readonly HashSet<string> _inheritedAttributes = new(StringComparer.Ordinal)
    {
        "NextUnit.CategoryAttribute",
        "NextUnit.CultureAttribute",
        "NextUnit.DisplayNameFormatterAttribute",
        "NextUnit.DisplayNameFormatterAttribute`1",
        "NextUnit.ExecutionPriorityAttribute",
        "NextUnit.ExplicitAttribute",
        "NextUnit.FlakyAttribute",
        "NextUnit.InvariantCultureAttribute",
        "NextUnit.NotInParallelAttribute",
        "NextUnit.ParallelGroupAttribute",
        "NextUnit.ParallelLimitAttribute",
        "NextUnit.RetryAttribute",
        "NextUnit.RetryAttribute`1",
        "NextUnit.TagAttribute",
        "NextUnit.TimeoutAttribute",
        "NextUnit.UICultureAttribute",
    };

    [Test]
    public void EveryPublicAttribute_DeclaresTheInheritedValueTheGeneratorImplements()
    {
        var attributes = PublicAttributeTypes();

        // Without this the assertion below passes on an empty set, which is what a renamed or
        // relocated assembly would produce.
        Assert.NotEmpty(attributes);

        var offenders = Names(attributes.Where(static type =>
            Usage(type)?.Inherited != _inheritedAttributes.Contains(NameOf(type))));

        Assert.Empty(
            offenders,
            $"These attributes declare an Inherited value the generator does not implement: {string.Join(", ", offenders)}");
    }

    [Test]
    public void EveryInheritedAttributeName_ResolvesToAnAttributeThatExists()
    {
        var declared = PublicAttributeTypes().Select(NameOf).ToHashSet(StringComparer.Ordinal);

        var missing = _inheritedAttributes.Where(name => !declared.Contains(name)).OrderBy(
            static name => name, StringComparer.Ordinal).ToList();

        // A renamed or removed attribute would otherwise leave a dead entry in the map, and the
        // remaining assertion cannot see one: an attribute that no longer exists offends nothing.
        Assert.Empty(missing, $"These names are in the inherited map but declare no attribute: {string.Join(", ", missing)}");
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

    private static string NameOf(Type type) => type.FullName ?? type.Name;

    private static List<string> Names(IEnumerable<Type> types) =>
        types
            .Select(NameOf)
            .OrderBy(static name => name, StringComparer.Ordinal)
            .ToList();

    // inherit: false because the declaration under test is the type's own, not one it picked up from
    // a base attribute class.
    private static AttributeUsageAttribute? Usage(Type type) =>
        type.GetCustomAttribute<AttributeUsageAttribute>(inherit: false);
}
