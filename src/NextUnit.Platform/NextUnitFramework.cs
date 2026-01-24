using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.TestFramework;
using Microsoft.Testing.Platform.Messages;
using Microsoft.Testing.Platform.Requests;
using Microsoft.Testing.Platform.Services;
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
        _filterConfig = LoadFilterConfiguration(services);
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
        // Collect session-level lifecycle methods from all test cases
        var testCases = GetTestCases();
        if (testCases.Count > 0 && !_sessionSetupExecuted)
        {
            // Collect unique session methods from all test cases
            // Use a HashSet to avoid duplicates if same methods appear in multiple test classes
            // Use method identity (declaring type + method name) for deduplication
            var beforeMethods = new List<LifecycleMethodDelegate>();
            var afterMethods = new List<LifecycleMethodDelegate>();
            var seenBefore = new HashSet<string>();
            var seenAfter = new HashSet<string>();

            foreach (var testCase in testCases)
            {
                foreach (var method in testCase.Lifecycle.BeforeSessionMethods)
                {
                    var methodInfo = method.Method;
                    // Use fully qualified name as deduplication key
                    var key = methodInfo.DeclaringType?.FullName + "." + methodInfo.Name;
                    if (seenBefore.Add(key))
                    {
                        beforeMethods.Add(method);
                    }
                }
                foreach (var method in testCase.Lifecycle.AfterSessionMethods)
                {
                    var methodInfo = method.Method;
                    var key = methodInfo.DeclaringType?.FullName + "." + methodInfo.Name;
                    if (seenAfter.Add(key))
                    {
                        afterMethods.Add(method);
                    }
                }
            }

            _sessionBeforeMethods.AddRange(beforeMethods);
            _sessionAfterMethods.AddRange(afterMethods);

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

        // Try to find generated test registry using reflection
        // This is a one-time operation during test discovery, not during test execution
        var testAssemblyPath = Environment.GetCommandLineArgs()[0];
        var loadResult = AssemblyLoader.TryLoadAssembly(testAssemblyPath);
        if (!loadResult.Success)
        {
            _testCases = Array.Empty<TestCaseDescriptor>();
            return _testCases;
        }

        // Look for NextUnit.Generated.GeneratedTestRegistry
        var generatedRegistryType = AssemblyLoader.GetTestRegistryType(loadResult.Assembly!);
        if (generatedRegistryType is null)
        {
            _testCases = Array.Empty<TestCaseDescriptor>();
            return _testCases;
        }

        var allTestCases = new List<TestCaseDescriptor>();

        // Get static test cases from TestCases property
        var staticTestCases = AssemblyLoader.GetStaticPropertyValue<IReadOnlyList<TestCaseDescriptor>>(generatedRegistryType, "TestCases");
        if (staticTestCases is not null)
        {
            allTestCases.AddRange(staticTestCases);
        }

        // Get dynamic test cases from TestDataDescriptors property
        var testDataDescriptors = AssemblyLoader.GetStaticPropertyValue<IReadOnlyList<TestDataDescriptor>>(generatedRegistryType, "TestDataDescriptors");
        if (testDataDescriptors is not null)
        {
            // Filter TestDataDescriptors BEFORE expansion to avoid executing data providers for excluded tests
            var filteredDescriptors = testDataDescriptors
                .Where(td => _filterConfig.ShouldIncludeTest(td.Categories, td.Tags, td.DisplayName, td.IsExplicit))
                .ToList();

            // Expand only the filtered TestDataDescriptors into TestCaseDescriptors at runtime
            var expandedTests = TestDataExpander.Expand(filteredDescriptors);
            allTestCases.AddRange(expandedTests);
        }

        // Get dynamic test cases from ClassDataSourceDescriptors property
        var classDataSourceDescriptors = AssemblyLoader.GetStaticPropertyValue<IReadOnlyList<ClassDataSourceDescriptor>>(generatedRegistryType, "ClassDataSourceDescriptors");
        if (classDataSourceDescriptors is not null)
        {
            // Filter ClassDataSourceDescriptors BEFORE expansion to avoid instantiating data sources for excluded tests
            var filteredDescriptors = classDataSourceDescriptors
                .Where(cd => _filterConfig.ShouldIncludeTest(cd.Categories, cd.Tags, cd.DisplayName, cd.IsExplicit))
                .ToList();

            // Expand only the filtered ClassDataSourceDescriptors into TestCaseDescriptors at runtime
            var expandedTests = ClassDataSourceExpander.Expand(filteredDescriptors);
            allTestCases.AddRange(expandedTests);
        }

        // Get dynamic test cases from CombinedDataSourceDescriptors property
        var combinedDataSourceDescriptors = AssemblyLoader.GetStaticPropertyValue<IReadOnlyList<CombinedDataSourceDescriptor>>(generatedRegistryType, "CombinedDataSourceDescriptors");
        if (combinedDataSourceDescriptors is not null)
        {
            // Filter CombinedDataSourceDescriptors BEFORE expansion to avoid resolving data sources for excluded tests
            var filteredDescriptors = combinedDataSourceDescriptors
                .Where(cd => _filterConfig.ShouldIncludeTest(cd.Categories, cd.Tags, cd.DisplayName, cd.IsExplicit))
                .ToList();

            // Expand only the filtered CombinedDataSourceDescriptors into TestCaseDescriptors at runtime
            var expandedTests = CombinedDataSourceExpander.Expand(filteredDescriptors);
            allTestCases.AddRange(expandedTests);
        }

        // Apply category and tag filtering to static test cases
        var filteredTestCases = allTestCases.Where(tc => _filterConfig.ShouldIncludeTest(tc.Categories, tc.Tags, tc.DisplayName, tc.IsExplicit)).ToList();

        _testCases = filteredTestCases;
        return _testCases;
    }

    private static TestFilterConfiguration LoadFilterConfiguration(IServiceProvider services)
    {
        var config = new TestFilterConfiguration();

        // Try to get command-line options service
        var commandLineOptions = services.GetService<ICommandLineOptions>();

        // Priority: CLI arguments > Environment variables

        // Load categories from CLI or environment
        var includeCategories = GetFilterValues(
            commandLineOptions,
            NextUnitCommandLineOptionsProvider.CategoryOption,
            "NEXTUNIT_INCLUDE_CATEGORIES");
        if (includeCategories.Count > 0)
        {
            config.IncludeCategories = includeCategories;
        }

        var excludeCategories = GetFilterValues(
            commandLineOptions,
            NextUnitCommandLineOptionsProvider.ExcludeCategoryOption,
            "NEXTUNIT_EXCLUDE_CATEGORIES");
        if (excludeCategories.Count > 0)
        {
            config.ExcludeCategories = excludeCategories;
        }

        // Load tags from CLI or environment
        var includeTags = GetFilterValues(
            commandLineOptions,
            NextUnitCommandLineOptionsProvider.TagOption,
            "NEXTUNIT_INCLUDE_TAGS");
        if (includeTags.Count > 0)
        {
            config.IncludeTags = includeTags;
        }

        var excludeTags = GetFilterValues(
            commandLineOptions,
            NextUnitCommandLineOptionsProvider.ExcludeTagOption,
            "NEXTUNIT_EXCLUDE_TAGS");
        if (excludeTags.Count > 0)
        {
            config.ExcludeTags = excludeTags;
        }

        // Load test name patterns (wildcard support)
        var testNamePatterns = GetFilterValues(
            commandLineOptions,
            NextUnitCommandLineOptionsProvider.TestNameOption,
            "NEXTUNIT_TEST_NAME");
        if (testNamePatterns.Count > 0)
        {
            config.TestNamePatterns = testNamePatterns;
        }

        // Load test name regex patterns
        var testNameRegexPatterns = GetFilterValues(
            commandLineOptions,
            NextUnitCommandLineOptionsProvider.TestNameRegexOption,
            "NEXTUNIT_TEST_NAME_REGEX");
        if (testNameRegexPatterns.Count > 0)
        {
            var regexList = new List<System.Text.RegularExpressions.Regex>();
            foreach (var pattern in testNameRegexPatterns)
            {
                try
                {
                    regexList.Add(new System.Text.RegularExpressions.Regex(
                        pattern,
                        System.Text.RegularExpressions.RegexOptions.IgnoreCase |
                        System.Text.RegularExpressions.RegexOptions.Compiled));
                }
                catch (ArgumentException)
                {
                    // Invalid regex pattern - silently skip it
                    // Users will notice when their filter doesn't match expected tests
                }
            }
            config.TestNameRegexPatterns = regexList;
        }

        // Load --explicit flag
        if (commandLineOptions is not null && commandLineOptions.IsOptionSet(NextUnitCommandLineOptionsProvider.ExplicitOption))
        {
            config.IncludeExplicitTests = true;
        }
        else
        {
            // Fall back to environment variable
            var explicitEnv = Environment.GetEnvironmentVariable("NEXTUNIT_INCLUDE_EXPLICIT");
            if (string.Equals(explicitEnv, "true", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(explicitEnv, "1", StringComparison.OrdinalIgnoreCase))
            {
                config.IncludeExplicitTests = true;
            }
        }

        return config;
    }

    private static IReadOnlyList<string> GetFilterValues(
        ICommandLineOptions? commandLineOptions,
        string optionName,
        string environmentVariableName)
    {
        // Try CLI arguments first (higher priority)
        if (commandLineOptions is not null
            && commandLineOptions.IsOptionSet(optionName)
            && commandLineOptions.TryGetOptionArgumentList(optionName, out var arguments)
            && arguments is not null)
        {
            return arguments.ToList();
        }

        // Fall back to environment variable
        var envValue = Environment.GetEnvironmentVariable(environmentVariableName);
        if (!string.IsNullOrWhiteSpace(envValue))
        {
            return envValue.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }

        return Array.Empty<string>();
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

        public async Task ReportPassedAsync(TestCaseDescriptor test, string? output = null, IReadOnlyList<Artifact>? artifacts = null)
        {
            var properties = new List<IProperty> { PassedTestNodeStateProperty.CachedInstance };

            if (!string.IsNullOrEmpty(output))
            {
                properties.Add(new TestMetadataProperty("TestOutput", output));
            }

            AddArtifactProperties(properties, artifacts);

            var testNode = new TestNode
            {
                Uid = new TestNodeUid(test.Id.Value),
                DisplayName = test.DisplayName,
                Properties = new PropertyBag(properties.ToArray())
            };

            await _messageBus.PublishAsync(
                _producer,
                new TestNodeUpdateMessage(
                    _sessionUid,
                    testNode)).ConfigureAwait(false);
        }

        public async Task ReportFailedAsync(TestCaseDescriptor test, AssertionFailedException ex, string? output = null, IReadOnlyList<Artifact>? artifacts = null)
        {
            var properties = new List<IProperty> { new FailedTestNodeStateProperty(ex.Message) };

            if (!string.IsNullOrEmpty(output))
            {
                properties.Add(new TestMetadataProperty("TestOutput", output));
            }

            AddArtifactProperties(properties, artifacts);

            var testNode = new TestNode
            {
                Uid = new TestNodeUid(test.Id.Value),
                DisplayName = test.DisplayName,
                Properties = new PropertyBag(properties.ToArray())
            };

            await _messageBus.PublishAsync(
                _producer,
                new TestNodeUpdateMessage(
                    _sessionUid,
                    testNode)).ConfigureAwait(false);
        }

        public async Task ReportErrorAsync(TestCaseDescriptor test, Exception ex, string? output = null, IReadOnlyList<Artifact>? artifacts = null)
        {
            var properties = new List<IProperty> { new ErrorTestNodeStateProperty(ex) };

            if (!string.IsNullOrEmpty(output))
            {
                properties.Add(new TestMetadataProperty("TestOutput", output));
            }

            AddArtifactProperties(properties, artifacts);

            var testNode = new TestNode
            {
                Uid = new TestNodeUid(test.Id.Value),
                DisplayName = test.DisplayName,
                Properties = new PropertyBag(properties.ToArray())
            };

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

        public async Task ReportSkippedAsync(TestCaseDescriptor test, IReadOnlyList<Artifact>? artifacts = null)
        {
            var explanation = test.SkipReason ?? "Test was skipped";
            var properties = new List<IProperty> { new SkippedTestNodeStateProperty(explanation) };

            AddArtifactProperties(properties, artifacts);

            var testNode = new TestNode
            {
                Uid = new TestNodeUid(test.Id.Value),
                DisplayName = test.DisplayName,
                Properties = new PropertyBag(properties.ToArray())
            };

            await _messageBus.PublishAsync(
                _producer,
                new TestNodeUpdateMessage(
                    _sessionUid,
                    testNode)).ConfigureAwait(false);
        }
    }
}
