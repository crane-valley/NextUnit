using System.Reflection;
using System.Security;
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
                // Rethrow critical exceptions that should not be suppressed
                if (ex is OutOfMemoryException or ThreadAbortException)
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
        if (!File.Exists(source))
        {
            return;
        }

        Assembly assembly;
        try
        {
            assembly = Assembly.LoadFrom(source);
        }
        catch (FileNotFoundException ex)
        {
            logger.SendMessage(TestMessageLevel.Warning, $"NextUnit: Could not load assembly {source} (file not found): {ex.Message}");
            return;
        }
        catch (BadImageFormatException ex)
        {
            logger.SendMessage(TestMessageLevel.Warning, $"NextUnit: Could not load assembly {source} (bad image format): {ex.Message}");
            return;
        }
        catch (FileLoadException ex)
        {
            logger.SendMessage(TestMessageLevel.Warning, $"NextUnit: Could not load assembly {source} (file load error): {ex.Message}");
            return;
        }
        catch (IOException ex)
        {
            logger.SendMessage(TestMessageLevel.Warning, $"NextUnit: Could not load assembly {source} (I/O error): {ex.Message}");
            return;
        }
        catch (UnauthorizedAccessException ex)
        {
            logger.SendMessage(TestMessageLevel.Warning, $"NextUnit: Could not load assembly {source} (access denied): {ex.Message}");
            return;
        }
        catch (SecurityException ex)
        {
            logger.SendMessage(TestMessageLevel.Warning, $"NextUnit: Could not load assembly {source} (security error): {ex.Message}");
            return;
        }

        // Look for NextUnit.Generated.GeneratedTestRegistry
        var registryType = assembly.GetType("NextUnit.Generated.GeneratedTestRegistry");
        if (registryType == null)
        {
            // Not a NextUnit test assembly
            return;
        }

        logger.SendMessage(TestMessageLevel.Informational, $"NextUnit: Found test registry in {Path.GetFileName(source)}");

        // Get TestCases property
        var testCasesProperty = registryType.GetProperty("TestCases", BindingFlags.Public | BindingFlags.Static);
        if (testCasesProperty == null)
        {
            logger.SendMessage(TestMessageLevel.Warning, "NextUnit: TestCases property not found");
            return;
        }

        var testCases = testCasesProperty.GetValue(null) as IReadOnlyList<TestCaseDescriptor>;
        if (testCases == null)
        {
            logger.SendMessage(TestMessageLevel.Warning, "NextUnit: Could not get test cases");
            return;
        }

        logger.SendMessage(TestMessageLevel.Informational, $"NextUnit: Found {testCases.Count} static test cases");

        foreach (var vsTestCase in testCases.Select(tc => CreateVSTestCase(tc, source)))
        {
            discoverySink.SendTestCase(vsTestCase);
        }

        // Get TestDataDescriptors property for parameterized tests
        var testDataDescriptorsProperty = registryType.GetProperty("TestDataDescriptors", BindingFlags.Public | BindingFlags.Static);
        if (testDataDescriptorsProperty != null)
        {
            var testDataDescriptors = testDataDescriptorsProperty.GetValue(null) as IReadOnlyList<TestDataDescriptor>;
            if (testDataDescriptors != null && testDataDescriptors.Count > 0)
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
