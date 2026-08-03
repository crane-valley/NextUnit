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
    IDataProducer,
    IDisposable
{
    // TODO M4: _services will be used for dependency injection and service resolution
#pragma warning disable IDE0052 // Remove unread private members
    private readonly IServiceProvider _services;
#pragma warning restore IDE0052
    private readonly TestExecutionEngine _engine = new();

    // One framework instance serves every request the platform issues, and discovery and execution
    // requests may overlap, so the one-time initialization below has to be guarded rather than relying
    // on a single caller. The memoized value is the build *task*, not the finished list: expanding a
    // data source can now await an asynchronous member, and nothing may be awaited while a lock is
    // held. _testCasesGate covers both _testCasesTask and _assemblyLifecycleInitialized because they
    // are published together by the same one-time build in GetTestCasesAsync.
    private readonly Lock _testCasesGate = new();
    private Task<IReadOnlyList<TestCaseDescriptor>>? _testCasesTask;

    // The build is shared by every request, so it cannot borrow any single request's token; it gets
    // its own. _buildWaiters counts the requests currently awaiting it, which is what lets an
    // unfinished build be cancelled once its last waiter leaves without disturbing the others.
    private CancellationTokenSource? _buildCancellation;
    private int _buildWaiters;
    private readonly TestFilterConfiguration _filterConfig;

    private readonly SessionLifecycleRunner _sessionHooks = new();
    private bool _assemblyLifecycleInitialized;
    private bool _disposed;

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
    /// Gets the version of the NextUnit framework, taken from the assembly informational version.
    /// </summary>
    public string Version => PlatformVersion.Value;

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
        var testCases = await GetTestCasesAsync(context.CancellationToken).ConfigureAwait(false);
        if (testCases.Count == 0)
        {
            return new CreateTestSessionResult { IsSuccess = true };
        }

        // Session lifecycle methods are now collected globally via GetTestCases
        var error = await _sessionHooks.RunSetupOnceAsync(context.CancellationToken).ConfigureAwait(false);

        return error is null
            ? new CreateTestSessionResult { IsSuccess = true }
            : new CreateTestSessionResult { IsSuccess = false, ErrorMessage = error };
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
        var error = await _sessionHooks.RunTeardownAsync(context.CancellationToken).ConfigureAwait(false);

        return error is null
            ? new CloseTestSessionResult { IsSuccess = true }
            : new CloseTestSessionResult { IsSuccess = false, ErrorMessage = error };
    }

    /// <summary>
    /// Returns the memoized test case list, building it on the first call.
    /// </summary>
    /// <remarks>
    /// The build task is shared, so the data source members run once per process however many
    /// requests the platform issues. It runs on its own token rather than any single request's, and
    /// each caller cancels only its own wait, so one cancelled request cannot abort another that is
    /// merely awaiting the same result. A build that ends in failure is dropped rather than
    /// memoized, so the next request rebuilds instead of replaying the failure.
    /// <para>
    /// Waiters are counted so an unfinished build does not outlive its audience. When the last
    /// waiter walks away from a build that is still running, nothing will ever observe it, so it is
    /// cancelled and dropped instead of being left cached where every later request would inherit
    /// the same stuck work and whatever resources its data source is holding.
    /// </para>
    /// </remarks>
    private async Task<IReadOnlyList<TestCaseDescriptor>> GetTestCasesAsync(CancellationToken cancellationToken)
    {
        Task<IReadOnlyList<TestCaseDescriptor>> buildTask;

        lock (_testCasesGate)
        {
            // Starting a build after disposal would hand the data sources an already-cancelled
            // token; failing here instead names the object the caller actually misused.
            ObjectDisposedException.ThrowIf(_disposed, this);

            // A build can fail after the caller that started it has already walked away, leaving a
            // faulted task nobody observed. Dropping it on acquisition means the next request
            // rebuilds, rather than being handed a failure that belonged to someone else.
            if (_testCasesTask is { IsCompleted: true, IsCompletedSuccessfully: false })
            {
                DetachBuild();
            }

            if (_testCasesTask is null)
            {
                _buildCancellation = new CancellationTokenSource();

                // BuildTestCasesAsync runs synchronously up to its first await, so the synchronous
                // data sources are still expanded under the gate exactly as they were before.
                _testCasesTask = StartBuild(_buildCancellation.Token);
            }

            buildTask = _testCasesTask;
            _buildWaiters++;
        }

        try
        {
            // Each caller cancels only its own wait. Binding the build to whichever request happened
            // to start it would let a cancelled discovery abort an unrelated run that is merely
            // awaiting the same result.
            return await buildTask.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            ReleaseBuild(buildTask);
        }
    }

    /// <summary>
    /// Records that one waiter has left, cancelling the build if it was the last one.
    /// </summary>
    private void ReleaseBuild(Task<IReadOnlyList<TestCaseDescriptor>> buildTask)
    {
        CancellationTokenSource? abandoned = null;

        lock (_testCasesGate)
        {
            _buildWaiters--;

            if (_buildWaiters == 0 &&
                !buildTask.IsCompleted &&
                ReferenceEquals(_testCasesTask, buildTask))
            {
                abandoned = _buildCancellation;
                DetachBuild();
            }
        }

        // Cancelled outside the gate: Cancel runs its registered callbacks inline, and running
        // arbitrary continuation work while holding the lock every request needs is how a
        // discovery-time stall turns into a deadlock.
        CancelAndDispose(abandoned);
    }

    /// <summary>
    /// Forgets the current build so the next request starts a fresh one. Callers hold the gate.
    /// </summary>
    private void DetachBuild()
    {
        _testCasesTask = null;
        _buildCancellation = null;
    }

    private static void CancelAndDispose(CancellationTokenSource? cancellation)
    {
        if (cancellation is null)
        {
            return;
        }

        try
        {
            cancellation.Cancel();
        }
        finally
        {
            cancellation.Dispose();
        }
    }

    /// <summary>
    /// Cancels and releases the shared build.
    /// </summary>
    /// <remarks>
    /// The build no longer belongs to any single request, so disposal is where its lifetime ends:
    /// once the framework is gone, nothing can still want the result. A host that never disposes the
    /// framework loses nothing but the early cancellation, because the build dies with the process
    /// and every caller already cancels its own wait independently.
    /// </remarks>
    public void Dispose()
    {
        CancellationTokenSource? abandoned;

        // Idempotent: Cancel() on an already-disposed source throws, and a host is free to dispose
        // an extension more than once.
        lock (_testCasesGate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            abandoned = _buildCancellation;
            DetachBuild();
        }

        CancelAndDispose(abandoned);
    }

    /// <summary>
    /// Starts the shared build and makes sure its failure is observed.
    /// </summary>
    /// <remarks>
    /// Now that a caller can cancel its own wait and leave, a failing build may end up with nobody
    /// awaiting it. Reading the exception here keeps that case out of
    /// <see cref="TaskScheduler.UnobservedTaskException"/>; the failure still propagates normally to
    /// anyone who does await the task.
    /// </remarks>
    private Task<IReadOnlyList<TestCaseDescriptor>> StartBuild(CancellationToken cancellationToken)
    {
        var buildTask = BuildTestCasesAsync(cancellationToken);

        _ = buildTask.ContinueWith(
            static failed => _ = failed.Exception,
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);

        return buildTask;
    }

    /// <summary>
    /// Builds the filtered test case list.
    /// </summary>
    private async Task<IReadOnlyList<TestCaseDescriptor>> BuildTestCasesAsync(CancellationToken cancellationToken)
    {
        var generatedRegistry = GeneratedTestRegistryStore.Current;
        if (generatedRegistry is null)
        {
            return Array.Empty<TestCaseDescriptor>();
        }

        var allTestCases = new List<TestCaseDescriptor>();

        allTestCases.AddRange(generatedRegistry.TestCases);

        // [TestData] members may be asynchronous, so this expansion is awaited with the request's
        // token instead of going through the synchronous AddFilteredExpansion helper.
        var testDataDescriptors = SelectDescriptorsToExpand(
            generatedRegistry.TestDataDescriptors,
            td => _filterConfig.ShouldExpandDynamicTest(td.Categories, td.Tags, td.DisplayName, td.IsExplicit));
        if (testDataDescriptors.Count > 0)
        {
            allTestCases.AddRange(
                await TestDataExpander.ExpandAsync(testDataDescriptors, cancellationToken).ConfigureAwait(false));
        }

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

        // Get global lifecycle methods from the registry and set on engine (one-time).
        // A registry with no asynchronous data source completes this method without ever awaiting,
        // so this can run while GetTestCasesAsync still holds the gate. That reentrancy is
        // deliberate and safe: System.Threading.Lock is recursive for the thread that owns it.
        lock (_testCasesGate)
        {
            if (!_assemblyLifecycleInitialized)
            {
                _engine.SetGlobalAssemblyLifecycle(
                    generatedRegistry.GlobalBeforeAssemblyMethods,
                    generatedRegistry.GlobalAfterAssemblyMethods);
                _sessionHooks.AddMethods(
                    generatedRegistry.GlobalBeforeSessionMethods,
                    generatedRegistry.GlobalAfterSessionMethods);

                _assemblyLifecycleInitialized = true;
            }
        }

        return filteredTestCases;
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
        var filteredDescriptors = SelectDescriptorsToExpand(descriptors, shouldExpand);
        if (filteredDescriptors.Count == 0)
        {
            return;
        }

        destination.AddRange(expand(filteredDescriptors));
    }

    private static List<TDescriptor> SelectDescriptorsToExpand<TDescriptor>(
        IReadOnlyList<TDescriptor> descriptors,
        Func<TDescriptor, bool> shouldExpand) =>
        descriptors.Count == 0
            ? new List<TDescriptor>()
            : descriptors.Where(shouldExpand).ToList();

    private async Task DiscoverAsync(
        DiscoverTestExecutionRequest request,
        IMessageBus messageBus,
        CancellationToken cancellationToken)
    {
        var testCases = await GetTestCasesAsync(cancellationToken).ConfigureAwait(false);

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
        var testCases = await GetTestCasesAsync(cancellationToken).ConfigureAwait(false);
        var sink = new MessageBusSink(messageBus, request.Session.SessionUid, this);

        // A session setup hook that requested a skip disqualifies every test in the session, so the
        // reason is reported per test instead of the tests running.
        if (await _sessionHooks.TryReportSessionSkipAsync(testCases, sink, cancellationToken).ConfigureAwait(false))
        {
            return;
        }

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
