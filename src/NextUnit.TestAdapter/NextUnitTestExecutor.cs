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
/// <para>
/// Shared data source instances are not deferred the same way: they hold resources rather than user
/// code, so the end of a run stands in for the end of a session and <see cref="SharedInstanceCleanup"/>
/// releases them there.
/// </para>
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

        try
        {
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
        finally
        {
            // The whole run is the session equivalent here: VSTest has no session boundary, but the
            // end of the run is the point past which no test can still reach a shared instance. Every
            // source is covered at once rather than one at a time, because a PerSession instance is
            // keyed by data source type alone, so two sources referencing one shared library share it.
            SharedInstanceCleanup.Run(frameworkHandle);
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

        try
        {
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
        finally
        {
            // The whole run is the session equivalent here: VSTest has no session boundary, but the
            // end of the run is the point past which no test can still reach a shared instance. Every
            // source is covered at once rather than one at a time, because a PerSession instance is
            // keyed by data source type alone, so two sources referencing one shared library share it.
            SharedInstanceCleanup.Run(frameworkHandle);
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
        //
        // A deferred source still yields only its placeholder here, which is what makes the
        // testIdsToRun filter below work: discovery published that same placeholder id, so it is the
        // id the user can have selected. The execution engine replaces the placeholder with the real
        // rows once the selection has been applied.
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
            // Derived once rather than rescanned per placeholder: a selection can hold thousands of
            // ids, and matching each placeholder against all of them would grow with the product.
            var selectedRowGroupIds = BuildSelectedRowGroupIds(testIdsToRun);

            allTestCases = allTestCases
                .Where(t => testIdsToRun.Contains(t.Id.Value) || StandsForSelectedRow(t, selectedRowGroupIds))
                .ToList();
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

    /// <summary>
    /// Reports whether a deferred placeholder stands for one of the rows the user selected.
    /// </summary>
    /// <remarks>
    /// A deferred row's id comes into existence only when a run produces it, but VSTest remembers
    /// the test cases it saw in results, so the user can select one of those rows and run it again.
    /// Discovery still offers nothing but the placeholder, whose id is the row id without its
    /// <c>[index]</c> suffix, so an exact-id filter would drop it and the run would do nothing at
    /// all -- a silent no-op in Test Explorer. Selecting a row therefore reruns its whole group,
    /// which is the documented granularity of a deferred source.
    /// <para>
    /// The deferred check comes first so an ordinary test case, which is selected by its exact id,
    /// can never be pulled into a run by a row id that happens to extend it.
    /// </para>
    /// </remarks>
    internal static bool StandsForSelectedRow(TestCaseDescriptor testCase, HashSet<string> selectedRowGroupIds) =>
        testCase.DeferredDataSource is not null && selectedRowGroupIds.Contains(testCase.Id.Value);

    /// <summary>
    /// Maps every selected row id back to the id of the group it was expanded from.
    /// </summary>
    /// <remarks>
    /// A row id is its group's id followed by an <c>[index]</c> suffix, so dropping the suffix
    /// yields the id discovery published. Selected ids that do not end in such a suffix are not row
    /// ids and contribute nothing; they are already matched exactly by the caller.
    /// </remarks>
    internal static HashSet<string> BuildSelectedRowGroupIds(IEnumerable<string> selectedTestIds)
    {
        var groupIds = new HashSet<string>(StringComparer.Ordinal);

        foreach (var testId in selectedTestIds)
        {
            if (testId.Length == 0 || testId[^1] != ']')
            {
                continue;
            }

            var indexStart = testId.LastIndexOf('[');
            if (indexStart > 0)
            {
                groupIds.Add(testId.Substring(0, indexStart));
            }
        }

        return groupIds;
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
        /// <para>
        /// Traits are attached to the result's test case even though discovery already sent them for
        /// every test it reported. A deferred data source's rows are not among those: their ids come
        /// into existence during the run, so VSTest has no trait information for them at all, and
        /// omitting traits here would drop their categories and tags entirely. Repeating the traits
        /// for an already-discovered test costs a handful of properties and keeps every result
        /// self-describing regardless of when its test case first appeared.
        /// </para>
        /// </remarks>
        private Task RecordResult(
            TestCaseDescriptor test,
            Microsoft.VisualStudio.TestPlatform.ObjectModel.TestOutcome outcome,
            string? output,
            IReadOnlyList<Artifact>? artifacts,
            string? errorMessage = null,
            string? errorStackTrace = null)
        {
            var vsTestCase = VSTestCaseFactory.Create(test, _source);
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
