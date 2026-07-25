using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.TestFramework;
using Microsoft.Testing.Platform.Messages;
using Microsoft.Testing.Platform.Requests;
using Microsoft.Testing.Platform.TestHost;
using NextUnit.Internal;

namespace NextUnit.Platform;

/// <summary>
/// Represents the NextUnit test framework implementation for Microsoft.Testing.Platform.
/// </summary>
/// <remarks>
/// This class integrates the NextUnit testing framework with the Microsoft.Testing.Platform infrastructure,
/// providing test discovery and execution capabilities.
/// </remarks>
internal sealed class NextUnitFramework :
    ITestFramework,
    IDataProducer
{
    // TODO M4: _services will be used for dependency injection and service resolution
#pragma warning disable IDE0052 // Remove unread private members
    private readonly IServiceProvider _services;
#pragma warning restore IDE0052
    private readonly TestExecutionEngine _engine = new();
    private IReadOnlyList<TestCaseDescriptor>? _testCases;
    private readonly TestFilterConfiguration _filterConfig;
    private bool _sessionSetupExecuted;
    private bool _assemblyLifecycleInitialized;
    private readonly List<LifecycleMethodDelegate> _sessionBeforeMethods = new();
    private readonly List<LifecycleMethodDelegate> _sessionAfterMethods = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="NextUnitFramework"/> class.
    /// </summary>
    /// <param name="capabilities">The test framework capabilities.</param>
    /// <param name="services">The service provider for dependency injection.</param>
    public NextUnitFramework(
        ITestFrameworkCapabilities capabilities,
        IServiceProvider services)
    {
        _services = services;
        _ = capabilities; // Suppress unused parameter warning
        _filterConfig = TestFilterConfigurationLoader.Load(services);
    }

    /// <summary>
    /// Gets the unique identifier for the NextUnit framework.
    /// </summary>
    public string Uid => "NextUnit.Framework";

    /// <summary>
    /// Gets the version of the NextUnit framework.
    /// </summary>
    public string Version => "1.2.0";

    /// <summary>
    /// Gets the display name of the NextUnit framework.
    /// </summary>
    public string DisplayName => "NextUnit";

    /// <summary>
    /// Gets the description of the NextUnit framework.
    /// </summary>
    public string Description => "Next-gen .NET 10 test framework built on Microsoft.Testing.Platform";

    /// <summary>
    /// Determines whether the framework is enabled.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation. The task result contains <c>true</c> if the framework is enabled; otherwise, <c>false</c>.</returns>
    public Task<bool> IsEnabledAsync() => Task.FromResult(true);

    /// <summary>
    /// Gets the types of data produced by this framework.
    /// </summary>
    public Type[] DataTypesProduced =>
    [
        typeof(TestNodeUpdateMessage)
    ];

    /// <summary>
    /// Creates a new test session.
    /// </summary>
    /// <param name="context">The context for creating the test session.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the result of the test session creation.</returns>
    public async Task<CreateTestSessionResult> CreateTestSessionAsync(CreateTestSessionContext context)
    {
        // Ensure test cases and global lifecycle methods are loaded
        var testCases = GetTestCases();
        if (testCases.Count > 0 && !_sessionSetupExecuted)
        {
            // Session lifecycle methods are now collected globally via GetTestCases
            // Execute session setup methods
            await ExecuteSessionSetupAsync(context.CancellationToken).ConfigureAwait(false);
            _sessionSetupExecuted = true;
        }

        return new CreateTestSessionResult { IsSuccess = true };
    }

    /// <summary>
    /// Executes a test request, either for discovery or execution.
    /// </summary>
    /// <param name="context">The context for executing the request, containing the request type and communication channels.</param>
    /// <returns>A task that represents the asynchronous execution operation.</returns>
    public async Task ExecuteRequestAsync(ExecuteRequestContext context)
    {
        switch (context.Request)
        {
            case DiscoverTestExecutionRequest discover:
                await DiscoverAsync(discover, context.MessageBus, context.CancellationToken).ConfigureAwait(false);
                break;

            case RunTestExecutionRequest run:
                await RunAsync(run, context.MessageBus, context.CancellationToken).ConfigureAwait(false);
                break;
        }

        context.Complete();
    }

    /// <summary>
    /// Closes the current test session and performs cleanup.
    /// </summary>
    /// <param name="context">The context for closing the test session.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the result of the test session closure.</returns>
    public async Task<CloseTestSessionResult> CloseTestSessionAsync(CloseTestSessionContext context)
    {
        // Execute session teardown methods
        await ExecuteSessionTeardownAsync(context.CancellationToken).ConfigureAwait(false);

        return new CloseTestSessionResult { IsSuccess = true };
    }

    private IReadOnlyList<TestCaseDescriptor> GetTestCases()
    {
        if (_testCases is not null)
        {
            return _testCases;
        }

        var generatedRegistry = GeneratedTestRegistryStore.Current;
        if (generatedRegistry is null)
        {
            _testCases = Array.Empty<TestCaseDescriptor>();
            return _testCases;
        }

        var allTestCases = new List<TestCaseDescriptor>();

        allTestCases.AddRange(generatedRegistry.TestCases);

        AddFilteredExpansion(
            generatedRegistry.TestDataDescriptors,
            td => _filterConfig.ShouldExpandDynamicTest(td.Categories, td.Tags, td.DisplayName, td.IsExplicit),
            TestDataExpander.Expand,
            allTestCases);

        AddFilteredExpansion(
            generatedRegistry.ClassDataSourceDescriptors,
            cd => _filterConfig.ShouldExpandDynamicTest(cd.Categories, cd.Tags, cd.DisplayName, cd.IsExplicit),
            ClassDataSourceExpander.Expand,
            allTestCases);

        AddFilteredExpansion(
            generatedRegistry.CombinedDataSourceDescriptors,
            cd => _filterConfig.ShouldExpandDynamicTest(cd.Categories, cd.Tags, cd.DisplayName, cd.IsExplicit),
            CombinedDataSourceExpander.Expand,
            allTestCases);

        // Apply category and tag filtering to static test cases
        var filteredTestCases = allTestCases.Where(tc => _filterConfig.ShouldIncludeTest(tc.Categories, tc.Tags, tc.DisplayName, tc.IsExplicit)).ToList();

        // Get global lifecycle methods from the registry and set on engine (one-time)
        if (!_assemblyLifecycleInitialized)
        {
            _engine.SetGlobalAssemblyLifecycle(
                generatedRegistry.GlobalBeforeAssemblyMethods,
                generatedRegistry.GlobalAfterAssemblyMethods);
            _sessionBeforeMethods.AddRange(generatedRegistry.GlobalBeforeSessionMethods);
            _sessionAfterMethods.AddRange(generatedRegistry.GlobalAfterSessionMethods);

            _assemblyLifecycleInitialized = true;
        }

        _testCases = filteredTestCases;
        return _testCases;
    }

    /// <summary>
    /// Filters descriptors before expanding them, so data providers are never executed for tests the
    /// current filter excludes, and appends the expansion result.
    /// </summary>
    private static void AddFilteredExpansion<TDescriptor>(
        IReadOnlyList<TDescriptor> descriptors,
        Func<TDescriptor, bool> shouldExpand,
        Func<IEnumerable<TDescriptor>, IEnumerable<TestCaseDescriptor>> expand,
        List<TestCaseDescriptor> destination)
    {
        if (descriptors.Count == 0)
        {
            return;
        }

        var filteredDescriptors = descriptors.Where(shouldExpand).ToList();
        destination.AddRange(expand(filteredDescriptors));
    }

    private async Task ExecuteSessionSetupAsync(CancellationToken cancellationToken)
    {
        // Session lifecycle methods MUST be static (enforced by generator/runtime)
        // The null! instance parameter is safe because generated delegates for static methods
        // do not use the instance parameter - they call TypeName.Method() directly
        foreach (var beforeMethod in _sessionBeforeMethods)
        {
            await beforeMethod(null!, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task ExecuteSessionTeardownAsync(CancellationToken cancellationToken)
    {
        // Session lifecycle methods MUST be static (enforced by generator/runtime)
        // The null! instance parameter is safe because generated delegates for static methods
        // do not use the instance parameter - they call TypeName.Method() directly
        // Execute session teardown methods in reverse order
        for (int i = _sessionAfterMethods.Count - 1; i >= 0; i--)
        {
            await _sessionAfterMethods[i](null!, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task DiscoverAsync(
        DiscoverTestExecutionRequest request,
        IMessageBus messageBus,
        CancellationToken cancellationToken)
    {
        var testCases = GetTestCases();

        foreach (var testCase in testCases)
        {
            var testNode = TestNodeFactory.Create(testCase);

            await messageBus.PublishAsync(
                this,
                new TestNodeUpdateMessage(
                    request.Session.SessionUid,
                    testNode)).ConfigureAwait(false);

            cancellationToken.ThrowIfCancellationRequested();
        }
    }

    private async Task RunAsync(
        RunTestExecutionRequest request,
        IMessageBus messageBus,
        CancellationToken cancellationToken)
    {
        var testCases = GetTestCases();
        var sink = new MessageBusSink(messageBus, request.Session.SessionUid, this);

        await _engine.RunAsync(testCases, sink, cancellationToken).ConfigureAwait(false);
    }

    private sealed class MessageBusSink : ITestExecutionSink
    {
        private readonly IMessageBus _messageBus;
        private readonly SessionUid _sessionUid;
        private readonly IDataProducer _producer;

        public MessageBusSink(IMessageBus messageBus, SessionUid sessionUid, IDataProducer producer)
        {
            _messageBus = messageBus;
            _sessionUid = sessionUid;
            _producer = producer;
        }

        public Task ReportPassedAsync(TestCaseDescriptor test, string? output = null, IReadOnlyList<Artifact>? artifacts = null) =>
            PublishStateAsync(test, PassedTestNodeStateProperty.CachedInstance, output, artifacts);

        public Task ReportFailedAsync(TestCaseDescriptor test, AssertionFailedException ex, string? output = null, IReadOnlyList<Artifact>? artifacts = null) =>
            PublishStateAsync(test, new FailedTestNodeStateProperty(ex.Message), output, artifacts);

        public Task ReportErrorAsync(TestCaseDescriptor test, Exception ex, string? output = null, IReadOnlyList<Artifact>? artifacts = null) =>
            PublishStateAsync(test, new ErrorTestNodeStateProperty(ex), output, artifacts);

        public Task ReportSkippedAsync(TestCaseDescriptor test, IReadOnlyList<Artifact>? artifacts = null) =>
            PublishStateAsync(
                test,
                new SkippedTestNodeStateProperty(test.SkipReason ?? "Test was skipped"),
                output: null,
                artifacts);

        /// <summary>
        /// Publishes one test node state to the platform message bus.
        /// </summary>
        /// <remarks>
        /// The state property must come first in the list: Microsoft.Testing.Platform reads the
        /// outcome from the first state property it finds on the node.
        /// </remarks>
        private async Task PublishStateAsync(
            TestCaseDescriptor test,
            IProperty state,
            string? output,
            IReadOnlyList<Artifact>? artifacts)
        {
            var properties = new List<IProperty> { state };

            if (!string.IsNullOrEmpty(output))
            {
                properties.Add(new TestMetadataProperty("TestOutput", output));
            }

            AddArtifactProperties(properties, artifacts);

            var testNode = TestNodeFactory.Create(test, properties);

            await _messageBus.PublishAsync(
                _producer,
                new TestNodeUpdateMessage(
                    _sessionUid,
                    testNode)).ConfigureAwait(false);
        }

        private static void AddArtifactProperties(List<IProperty> properties, IReadOnlyList<Artifact>? artifacts)
        {
            if (artifacts is null || artifacts.Count == 0)
            {
                return;
            }

            // Add artifact file paths as metadata
            // Microsoft.Testing.Platform's TestFileArtifact requires specific API version
            // For now, add as metadata properties
            for (var i = 0; i < artifacts.Count; i++)
            {
                var artifact = artifacts[i];
                properties.Add(new TestMetadataProperty($"Artifact[{i}].FilePath", artifact.FilePath));
                if (artifact.Description is not null)
                {
                    properties.Add(new TestMetadataProperty($"Artifact[{i}].Description", artifact.Description));
                }
            }
        }

    }
}
