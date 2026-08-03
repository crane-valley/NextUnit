using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
using NextUnit.Internal;

namespace NextUnit.TestAdapter;

/// <summary>
/// VSTest adapter for executing NextUnit tests.
/// </summary>
/// <remarks>
/// Session-scoped lifecycle hooks are a known limitation of this adapter: it runs assembly-scoped
/// <c>[Before]</c> and <c>[After]</c> hooks only, so <c>[Before(LifecycleScope.Session)]</c> and
/// <c>[After(LifecycleScope.Session)]</c> do not run under VSTest. They do run under
/// Microsoft.Testing.Platform. VSTest executes per assembly and has no session boundary to map them
/// onto, so wiring them would require choosing between once-per-session and once-per-assembly
/// semantics; that choice is deferred until a concrete need defines it.
/// </remarks>
[ExtensionUri(ExecutorUri)]
public sealed class NextUnitTestExecutor : ITestExecutor
{
    /// <summary>
    /// The executor URI used to identify this adapter.
    /// </summary>
    public const string ExecutorUri = "executor://NextUnitTestExecutor/v1";

    private CancellationTokenSource? _cancellationTokenSource;

    /// <summary>
    /// Runs all tests from the specified sources.
    /// </summary>
    public void RunTests(
        IEnumerable<string>? sources,
        IRunContext? runContext,
        IFrameworkHandle? frameworkHandle)
    {
        if (sources == null || frameworkHandle == null)
        {
            return;
        }

        _cancellationTokenSource?.Dispose();
        _cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = _cancellationTokenSource.Token;

        foreach (var source in sources)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            try
            {
                RunTestsInAssembly(source, null, frameworkHandle, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                if (ExceptionHelper.IsCriticalException(ex))
                {
                    throw;
                }

                // Intentionally catch broadly to prevent a single bad assembly from
                // aborting execution of all test sources, but preserve full diagnostics
                AdapterDiagnostics.ReportSourceFailure(frameworkHandle, "running tests", source, ex);
            }
        }
    }

    /// <summary>
    /// Runs the specified tests.
    /// </summary>
    public void RunTests(
        IEnumerable<TestCase>? tests,
        IRunContext? runContext,
        IFrameworkHandle? frameworkHandle)
    {
        if (tests == null || frameworkHandle == null)
        {
            return;
        }

        _cancellationTokenSource?.Dispose();
        _cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = _cancellationTokenSource.Token;

        // Group tests by source
        var testsBySource = tests.GroupBy(t => t.Source);

        foreach (var sourceGroup in testsBySource)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            try
            {
                var testIds = sourceGroup.Select(t => t.FullyQualifiedName).ToHashSet();
                RunTestsInAssembly(sourceGroup.Key, testIds, frameworkHandle, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                if (ExceptionHelper.IsCriticalException(ex))
                {
                    throw;
                }

                // Intentionally catch broadly to prevent a single bad assembly from
                // aborting execution of all test sources, but preserve full diagnostics
                AdapterDiagnostics.ReportSourceFailure(frameworkHandle, "running tests", sourceGroup.Key, ex);
            }
        }
    }

    /// <summary>
    /// Cancels the test run.
    /// </summary>
    public void Cancel()
    {
        _cancellationTokenSource?.Cancel();
    }

    private void RunTestsInAssembly(
        string source,
        HashSet<string>? testIdsToRun,
        IFrameworkHandle frameworkHandle,
        CancellationToken cancellationToken)
    {
        var registryType = RegistryDescriptorReader.TryResolveRegistryType(source, frameworkHandle);
        if (registryType is null)
        {
            return;
        }

        // Collect all test cases
        var allTestCases = new List<TestCaseDescriptor>();
        var selectedDescriptorIds = testIdsToRun is null
            ? null
            : BuildSelectedDescriptorIds(testIdsToRun);

        // Get static TestCases
        var testCases = RegistryDescriptorReader.ReadDescriptors<TestCaseDescriptor>(registryType, "TestCases");
        if (testCases is not null)
        {
            allTestCases.AddRange(testCases);
        }

        // The run token is threaded into [TestData] expansion so an asynchronous data source is
        // interruptible; TestDataExpander does the blocking drain that this synchronous contract
        // requires, in one documented place rather than here.
        AddExpandedTests<TestDataDescriptor>(
            registryType,
            "TestDataDescriptors",
            selectedDescriptorIds,
            descriptor => descriptor.BaseId,
            descriptor => descriptor.IsExplicit,
            descriptors => TestDataExpander.Expand(descriptors, cancellationToken),
            allTestCases);

        AddExpandedTests<ClassDataSourceDescriptor>(
            registryType,
            "ClassDataSourceDescriptors",
            selectedDescriptorIds,
            descriptor => descriptor.BaseId,
            descriptor => descriptor.IsExplicit,
            ClassDataSourceExpander.Expand,
            allTestCases);

        AddExpandedTests<CombinedDataSourceDescriptor>(
            registryType,
            "CombinedDataSourceDescriptors",
            selectedDescriptorIds,
            descriptor => descriptor.BaseId,
            descriptor => descriptor.IsExplicit,
            CombinedDataSourceExpander.Expand,
            allTestCases);

        // Filter tests if specific tests were requested
        if (testIdsToRun != null)
        {
            allTestCases = allTestCases.Where(t => testIdsToRun.Contains(t.Id.Value)).ToList();
        }
        else
        {
            // When running all tests (no specific selection), exclude explicit tests by default
            // This matches the behavior of the Platform CLI without --explicit flag
            // Users can still run explicit tests by selecting them specifically in Test Explorer
            allTestCases = allTestCases.Where(t => !t.IsExplicit).ToList();
        }

        // Create execution engine and run tests
        var engine = new TestExecutionEngine();
        var sink = new VSTestResultSink(frameworkHandle, source);

        // Get global assembly lifecycle methods from the registry
        var globalBeforeAssembly = AssemblyLoader.GetStaticPropertyValue<LifecycleMethodDelegate[]>(
            registryType, "GlobalBeforeAssemblyMethods");
        var globalAfterAssembly = AssemblyLoader.GetStaticPropertyValue<LifecycleMethodDelegate[]>(
            registryType, "GlobalAfterAssemblyMethods");
        engine.SetGlobalAssemblyLifecycle(globalBeforeAssembly, globalAfterAssembly);

        // Run tests synchronously (VSTest expects this)
        engine.RunAsync(allTestCases, sink, cancellationToken).GetAwaiter().GetResult();
    }

    private static void AddExpandedTests<TDescriptor>(
        Type registryType,
        string propertyName,
        HashSet<string>? selectedDescriptorIds,
        Func<TDescriptor, string> baseIdSelector,
        Func<TDescriptor, bool> isExplicitSelector,
        Func<IEnumerable<TDescriptor>, IEnumerable<TestCaseDescriptor>> expand,
        List<TestCaseDescriptor> destination)
    {
        var descriptors = RegistryDescriptorReader.ReadDescriptors<TDescriptor>(registryType, propertyName);
        if (descriptors is null)
        {
            return;
        }

        var descriptorsToExpand = RegistryDescriptorReader.SelectDescriptorsToExpand(
            descriptors, selectedDescriptorIds, baseIdSelector, isExplicitSelector);

        destination.AddRange(expand(descriptorsToExpand.ToList()));
    }

    internal static HashSet<string> BuildSelectedDescriptorIds(IEnumerable<string> selectedTestIds)
    {
        var descriptorIds = new HashSet<string>(StringComparer.Ordinal);

        foreach (var testId in selectedTestIds)
        {
            descriptorIds.Add(testId);

            var separatorIndex = testId.IndexOf(':', StringComparison.Ordinal);
            if (separatorIndex > 0)
            {
                descriptorIds.Add(testId.Substring(0, separatorIndex));
            }
        }

        return descriptorIds;
    }

    private sealed class VSTestResultSink : ITestExecutionSink
    {
        private readonly IFrameworkHandle _frameworkHandle;
        private readonly string _source;

        public VSTestResultSink(IFrameworkHandle frameworkHandle, string source)
        {
            _frameworkHandle = frameworkHandle;
            _source = source;
        }

        public Task ReportPassedAsync(TestCaseDescriptor test, string? output = null, IReadOnlyList<Artifact>? artifacts = null) =>
            RecordResult(test, Microsoft.VisualStudio.TestPlatform.ObjectModel.TestOutcome.Passed, output, artifacts);

        public Task ReportFailedAsync(TestCaseDescriptor test, AssertionFailedException ex, string? output = null, IReadOnlyList<Artifact>? artifacts = null) =>
            RecordResult(test, Microsoft.VisualStudio.TestPlatform.ObjectModel.TestOutcome.Failed, output, artifacts, ex.Message, ex.StackTrace);

        public Task ReportErrorAsync(TestCaseDescriptor test, Exception ex, string? output = null, IReadOnlyList<Artifact>? artifacts = null) =>
            RecordResult(test, Microsoft.VisualStudio.TestPlatform.ObjectModel.TestOutcome.Failed, output, artifacts, ex.Message, ex.StackTrace);

        public Task ReportSkippedAsync(TestCaseDescriptor test, IReadOnlyList<Artifact>? artifacts = null) =>
            RecordResult(
                test,
                Microsoft.VisualStudio.TestPlatform.ObjectModel.TestOutcome.Skipped,
                output: null,
                artifacts,
                errorMessage: test.SkipReason ?? "Test was skipped");

        /// <summary>
        /// Records one outcome against the VSTest framework handle.
        /// </summary>
        /// <remarks>
        /// Duration is always zero: the engine owns timing and does not report it through this
        /// sink, so reporting anything else here would be invented data.
        /// </remarks>
        private Task RecordResult(
            TestCaseDescriptor test,
            Microsoft.VisualStudio.TestPlatform.ObjectModel.TestOutcome outcome,
            string? output,
            IReadOnlyList<Artifact>? artifacts,
            string? errorMessage = null,
            string? errorStackTrace = null)
        {
            var vsTestCase = VSTestCaseFactory.Create(test, _source, includeTraits: false);
            var result = new TestResult(vsTestCase)
            {
                Outcome = outcome,
                ErrorMessage = errorMessage,
                ErrorStackTrace = errorStackTrace,
                Duration = TimeSpan.Zero
            };

            if (!string.IsNullOrEmpty(output))
            {
                result.Messages.Add(new TestResultMessage(TestResultMessage.StandardOutCategory, output));
            }

            AttachArtifacts(result, artifacts);
            _frameworkHandle.RecordResult(result);
            return Task.CompletedTask;
        }

        private static void AttachArtifacts(TestResult result, IReadOnlyList<Artifact>? artifacts)
        {
            if (artifacts is null || artifacts.Count == 0)
            {
                return;
            }

            var attachmentSet = new AttachmentSet(
                new Uri("nextunit://test-artifacts"),
                "NextUnit Test Artifacts");

            var attachments = artifacts.Select(artifact =>
            {
                // First try to interpret the value as an absolute URI; if that fails, treat it as a file path.
                if (!Uri.TryCreate(artifact.FilePath, UriKind.Absolute, out var artifactUri))
                {
                    artifactUri = new Uri(new Uri("file://"), artifact.FilePath);
                }

                return new UriDataAttachment(
                    artifactUri,
                    artifact.Description ?? Path.GetFileName(artifact.FilePath));
            });

            foreach (var attachment in attachments)
            {
                attachmentSet.Attachments.Add(attachment);
            }

            result.Attachments.Add(attachmentSet);
        }
    }
}
