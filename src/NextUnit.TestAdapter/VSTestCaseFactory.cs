using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using NextUnit.Internal;

namespace NextUnit.TestAdapter;

/// <summary>
/// Factory for creating VSTest TestCase objects from NextUnit test descriptors.
/// </summary>
internal static class VSTestCaseFactory
{
    private static readonly Dictionary<string, TestProperty> _filterProperties = new(StringComparer.OrdinalIgnoreCase)
    {
        ["FullyQualifiedName"] = TestCaseProperties.FullyQualifiedName,
        ["DisplayName"] = TestCaseProperties.DisplayName,
        ["Category"] = RegisterTraitProperty("Category"),
        ["Tag"] = RegisterTraitProperty("Tag"),
        ["SkipReason"] = RegisterTraitProperty("SkipReason"),
        ["Explicit"] = RegisterTraitProperty("Explicit"),
        ["ExplicitReason"] = RegisterTraitProperty("ExplicitReason")
    };

    internal static IEnumerable<string> SupportedFilterProperties => _filterProperties.Keys;

    internal static TestProperty? GetFilterProperty(string propertyName) =>
        _filterProperties.GetValueOrDefault(propertyName);

    internal static object? GetFilterValue(TestCase testCase, string propertyName)
    {
        if (propertyName.Equals("FullyQualifiedName", StringComparison.OrdinalIgnoreCase))
        {
            return testCase.FullyQualifiedName;
        }

        if (propertyName.Equals("DisplayName", StringComparison.OrdinalIgnoreCase))
        {
            return testCase.DisplayName;
        }

        var values = testCase.Traits
            .Where(trait => trait.Name.Equals(propertyName, StringComparison.OrdinalIgnoreCase))
            .Select(trait => trait.Value)
            .ToArray();
        return values.Length == 0 ? null : values;
    }

    private static TestProperty RegisterTraitProperty(string name) =>
        TestProperty.Register($"NextUnit.{name}", name, typeof(string[]), TestPropertyAttributes.Hidden, typeof(TestCase));

    /// <summary>
    /// Creates a VSTest TestCase from a test descriptor.
    /// </summary>
    /// <param name="descriptor">The test case descriptor.</param>
    /// <param name="source">The source assembly path.</param>
    /// <param name="includeTraits">Whether to include category and tag traits. Both discovery and execution results include them, because a deferred data source's rows are first seen during execution and would otherwise carry none.</param>
    /// <returns>A VSTest TestCase object.</returns>
    public static TestCase Create(TestCaseDescriptor descriptor, string source, bool includeTraits = true)
    {
        var testCase = new TestCase(descriptor.Id.Value, new Uri(NextUnitTestExecutor.ExecutorUri), source)
        {
            DisplayName = descriptor.DisplayName,
            CodeFilePath = null,
            LineNumber = 0
        };

        if (includeTraits)
        {
            AddTraits(testCase, descriptor);
        }

        return testCase;
    }

    /// <summary>
    /// Adds category and tag traits to a test case.
    /// </summary>
    private static void AddTraits(TestCase testCase, TestCaseDescriptor descriptor)
    {
        foreach (var category in descriptor.Categories)
        {
            testCase.Traits.Add(new Trait("Category", category));
        }

        foreach (var tag in descriptor.Tags)
        {
            testCase.Traits.Add(new Trait("Tag", tag));
        }

        if (descriptor.SkipReason is not null)
        {
            testCase.Traits.Add(new Trait("SkipReason", descriptor.SkipReason));
        }

        if (descriptor.IsExplicit)
        {
            testCase.Traits.Add(new Trait("Explicit", "true"));
            if (descriptor.ExplicitReason is not null)
            {
                testCase.Traits.Add(new Trait("ExplicitReason", descriptor.ExplicitReason));
            }
        }
    }
}
