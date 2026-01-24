using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using NextUnit.Internal;

namespace NextUnit.TestAdapter;

/// <summary>
/// Factory for creating VSTest TestCase objects from NextUnit test descriptors.
/// Centralizes TestCase creation to ensure consistent behavior between discovery and execution.
/// </summary>
internal static class VSTestCaseFactory
{
    /// <summary>
    /// Creates a VSTest TestCase from a NextUnit TestCaseDescriptor with full trait support.
    /// Used during test discovery to provide complete metadata to the Test Explorer.
    /// </summary>
    /// <param name="descriptor">The NextUnit test case descriptor.</param>
    /// <param name="source">The assembly path containing the test.</param>
    /// <returns>A fully populated VSTest TestCase.</returns>
    public static TestCase CreateForDiscovery(TestCaseDescriptor descriptor, string source)
    {
        var testCase = new TestCase(descriptor.Id.Value, new Uri(NextUnitTestExecutor.ExecutorUri), source)
        {
            DisplayName = descriptor.DisplayName,
            CodeFilePath = null, // Could be populated if we had source info
            LineNumber = 0
        };

        // Add traits for categories and tags
        foreach (var category in descriptor.Categories)
        {
            testCase.Traits.Add(new Trait("Category", category));
        }

        foreach (var tag in descriptor.Tags)
        {
            testCase.Traits.Add(new Trait("Tag", tag));
        }

        return testCase;
    }

    /// <summary>
    /// Creates a VSTest TestCase from a NextUnit TestCaseDescriptor for result reporting.
    /// Used during test execution to associate results with the correct test.
    /// </summary>
    /// <param name="descriptor">The NextUnit test case descriptor.</param>
    /// <param name="source">The assembly path containing the test.</param>
    /// <returns>A VSTest TestCase suitable for result reporting.</returns>
    public static TestCase CreateForExecution(TestCaseDescriptor descriptor, string source)
    {
        return new TestCase(descriptor.Id.Value, new Uri(NextUnitTestExecutor.ExecutorUri), source)
        {
            DisplayName = descriptor.DisplayName
        };
    }
}
