using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;
using NextUnit.Internal;

namespace NextUnit.TestAdapter;

/// <summary>
/// VSTest adapter for executing NextUnit tests.
/// </summary>
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
                frameworkHandle.SendMessage(
                    TestMessageLevel.Error,
                    $"NextUnit: Error running tests in {source}: {ex.GetType().FullName}: {ex}");
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
                frameworkHandle.SendMessage(
                    TestMessageLevel.Error,
                    $"NextUnit: Error running tests in {sourceGroup.Key}: {ex.GetType().FullName}: {ex}");
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
        var loadResult = AssemblyLoader.TryLoadAssembly(source);
        if (!loadResult.Success)
        {
            if (loadResult.ErrorMessage is not null)
            {
                frameworkHandle.SendMessage(
                    TestMessageLevel.Warning,
                    $"NextUnit: Could not load assembly {source} ({loadResult.ErrorCategory}): {loadResult.ErrorMessage}");
            }
            return;
        }

        // Look for NextUnit.Generated.GeneratedTestRegistry
        var registryType = AssemblyLoader.GetTestRegistryType(loadResult.Assembly!);
        if (registryType is null)
        {
            return;
        }

        // Collect all test cases
        var allTestCases = new List<TestCaseDescriptor>();

        // Get static TestCases
        var testCases = AssemblyLoader.GetStaticPropertyValue<IReadOnlyList<TestCaseDescriptor>>(registryType, "TestCases");
        if (testCases is not null)
        {
            allTestCases.AddRange(testCases);
        }

        // Get TestDataDescriptors and expand them
        var testDataDescriptors = AssemblyLoader.GetStaticPropertyValue<IReadOnlyList<TestDataDescriptor>>(registryType, "TestDataDescriptors");
        if (testDataDescriptors is not null)
        {
            // Filter descriptors before expansion to avoid invoking expensive data sources for unrelated tests
            IEnumerable<TestDataDescriptor> descriptorsToExpand = testDataDescriptors;
            if (testIdsToRun is not null)
            {
                descriptorsToExpand = testDataDescriptors.Where(d =>
                    testIdsToRun.Any(id => id.StartsWith(d.BaseId, StringComparison.Ordinal)));
            }

            var expandedTests = TestDataExpander.Expand(descriptorsToExpand.ToList());
            allTestCases.AddRange(expandedTests);
        }

        // Get ClassDataSourceDescriptors and expand them
        var classDataSourceDescriptors = AssemblyLoader.GetStaticPropertyValue<IReadOnlyList<ClassDataSourceDescriptor>>(registryType, "ClassDataSourceDescriptors");
        if (classDataSourceDescriptors is not null)
        {
            // Filter descriptors before expansion to avoid instantiating expensive data sources for unrelated tests
            IEnumerable<ClassDataSourceDescriptor> descriptorsToExpand = classDataSourceDescriptors;
            if (testIdsToRun is not null)
            {
                descriptorsToExpand = classDataSourceDescriptors.Where(d =>
                    testIdsToRun.Any(id => id.StartsWith(d.BaseId, StringComparison.Ordinal)));
            }

            var expandedTests = ClassDataSourceExpander.Expand(descriptorsToExpand.ToList());
            allTestCases.AddRange(expandedTests);
        }

        // Filter tests if specific tests were requested
        if (testIdsToRun != null)
        {
            allTestCases = allTestCases.Where(t => testIdsToRun.Contains(t.Id.Value)).ToList();
        }

        // Create execution engine and run tests
        var engine = new TestExecutionEngine();
        var sink = new VSTestResultSink(frameworkHandle, source);

        // Run tests synchronously (VSTest expects this)
        engine.RunAsync(allTestCases, sink, cancellationToken).GetAwaiter().GetResult();
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

        public Task ReportPassedAsync(TestCaseDescriptor test, string? output = null)
        {
            var vsTestCase = VSTestCaseFactory.Create(test, _source, includeTraits: false);
            var result = new TestResult(vsTestCase)
            {
                Outcome = Microsoft.VisualStudio.TestPlatform.ObjectModel.TestOutcome.Passed,
                Duration = TimeSpan.Zero
            };

            if (!string.IsNullOrEmpty(output))
            {
                result.Messages.Add(new TestResultMessage(TestResultMessage.StandardOutCategory, output));
            }

            _frameworkHandle.RecordResult(result);
            return Task.CompletedTask;
        }

        public Task ReportFailedAsync(TestCaseDescriptor test, AssertionFailedException ex, string? output = null)
        {
            var vsTestCase = VSTestCaseFactory.Create(test, _source, includeTraits: false);
            var result = new TestResult(vsTestCase)
            {
                Outcome = Microsoft.VisualStudio.TestPlatform.ObjectModel.TestOutcome.Failed,
                ErrorMessage = ex.Message,
                ErrorStackTrace = ex.StackTrace,
                Duration = TimeSpan.Zero
            };

            if (!string.IsNullOrEmpty(output))
            {
                result.Messages.Add(new TestResultMessage(TestResultMessage.StandardOutCategory, output));
            }

            _frameworkHandle.RecordResult(result);
            return Task.CompletedTask;
        }

        public Task ReportErrorAsync(TestCaseDescriptor test, Exception ex, string? output = null)
        {
            var vsTestCase = VSTestCaseFactory.Create(test, _source, includeTraits: false);
            var result = new TestResult(vsTestCase)
            {
                Outcome = Microsoft.VisualStudio.TestPlatform.ObjectModel.TestOutcome.Failed,
                ErrorMessage = ex.Message,
                ErrorStackTrace = ex.StackTrace,
                Duration = TimeSpan.Zero
            };

            if (!string.IsNullOrEmpty(output))
            {
                result.Messages.Add(new TestResultMessage(TestResultMessage.StandardOutCategory, output));
            }

            _frameworkHandle.RecordResult(result);
            return Task.CompletedTask;
        }

        public Task ReportSkippedAsync(TestCaseDescriptor test)
        {
            var vsTestCase = VSTestCaseFactory.Create(test, _source, includeTraits: false);
            var result = new TestResult(vsTestCase)
            {
                Outcome = Microsoft.VisualStudio.TestPlatform.ObjectModel.TestOutcome.Skipped,
                ErrorMessage = test.SkipReason ?? "Test was skipped",
                Duration = TimeSpan.Zero
            };

            _frameworkHandle.RecordResult(result);
            return Task.CompletedTask;
        }
    }
}
