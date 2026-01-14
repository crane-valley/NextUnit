using System.Reflection;
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
                frameworkHandle.SendMessage(TestMessageLevel.Error, $"NextUnit: Error running tests in {source}: {ex.Message}");
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
                frameworkHandle.SendMessage(TestMessageLevel.Error, $"NextUnit: Error running tests in {sourceGroup.Key}: {ex.Message}");
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
        if (!File.Exists(source))
        {
            return;
        }

        Assembly assembly;
        try
        {
            assembly = Assembly.LoadFrom(source);
        }
        catch (Exception ex)
        {
            frameworkHandle.SendMessage(TestMessageLevel.Warning, $"NextUnit: Could not load assembly {source}: {ex.Message}");
            return;
        }

        // Look for NextUnit.Generated.GeneratedTestRegistry
        var registryType = assembly.GetType("NextUnit.Generated.GeneratedTestRegistry");
        if (registryType == null)
        {
            return;
        }

        // Collect all test cases
        var allTestCases = new List<TestCaseDescriptor>();

        // Get static TestCases
        var testCasesProperty = registryType.GetProperty("TestCases", BindingFlags.Public | BindingFlags.Static);
        if (testCasesProperty != null)
        {
            var testCases = testCasesProperty.GetValue(null) as IReadOnlyList<TestCaseDescriptor>;
            if (testCases != null)
            {
                allTestCases.AddRange(testCases);
            }
        }

        // Get TestDataDescriptors and expand them
        var testDataDescriptorsProperty = registryType.GetProperty("TestDataDescriptors", BindingFlags.Public | BindingFlags.Static);
        if (testDataDescriptorsProperty != null)
        {
            var testDataDescriptors = testDataDescriptorsProperty.GetValue(null) as IReadOnlyList<TestDataDescriptor>;
            if (testDataDescriptors != null)
            {
                // Filter descriptors before expansion to avoid invoking expensive data sources for unrelated tests
                IEnumerable<TestDataDescriptor> descriptorsToExpand = testDataDescriptors;
                if (testIdsToRun != null)
                {
                    descriptorsToExpand = testDataDescriptors.Where(d =>
                        testIdsToRun.Any(id => id.StartsWith(d.BaseId, StringComparison.Ordinal)));
                }

                var expandedTests = TestDataExpander.Expand(descriptorsToExpand.ToList());
                allTestCases.AddRange(expandedTests);
            }
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
            var vsTestCase = CreateVSTestCase(test);
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
            var vsTestCase = CreateVSTestCase(test);
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
            var vsTestCase = CreateVSTestCase(test);
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
            var vsTestCase = CreateVSTestCase(test);
            var result = new TestResult(vsTestCase)
            {
                Outcome = Microsoft.VisualStudio.TestPlatform.ObjectModel.TestOutcome.Skipped,
                ErrorMessage = test.SkipReason ?? "Test was skipped",
                Duration = TimeSpan.Zero
            };

            _frameworkHandle.RecordResult(result);
            return Task.CompletedTask;
        }

        private TestCase CreateVSTestCase(TestCaseDescriptor descriptor)
        {
            return new TestCase(descriptor.Id.Value, new Uri(ExecutorUri), _source)
            {
                DisplayName = descriptor.DisplayName
            };
        }
    }
}
