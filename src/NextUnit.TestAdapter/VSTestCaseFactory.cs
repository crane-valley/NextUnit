using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using NextUnit.Internal;

namespace NextUnit.TestAdapter;

/// <summary>
/// Factory for creating VSTest TestCase objects from NextUnit test descriptors.
/// </summary>
internal static class VSTestCaseFactory
{
    /// <summary>
    /// Creates a VSTest TestCase from a test descriptor.
    /// </summary>
    /// <param name="descriptor">The test case descriptor.</param>
    /// <param name="source">The source assembly path.</param>
    /// <param name="includeTraits">Whether to include category and tag traits. Default is true for discovery, false for execution results.</param>
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
