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
                AdapterDiagnostics.ReportSourceFailure(logger, "discovering tests", source, ex);
            }
        }
    }

    private static void DiscoverTestsInAssembly(
        string source,
        IMessageLogger logger,
        ITestCaseDiscoverySink discoverySink)
    {
        var registryType = RegistryDescriptorReader.TryResolveRegistryType(source, logger);
        if (registryType is null)
        {
            return;
        }

        logger.SendMessage(TestMessageLevel.Informational, $"NextUnit: Found test registry in {Path.GetFileName(source)}");

        // Get TestCases property
        var testCases = RegistryDescriptorReader.ReadDescriptors<TestCaseDescriptor>(registryType, "TestCases");
        if (testCases is null)
        {
            logger.SendMessage(TestMessageLevel.Warning, "NextUnit: TestCases property not found or returned null");
            return;
        }

        logger.SendMessage(TestMessageLevel.Informational, $"NextUnit: Found {testCases.Count} static test cases");
        SendTestCases(testCases, source, discoverySink);

        // Discovery reports every descriptor, unfiltered: VSTest asks for the full list once and
        // applies its own filtering when the user later selects tests to run.
        //
        // CancellationToken.None is passed explicitly rather than by omission: ITestDiscoverer has
        // no cancellation contract at all -- unlike ITestExecutor, which owns a Cancel() call and
        // therefore a token to hand down -- so there is genuinely nothing to route here. An
        // asynchronous data source that blocks forever will block VSTest discovery. Under
        // Microsoft.Testing.Platform the request token flows all the way through, and that is the
        // path NextUnit's own runner uses.
        //
        // A source declared with DeferredEnumeration is reported as one placeholder test instead of
        // one test per row; the expander decides that, so nothing here has to. Selecting the
        // placeholder in an IDE runs every row of the source.
        DiscoverExpandedTests<TestDataDescriptor>(
            registryType,
            "TestDataDescriptors",
            "test data",
            descriptors => TestDataExpander.Expand(descriptors, CancellationToken.None),
            source,
            logger,
            discoverySink);

        DiscoverExpandedTests<ClassDataSourceDescriptor>(
            registryType, "ClassDataSourceDescriptors", "class data source", ClassDataSourceExpander.Expand, source, logger, discoverySink);

        DiscoverExpandedTests<CombinedDataSourceDescriptor>(
            registryType, "CombinedDataSourceDescriptors", "combined data source", CombinedDataSourceExpander.Expand, source, logger, discoverySink);
    }

    private static void DiscoverExpandedTests<TDescriptor>(
        Type registryType,
        string propertyName,
        string descriptorLabel,
        Func<IEnumerable<TDescriptor>, IEnumerable<TestCaseDescriptor>> expand,
        string source,
        IMessageLogger logger,
        ITestCaseDiscoverySink discoverySink)
    {
        var descriptors = RegistryDescriptorReader.ReadDescriptors<TDescriptor>(registryType, propertyName);
        if (descriptors is null || descriptors.Count == 0)
        {
            return;
        }

        logger.SendMessage(TestMessageLevel.Informational, $"NextUnit: Found {descriptors.Count} {descriptorLabel} descriptors");

        SendTestCases(expand(descriptors), source, discoverySink);
    }

    private static void SendTestCases(
        IEnumerable<TestCaseDescriptor> testCases,
        string source,
        ITestCaseDiscoverySink discoverySink)
    {
        foreach (var vsTestCase in testCases.Select(tc => VSTestCaseFactory.Create(tc, source)))
        {
            discoverySink.SendTestCase(vsTestCase);
        }
    }
}
