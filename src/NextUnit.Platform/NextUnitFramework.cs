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
    }

    /// <summary>
    /// Gets the unique identifier for the NextUnit framework.
    /// </summary>
    public string Uid => "NextUnit.Framework";

    /// <summary>
    /// Gets the version of the NextUnit framework.
    /// </summary>
    public string Version => "1.0.0";

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

    private async Task DiscoverAsync(
        DiscoverTestExecutionRequest request,
        IMessageBus messageBus,
        CancellationToken cancellationToken)
    {
#if false // Generated registry will be available in test projects only
        // Try to use generated registry first (zero reflection!)
        IReadOnlyList<TestCaseDescriptor> testCases = NextUnit.Generated.GeneratedTestRegistry.TestCases;
#else
        // TODO M1: Remove this fallback before v1.0
        // Development fallback - uses reflection for test discovery
        var testAssemblyPath = Environment.GetCommandLineArgs()[0];
        var testAssembly = Assembly.LoadFrom(testAssemblyPath);
        var testCases = TestDescriptorProvider.GetTestCases(testAssembly);
#endif

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
#if false // Generated registry will be available in test projects only
        // Try to use generated registry first (zero reflection!)
        IReadOnlyList<TestCaseDescriptor> testCases = NextUnit.Generated.GeneratedTestRegistry.TestCases;
#else
        // TODO M1: Remove this fallback before v1.0
        // Development fallback - uses reflection for test discovery
        var testAssemblyPath = Environment.GetCommandLineArgs()[0];
        var testAssembly = Assembly.LoadFrom(testAssemblyPath);
        var testCases = TestDescriptorProvider.GetTestCases(testAssembly);
#endif

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
    }
}
