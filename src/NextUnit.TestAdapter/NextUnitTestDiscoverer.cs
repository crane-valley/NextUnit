using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;
using NextUnit.Internal;

namespace NextUnit.TestAdapter;

/// <summary>
/// VSTest adapter for discovering NextUnit tests.
/// </summary>
[DefaultExecutorUri(NextUnitTestExecutor.ExecutorUri)]
[FileExtension(".dll")]
[FileExtension(".exe")]
public sealed class NextUnitTestDiscoverer : ITestDiscoverer
{

    /// <summary>
    /// Discovers tests from the specified sources.
    /// </summary>
    public void DiscoverTests(
        IEnumerable<string> sources,
        IDiscoveryContext discoveryContext,
        IMessageLogger logger,
        ITestCaseDiscoverySink discoverySink)
    {
        logger.SendMessage(TestMessageLevel.Informational, "NextUnit: Starting test discovery");

        foreach (var source in sources)
        {
            try
            {
                DiscoverTestsInAssembly(source, logger, discoverySink);
            }
            catch (Exception ex)
            {
                if (ExceptionHelper.IsCriticalException(ex))
                {
                    throw;
                }

                // Intentionally catch broadly to prevent a single bad assembly from
                // aborting discovery of all test sources, but preserve full diagnostics
                logger.SendMessage(
                    TestMessageLevel.Error,
                    $"NextUnit: Error discovering tests in {source}: {ex.GetType().FullName}: {ex}");
            }
        }
    }

    private static void DiscoverTestsInAssembly(
        string source,
        IMessageLogger logger,
        ITestCaseDiscoverySink discoverySink)
    {
        var loadResult = AssemblyLoader.TryLoadAssembly(source);
        if (!loadResult.Success)
        {
            if (loadResult.ErrorMessage is not null)
            {
                logger.SendMessage(
                    TestMessageLevel.Warning,
                    $"NextUnit: Could not load assembly {source} ({loadResult.ErrorCategory}): {loadResult.ErrorMessage}");
            }
            return;
        }

        // Look for NextUnit.Generated.GeneratedTestRegistry
        var registryType = AssemblyLoader.GetTestRegistryType(loadResult.Assembly!);
        if (registryType is null)
        {
            // Not a NextUnit test assembly
            return;
        }

        logger.SendMessage(TestMessageLevel.Informational, $"NextUnit: Found test registry in {Path.GetFileName(source)}");

        // Get TestCases property
        var testCases = AssemblyLoader.GetStaticPropertyValue<IReadOnlyList<TestCaseDescriptor>>(registryType, "TestCases");
        if (testCases is null)
        {
            logger.SendMessage(TestMessageLevel.Warning, "NextUnit: TestCases property not found or empty");
            return;
        }

        logger.SendMessage(TestMessageLevel.Informational, $"NextUnit: Found {testCases.Count} static test cases");

        foreach (var vsTestCase in testCases.Select(tc => CreateVSTestCase(tc, source)))
        {
            discoverySink.SendTestCase(vsTestCase);
        }

        // Get TestDataDescriptors property for parameterized tests
        var testDataDescriptors = AssemblyLoader.GetStaticPropertyValue<IReadOnlyList<TestDataDescriptor>>(registryType, "TestDataDescriptors");
        if (testDataDescriptors is not null && testDataDescriptors.Count > 0)
        {
            logger.SendMessage(TestMessageLevel.Informational, $"NextUnit: Found {testDataDescriptors.Count} test data descriptors");

            // Expand TestDataDescriptors into TestCaseDescriptors
            var expandedTests = TestDataExpander.Expand(testDataDescriptors);
            foreach (var vsTestCase in expandedTests.Select(tc => CreateVSTestCase(tc, source)))
            {
                discoverySink.SendTestCase(vsTestCase);
            }
        }
    }

    private static TestCase CreateVSTestCase(TestCaseDescriptor descriptor, string source)
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
}
