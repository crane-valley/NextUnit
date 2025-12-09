namespace NextUnit.Internal;

/// <summary>
/// Represents a unique identifier for a test case.
/// </summary>
/// <param name="Value">The unique identifier value.</param>
public sealed record TestCaseId(string Value);

/// <summary>
/// Represents the outcome of a test execution.
/// </summary>
public enum TestOutcome
{
    /// <summary>
    /// The test has not been executed yet.
    /// </summary>
    NotRun,

    /// <summary>
    /// The test was skipped and not executed.
    /// </summary>
    Skipped,

    /// <summary>
    /// The test executed successfully and all assertions passed.
    /// </summary>
    Passed,

    /// <summary>
    /// The test failed due to an assertion failure.
    /// </summary>
    Failed,

    /// <summary>
    /// The test encountered an unexpected error during execution.
    /// </summary>
    Error
}

/// <summary>
/// Delegate for invoking a test method.
/// </summary>
/// <param name="instance">The test class instance.</param>
/// <param name="cancellationToken">A cancellation token.</param>
/// <returns>A task representing the test execution.</returns>
public delegate Task TestMethodDelegate(object instance, CancellationToken cancellationToken);

/// <summary>
/// Delegate for invoking a lifecycle method.
/// </summary>
/// <param name="instance">The test class instance.</param>
/// <param name="cancellationToken">A cancellation token.</param>
/// <returns>A task representing the lifecycle method execution.</returns>
public delegate Task LifecycleMethodDelegate(object instance, CancellationToken cancellationToken);

/// <summary>
/// Contains information about lifecycle hooks (setup and teardown methods) for a test.
/// </summary>
public sealed class LifecycleInfo
{
    /// <summary>
    /// Gets or initializes the delegates to execute before each test.
    /// </summary>
    public IReadOnlyList<LifecycleMethodDelegate> BeforeTestMethods { get; init; } = Array.Empty<LifecycleMethodDelegate>();

    /// <summary>
    /// Gets or initializes the delegates to execute after each test.
    /// </summary>
    public IReadOnlyList<LifecycleMethodDelegate> AfterTestMethods { get; init; } = Array.Empty<LifecycleMethodDelegate>();

    /// <summary>
    /// Gets or initializes the delegates to execute before all tests in a class.
    /// </summary>
    public IReadOnlyList<LifecycleMethodDelegate> BeforeClassMethods { get; init; } = Array.Empty<LifecycleMethodDelegate>();

    /// <summary>
    /// Gets or initializes the delegates to execute after all tests in a class.
    /// </summary>
    public IReadOnlyList<LifecycleMethodDelegate> AfterClassMethods { get; init; } = Array.Empty<LifecycleMethodDelegate>();

    /// <summary>
    /// Gets or initializes the delegates to execute before all tests in an assembly.
    /// </summary>
    public IReadOnlyList<LifecycleMethodDelegate> BeforeAssemblyMethods { get; init; } = Array.Empty<LifecycleMethodDelegate>();

    /// <summary>
    /// Gets or initializes the delegates to execute after all tests in an assembly.
    /// </summary>
    public IReadOnlyList<LifecycleMethodDelegate> AfterAssemblyMethods { get; init; } = Array.Empty<LifecycleMethodDelegate>();

    /// <summary>
    /// Gets or initializes the delegates to execute before all tests in a session.
    /// </summary>
    public IReadOnlyList<LifecycleMethodDelegate> BeforeSessionMethods { get; init; } = Array.Empty<LifecycleMethodDelegate>();

    /// <summary>
    /// Gets or initializes the delegates to execute after all tests in a session.
    /// </summary>
    public IReadOnlyList<LifecycleMethodDelegate> AfterSessionMethods { get; init; } = Array.Empty<LifecycleMethodDelegate>();
}

/// <summary>
/// Contains information about parallel execution configuration for a test.
/// </summary>
public sealed class ParallelInfo
{
    /// <summary>
    /// Gets or initializes a value indicating whether the test should not run in parallel with other tests.
    /// </summary>
    public bool NotInParallel { get; init; }

    /// <summary>
    /// Gets or initializes the maximum degree of parallelism for the test, or <c>null</c> if no limit is specified.
    /// </summary>
    public int? ParallelLimit { get; init; }
}

/// <summary>
/// Describes a test case with all its metadata and configuration.
/// </summary>
public sealed class TestCaseDescriptor
{
    /// <summary>
    /// Gets or initializes the unique identifier for the test case.
    /// </summary>
    public TestCaseId Id { get; init; } = new("");

    /// <summary>
    /// Gets or initializes the display name for the test case.
    /// </summary>
    public string DisplayName { get; init; } = "";

    /// <summary>
    /// Gets or initializes the type of the test class containing the test method.
    /// </summary>
    public Type TestClass { get; init; } = typeof(object);

    /// <summary>
    /// Gets or initializes the name of the test method.
    /// </summary>
    public string MethodName { get; init; } = "";

    /// <summary>
    /// Gets or initializes the delegate to invoke the test method.
    /// </summary>
    public TestMethodDelegate? TestMethod { get; init; }

    /// <summary>
    /// Gets or initializes the lifecycle hooks configuration for the test.
    /// </summary>
    public LifecycleInfo Lifecycle { get; init; } = new();

    /// <summary>
    /// Gets or initializes the parallel execution configuration for the test.
    /// </summary>
    public ParallelInfo Parallel { get; init; } = new();

    /// <summary>
    /// Gets or initializes the collection of test case identifiers that this test depends on.
    /// </summary>
    public IReadOnlyList<TestCaseId> Dependencies { get; init; } = Array.Empty<TestCaseId>();

    /// <summary>
    /// Gets or initializes a value indicating whether the test should be skipped.
    /// </summary>
    public bool IsSkipped { get; init; }

    /// <summary>
    /// Gets or initializes the reason why the test is skipped, or <c>null</c> if not skipped.
    /// </summary>
    public string? SkipReason { get; init; }

    /// <summary>
    /// Gets or initializes the arguments to pass to the test method for parameterized tests.
    /// </summary>
    /// <remarks>
    /// This will be <c>null</c> for non-parameterized tests, or an array of arguments for parameterized tests.
    /// </remarks>
    public object?[]? Arguments { get; init; }

    /// <summary>
    /// Gets or initializes the categories assigned to the test.
    /// </summary>
    public IReadOnlyList<string> Categories { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Gets or initializes the tags assigned to the test.
    /// </summary>
    public IReadOnlyList<string> Tags { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Gets or initializes a value indicating whether the test class constructor requires an ITestOutput parameter.
    /// </summary>
    public bool RequiresTestOutput { get; init; }
}

/// <summary>
/// Describes a test data source that provides test cases at runtime.
/// </summary>
/// <remarks>
/// This descriptor is generated for tests using [TestData] attribute.
/// At runtime, the data source is invoked to produce the actual test cases.
/// </remarks>
public sealed class TestDataDescriptor
{
    /// <summary>
    /// Gets or initializes the base test case ID (without data index).
    /// </summary>
    public string BaseId { get; init; } = "";

    /// <summary>
    /// Gets or initializes the display name template for the test.
    /// </summary>
    public string DisplayName { get; init; } = "";

    /// <summary>
    /// Gets or initializes the type of the test class containing the test method.
    /// </summary>
    public Type TestClass { get; init; } = typeof(object);

    /// <summary>
    /// Gets or initializes the name of the test method.
    /// </summary>
    public string MethodName { get; init; } = "";

    /// <summary>
    /// Gets or initializes the name of the data source member (method or property).
    /// </summary>
    public string DataSourceName { get; init; } = "";

    /// <summary>
    /// Gets or initializes the type that contains the data source member.
    /// If null, the test class itself is used.
    /// </summary>
    public Type? DataSourceType { get; init; }

    /// <summary>
    /// Gets or initializes the lifecycle hooks configuration for the test.
    /// </summary>
    public LifecycleInfo Lifecycle { get; init; } = new();

    /// <summary>
    /// Gets or initializes the parallel execution configuration for the test.
    /// </summary>
    public ParallelInfo Parallel { get; init; } = new();

    /// <summary>
    /// Gets or initializes the collection of test case identifiers that this test depends on.
    /// </summary>
    public IReadOnlyList<TestCaseId> Dependencies { get; init; } = Array.Empty<TestCaseId>();

    /// <summary>
    /// Gets or initializes a value indicating whether the test should be skipped.
    /// </summary>
    public bool IsSkipped { get; init; }

    /// <summary>
    /// Gets or initializes the reason why the test is skipped, or <c>null</c> if not skipped.
    /// </summary>
    public string? SkipReason { get; init; }

    /// <summary>
    /// Gets or initializes the categories assigned to the test.
    /// </summary>
    public IReadOnlyList<string> Categories { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Gets or initializes the tags assigned to the test.
    /// </summary>
    public IReadOnlyList<string> Tags { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Gets or initializes the parameter types for the test method.
    /// Used to resolve the correct method overload when invoking via reflection.
    /// </summary>
    public Type[] ParameterTypes { get; init; } = [];

    /// <summary>
    /// Gets or initializes a value indicating whether the test class constructor requires an ITestOutput parameter.
    /// </summary>
    public bool RequiresTestOutput { get; init; }
}
