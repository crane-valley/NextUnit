using System.Reflection;
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
        _filterConfig = LoadFilterConfiguration();
    }

    /// <summary>
    /// Gets the unique identifier for the NextUnit framework.
    /// </summary>
    public string Uid => "NextUnit.Framework";

    /// <summary>
    /// Gets the version of the NextUnit framework.
    /// </summary>
    public string Version => "1.1.0";

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
    public Task<CreateTestSessionResult> CreateTestSessionAsync(CreateTestSessionContext context)
    {
        _ = context; // TODO: Will be used in future implementation
        return Task.FromResult(new CreateTestSessionResult { IsSuccess = true });
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
    public Task<CloseTestSessionResult> CloseTestSessionAsync(CloseTestSessionContext context)
    {
        _ = context; // TODO: Will be used in future implementation
        return Task.FromResult(new CloseTestSessionResult { IsSuccess = true });
    }

    private IReadOnlyList<TestCaseDescriptor> GetTestCases()
    {
        if (_testCases is not null)
        {
            return _testCases;
        }

        // Try to find generated test registry using reflection
        // This is a one-time operation during test discovery, not during test execution
        var testAssemblyPath = Environment.GetCommandLineArgs()[0];
        var testAssembly = Assembly.LoadFrom(testAssemblyPath);

        // Look for NextUnit.Generated.GeneratedTestRegistry
        var generatedRegistryType = testAssembly.GetType("NextUnit.Generated.GeneratedTestRegistry");
        if (generatedRegistryType is null)
        {
            _testCases = Array.Empty<TestCaseDescriptor>();
            return _testCases;
        }

        var allTestCases = new List<TestCaseDescriptor>();

        // Get static test cases from TestCases property
        var testCasesProperty = generatedRegistryType.GetProperty(
            "TestCases",
            BindingFlags.Public | BindingFlags.Static);

        if (testCasesProperty is not null)
        {
            var staticTestCases = (IReadOnlyList<TestCaseDescriptor>?)testCasesProperty.GetValue(null);
            if (staticTestCases is not null)
            {
                allTestCases.AddRange(staticTestCases);
            }
        }

        // Get dynamic test cases from TestDataDescriptors property
        var testDataDescriptorsProperty = generatedRegistryType.GetProperty(
            "TestDataDescriptors",
            BindingFlags.Public | BindingFlags.Static);

        if (testDataDescriptorsProperty is not null)
        {
            var testDataDescriptors = (IReadOnlyList<TestDataDescriptor>?)testDataDescriptorsProperty.GetValue(null);
            if (testDataDescriptors is not null)
            {
                // Expand TestDataDescriptors into TestCaseDescriptors at runtime
                var expandedTests = TestDataExpander.Expand(testDataDescriptors);
                allTestCases.AddRange(expandedTests);
            }
        }

        // Apply category and tag filtering
        var filteredTestCases = allTestCases.Where(tc => _filterConfig.ShouldIncludeTest(tc.Categories, tc.Tags)).ToList();

        _testCases = filteredTestCases;
        return _testCases;
    }

    private static TestFilterConfiguration LoadFilterConfiguration()
    {
        var config = new TestFilterConfiguration();

        // Load from environment variables (temporary solution until proper CLI integration)
        var includeCategories = Environment.GetEnvironmentVariable("NEXTUNIT_INCLUDE_CATEGORIES");
        if (!string.IsNullOrWhiteSpace(includeCategories))
        {
            config.IncludeCategories = includeCategories.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }

        var excludeCategories = Environment.GetEnvironmentVariable("NEXTUNIT_EXCLUDE_CATEGORIES");
        if (!string.IsNullOrWhiteSpace(excludeCategories))
        {
            config.ExcludeCategories = excludeCategories.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }

        var includeTags = Environment.GetEnvironmentVariable("NEXTUNIT_INCLUDE_TAGS");
        if (!string.IsNullOrWhiteSpace(includeTags))
        {
            config.IncludeTags = includeTags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }

        var excludeTags = Environment.GetEnvironmentVariable("NEXTUNIT_EXCLUDE_TAGS");
        if (!string.IsNullOrWhiteSpace(excludeTags))
        {
            config.ExcludeTags = excludeTags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }

        return config;
    }

    private async Task DiscoverAsync(
        DiscoverTestExecutionRequest request,
        IMessageBus messageBus,
        CancellationToken cancellationToken)
    {
        var testCases = GetTestCases();

        foreach (var testCase in testCases)
        {
            var testNode = new TestNode
            {
                Uid = new TestNodeUid(testCase.Id.Value),
                DisplayName = testCase.DisplayName,
                Properties = new PropertyBag()
            };

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

        public async Task ReportPassedAsync(TestCaseDescriptor test)
        {
            var testNode = new TestNode
            {
                Uid = new TestNodeUid(test.Id.Value),
                DisplayName = test.DisplayName,
                Properties = new PropertyBag(PassedTestNodeStateProperty.CachedInstance)
            };

            await _messageBus.PublishAsync(
                _producer,
                new TestNodeUpdateMessage(
                    _sessionUid,
                    testNode)).ConfigureAwait(false);
        }

        public async Task ReportFailedAsync(TestCaseDescriptor test, AssertionFailedException ex)
        {
            var testNode = new TestNode
            {
                Uid = new TestNodeUid(test.Id.Value),
                DisplayName = test.DisplayName,
                Properties = new PropertyBag(
                    new FailedTestNodeStateProperty(ex.Message))
            };

            await _messageBus.PublishAsync(
                _producer,
                new TestNodeUpdateMessage(
                    _sessionUid,
                    testNode)).ConfigureAwait(false);
        }

        public async Task ReportErrorAsync(TestCaseDescriptor test, Exception ex)
        {
            var testNode = new TestNode
            {
                Uid = new TestNodeUid(test.Id.Value),
                DisplayName = test.DisplayName,
                Properties = new PropertyBag(
                    new ErrorTestNodeStateProperty(ex))
            };

            await _messageBus.PublishAsync(
                _producer,
                new TestNodeUpdateMessage(
                    _sessionUid,
                    testNode)).ConfigureAwait(false);
        }

        public async Task ReportSkippedAsync(TestCaseDescriptor test)
        {
            var explanation = test.SkipReason ?? "Test was skipped";
            var testNode = new TestNode
            {
                Uid = new TestNodeUid(test.Id.Value),
                DisplayName = test.DisplayName,
                Properties = new PropertyBag(
                    new SkippedTestNodeStateProperty(explanation))
            };

            await _messageBus.PublishAsync(
                _producer,
                new TestNodeUpdateMessage(
                    _sessionUid,
                    testNode)).ConfigureAwait(false);
        }
    }
}
