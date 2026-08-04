using System.Diagnostics.CodeAnalysis;
using NextUnit.Core;

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
/// Invokes a generated parameterized test method without runtime reflection.
/// </summary>
public delegate Task TestMethodWithArgumentsDelegate(
    object instance,
    object?[] arguments,
    CancellationToken cancellationToken);

/// <summary>
/// Creates a test class instance using a source-generated constructor call.
/// </summary>
public delegate object TestClassFactoryDelegate(ITestOutput output, ITestContext context);

/// <summary>
/// Resolves a source-generated member or class data source.
/// </summary>
public delegate object? DataSourceProviderDelegate();

/// <summary>
/// Resolves a source-generated asynchronous member data source into an untyped row sequence.
/// </summary>
/// <param name="cancellationToken">The token that cancels row enumeration during discovery.</param>
/// <remarks>
/// The generator binds the member's element type statically and hands the rows over as
/// <see cref="object"/>, so the runtime never needs reflection or a runtime generic instantiation
/// to read an <c>IAsyncEnumerable&lt;T&gt;</c> whose <c>T</c> it does not know.
/// </remarks>
public delegate IAsyncEnumerable<object?> AsyncDataSourceProviderDelegate(CancellationToken cancellationToken);

/// <summary>
/// Creates the <see cref="IRetryPolicy"/> configured by <see cref="RetryAttribute{TPolicy}"/>.
/// </summary>
/// <remarks>
/// A factory rather than a <see cref="Type"/>: the generator emits a direct constructor call, so no
/// part of the retry path reflects over a user type, and the trimmer keeps the policy because
/// ordinary generated code references it.
/// </remarks>
public delegate IRetryPolicy RetryPolicyFactoryDelegate();

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
    /// Gets or initializes the constraint keys for parallel execution.
    /// Tests sharing any constraint key will not run in parallel.
    /// </summary>
    /// <remarks>
    /// When <see cref="NotInParallel"/> is <c>true</c> and this array is empty,
    /// the test will not run in parallel with any other <see cref="NotInParallel"/> test.
    /// When constraint keys are specified, only tests sharing at least one key are serialized.
    /// </remarks>
    public IReadOnlyList<string> ConstraintKeys { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Gets or initializes the parallel group name for exclusive group execution.
    /// </summary>
    /// <remarks>
    /// Tests in the same group run in parallel with each other but the entire group
    /// does not run in parallel with other groups or ungrouped tests.
    /// </remarks>
    public string? ParallelGroup { get; init; }

    /// <summary>
    /// Gets or initializes the maximum degree of parallelism for the test, or <c>null</c> if no limit is specified.
    /// </summary>
    public int? ParallelLimit { get; init; }
}

/// <summary>
/// Contains information about a test dependency.
/// </summary>
public sealed class DependencyInfo
{
    /// <summary>
    /// Gets or initializes the identifier of the test this dependency refers to.
    /// </summary>
    public TestCaseId DependsOnId { get; init; } = new("");

    /// <summary>
    /// Gets or initializes a value indicating whether the dependent test should proceed
    /// even if this dependency fails or is skipped.
    /// </summary>
    /// <remarks>
    /// When <c>true</c>, the dependent test will run regardless of the outcome of this dependency.
    /// When <c>false</c> (default), the dependent test will be skipped if this dependency fails.
    /// </remarks>
    public bool ProceedOnFailure { get; init; }
}

/// <summary>
/// Contains information about retry configuration for a test.
/// </summary>
public sealed class RetryInfo
{
    /// <summary>
    /// Gets or initializes the maximum number of retry attempts, or <c>null</c> if no retry is configured.
    /// </summary>
    public int? Count { get; init; }

    /// <summary>
    /// Gets or initializes the delay in milliseconds between retry attempts.
    /// </summary>
    public int DelayMs { get; init; }

    /// <summary>
    /// Gets or initializes the factory for the <see cref="IRetryPolicy"/> that decides which
    /// failures are retried, or <c>null</c> to retry every retriable failure.
    /// </summary>
    /// <remarks>
    /// Null is the compatibility default: a test declared with the non-generic
    /// <see cref="RetryAttribute"/> never allocates a policy and behaves exactly as it did before
    /// policies existed.
    /// </remarks>
    public RetryPolicyFactoryDelegate? PolicyFactory { get; init; }

    /// <summary>
    /// Gets or initializes a value indicating whether the test is marked as flaky.
    /// </summary>
    public bool IsFlaky { get; init; }

    /// <summary>
    /// Gets or initializes the reason why the test is considered flaky.
    /// </summary>
    public string? FlakyReason { get; init; }
}

/// <summary>
/// Contains the cultures a test runs under.
/// </summary>
/// <remarks>
/// The names are carried as strings rather than <see cref="System.Globalization.CultureInfo"/>
/// instances so the generator can emit them as literals, and so a culture missing on the executing
/// machine is reported as that test's error instead of failing the whole generated registry's static
/// initializer.
/// </remarks>
public sealed class TestCultureInfo
{
    /// <summary>
    /// The shared instance used by every test that declares no culture.
    /// </summary>
    /// <remarks>
    /// Safe to share because the type is immutable, and worth sharing because otherwise every
    /// descriptor allocates an object describing the absence of a setting.
    /// </remarks>
    internal static TestCultureInfo None { get; } = new();

    /// <summary>
    /// Gets or initializes the name for <see cref="System.Globalization.CultureInfo.CurrentCulture"/>,
    /// or <c>null</c> to leave the ambient culture in place. The empty string is the invariant culture.
    /// </summary>
    public string? CultureName { get; init; }

    /// <summary>
    /// Gets or initializes the name for <see cref="System.Globalization.CultureInfo.CurrentUICulture"/>,
    /// or <c>null</c> to leave the ambient UI culture in place. The empty string is the invariant culture.
    /// </summary>
    public string? UICultureName { get; init; }
}

/// <summary>
/// Describes a test case with all its metadata and configuration.
/// </summary>
public sealed class TestCaseDescriptor
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TestCaseDescriptor"/> class.
    /// </summary>
    public TestCaseDescriptor()
    {
    }

    // Single source of truth for copying a descriptor: a "with"-style helper that forgets a property
    // silently drops metadata, so every copy path goes through this constructor.
    private TestCaseDescriptor(TestCaseDescriptor other)
    {
        Id = other.Id;
        DisplayName = other.DisplayName;
        TestClass = other.TestClass;
        MethodName = other.MethodName;
        TestMethod = other.TestMethod;
        TestMethodWithArguments = other.TestMethodWithArguments;
        TestClassFactory = other.TestClassFactory;
        Lifecycle = other.Lifecycle;
        Parallel = other.Parallel;
        Dependencies = other.Dependencies;
        DependencyInfos = other.DependencyInfos;
        IsSkipped = other.IsSkipped;
        SkipReason = other.SkipReason;
        IsExplicit = other.IsExplicit;
        ExplicitReason = other.ExplicitReason;
        Arguments = other.Arguments;
        Categories = other.Categories;
        Tags = other.Tags;
        RequiresTestOutput = other.RequiresTestOutput;
        RequiresTestContext = other.RequiresTestContext;
        TimeoutMs = other.TimeoutMs;
        RepeatIndex = other.RepeatIndex;
        Retry = other.Retry;
        Culture = other.Culture;
        CustomDisplayNameTemplate = other.CustomDisplayNameTemplate;
        DisplayNameFormatterType = other.DisplayNameFormatterType;
        Priority = other.Priority;
        DeferredDataSource = other.DeferredDataSource;
    }

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
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicMethods | DynamicallyAccessedMemberTypes.NonPublicMethods)]
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
    /// Gets or initializes the generated delegate for a runtime-parameterized test method.
    /// </summary>
    public TestMethodWithArgumentsDelegate? TestMethodWithArguments { get; init; }

    /// <summary>
    /// Gets or initializes the generated test class factory.
    /// </summary>
    public TestClassFactoryDelegate? TestClassFactory { get; init; }

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
    /// <remarks>
    /// This is a simplified view of dependencies. Use <see cref="DependencyInfos"/> for
    /// full dependency information including <see cref="DependencyInfo.ProceedOnFailure"/>.
    /// </remarks>
    public IReadOnlyList<TestCaseId> Dependencies { get; init; } = Array.Empty<TestCaseId>();

    /// <summary>
    /// Gets or initializes the detailed dependency information including proceed-on-failure settings.
    /// </summary>
    public IReadOnlyList<DependencyInfo> DependencyInfos { get; init; } = Array.Empty<DependencyInfo>();

    /// <summary>
    /// Gets or initializes a value indicating whether the test should be skipped.
    /// </summary>
    public bool IsSkipped { get; init; }

    /// <summary>
    /// Gets or initializes the reason why the test is skipped, or <c>null</c> if not skipped.
    /// </summary>
    public string? SkipReason { get; init; }

    /// <summary>
    /// Gets or initializes a value indicating whether the test is marked as explicit.
    /// Explicit tests only run when specifically selected or when the --explicit flag is used.
    /// </summary>
    public bool IsExplicit { get; init; }

    /// <summary>
    /// Gets or initializes the reason why the test is marked as explicit, or <c>null</c> if not provided.
    /// </summary>
    public string? ExplicitReason { get; init; }

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

    /// <summary>
    /// Gets or initializes a value indicating whether the test class constructor requires an ITestContext parameter.
    /// </summary>
    public bool RequiresTestContext { get; init; }

    /// <summary>
    /// Gets or initializes the timeout for the test in milliseconds, or <c>null</c> if no timeout is specified.
    /// </summary>
    public int? TimeoutMs { get; init; }

    /// <summary>
    /// Gets or initializes the zero-based repeat index for tests marked with <see cref="NextUnit.RepeatAttribute"/>,
    /// or <c>null</c> for non-repeated tests.
    /// </summary>
    public int? RepeatIndex { get; init; }

    /// <summary>
    /// Gets or initializes the retry configuration for the test.
    /// </summary>
    public RetryInfo Retry { get; init; } = new();

    /// <summary>
    /// Gets or initializes the cultures the test runs under.
    /// </summary>
    public TestCultureInfo Culture { get; init; } = TestCultureInfo.None;

    /// <summary>
    /// Gets or initializes the custom display name template with optional placeholders ({0}, {1}, etc.).
    /// </summary>
    public string? CustomDisplayNameTemplate { get; init; }

    /// <summary>
    /// Gets or initializes the type that implements <see cref="IDisplayNameFormatter"/> for custom formatting.
    /// </summary>
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)]
    public Type? DisplayNameFormatterType { get; init; }

    /// <summary>
    /// Gets or initializes the execution priority for the test.
    /// Higher values indicate higher priority (run first). Default is 0.
    /// </summary>
    public int Priority { get; init; }

    /// <summary>
    /// Gets or initializes the data source this test case stands in for until execution expands it,
    /// or <c>null</c> for an ordinary test case.
    /// </summary>
    /// <remarks>
    /// Set only on the single placeholder produced for a <c>[TestData]</c> source declared with
    /// <see cref="NextUnit.TestDataAttribute.DeferredEnumeration"/>. A placeholder carries no
    /// invoker and never executes: <see cref="TestExecutionEngine"/> replaces it with the real rows
    /// before the dependency graph is built, so everything downstream sees ordinary test cases.
    /// </remarks>
    public TestDataDescriptor? DeferredDataSource { get; init; }

    /// <summary>
    /// Creates a copy of the current <see cref="TestCaseDescriptor"/> with updated skip-related properties.
    /// </summary>
    /// <param name="reason">The reason for skipping the test.</param>
    /// <returns>
    /// A new instance that preserves all properties of the current descriptor, but with
    /// <see cref="IsSkipped"/> set to <c>true</c> and <see cref="SkipReason"/> set to
    /// the specified <paramref name="reason"/>.
    /// </returns>
    public TestCaseDescriptor WithSkipReason(string reason) => new(this)
    {
        IsSkipped = true,
        SkipReason = reason
    };
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
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicMethods | DynamicallyAccessedMemberTypes.NonPublicMethods | DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.NonPublicProperties | DynamicallyAccessedMemberTypes.PublicFields | DynamicallyAccessedMemberTypes.NonPublicFields)]
    public Type TestClass { get; init; } = typeof(object);

    /// <summary>
    /// Gets or initializes the name of the test method.
    /// </summary>
    public string MethodName { get; init; } = "";

    /// <summary>
    /// Gets or initializes the generated test class factory.
    /// </summary>
    public TestClassFactoryDelegate? TestClassFactory { get; init; }

    /// <summary>
    /// Gets or initializes the generated runtime-argument test invoker.
    /// </summary>
    public TestMethodWithArgumentsDelegate? TestMethodWithArguments { get; init; }

    /// <summary>
    /// Gets or initializes the name of the data source member (method or property).
    /// </summary>
    public string DataSourceName { get; init; } = "";

    /// <summary>
    /// Gets or initializes the type that contains the data source member.
    /// If null, the test class itself is used.
    /// </summary>
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods | DynamicallyAccessedMemberTypes.NonPublicMethods | DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.NonPublicProperties | DynamicallyAccessedMemberTypes.PublicFields | DynamicallyAccessedMemberTypes.NonPublicFields)]
    public Type? DataSourceType { get; init; }

    /// <summary>
    /// Gets or initializes the generated data source accessor.
    /// </summary>
    public DataSourceProviderDelegate? DataSourceProvider { get; init; }

    /// <summary>
    /// Gets or initializes the generated accessor for an asynchronous data source member.
    /// </summary>
    /// <remarks>
    /// Set instead of <see cref="DataSourceProvider"/>, never alongside it: a member is either
    /// synchronous or asynchronous, and the expander prefers this one when both are present.
    /// </remarks>
    public AsyncDataSourceProviderDelegate? AsyncDataSourceProvider { get; init; }

    /// <summary>
    /// Gets or initializes a value indicating whether the rows are enumerated during execution
    /// rather than during discovery.
    /// </summary>
    /// <remarks>
    /// Mirrors <see cref="NextUnit.TestDataAttribute.DeferredEnumeration"/>. Discovery reports one
    /// placeholder test case for the whole source instead of one per row, so the rows are not
    /// individually selectable or filterable; see the attribute for the full tradeoff.
    /// </remarks>
    public bool DeferredEnumeration { get; init; }

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
    /// <remarks>
    /// This is a simplified view of dependencies. Use <see cref="DependencyInfos"/> for
    /// full dependency information including <see cref="DependencyInfo.ProceedOnFailure"/>.
    /// </remarks>
    public IReadOnlyList<TestCaseId> Dependencies { get; init; } = Array.Empty<TestCaseId>();

    /// <summary>
    /// Gets or initializes the detailed dependency information including proceed-on-failure settings.
    /// </summary>
    public IReadOnlyList<DependencyInfo> DependencyInfos { get; init; } = Array.Empty<DependencyInfo>();

    /// <summary>
    /// Gets or initializes a value indicating whether the test should be skipped.
    /// </summary>
    public bool IsSkipped { get; init; }

    /// <summary>
    /// Gets or initializes the reason why the test is skipped, or <c>null</c> if not skipped.
    /// </summary>
    public string? SkipReason { get; init; }

    /// <summary>
    /// Gets or initializes a value indicating whether the test is marked as explicit.
    /// Explicit tests only run when specifically selected or when the --explicit flag is used.
    /// </summary>
    public bool IsExplicit { get; init; }

    /// <summary>
    /// Gets or initializes the reason why the test is marked as explicit, or <c>null</c> if not provided.
    /// </summary>
    public string? ExplicitReason { get; init; }

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

    /// <summary>
    /// Gets or initializes a value indicating whether the test class constructor requires an ITestContext parameter.
    /// </summary>
    public bool RequiresTestContext { get; init; }

    /// <summary>
    /// Gets or initializes the timeout for the test in milliseconds, or <c>null</c> if no timeout is specified.
    /// </summary>
    public int? TimeoutMs { get; init; }

    /// <summary>
    /// Gets or initializes the retry configuration for the test.
    /// </summary>
    public RetryInfo Retry { get; init; } = new();

    /// <summary>
    /// Gets or initializes the cultures the test runs under.
    /// </summary>
    public TestCultureInfo Culture { get; init; } = TestCultureInfo.None;

    /// <summary>
    /// Gets or initializes the custom display name template with optional placeholders ({0}, {1}, etc.).
    /// </summary>
    public string? CustomDisplayNameTemplate { get; init; }

    /// <summary>
    /// Gets or initializes the type that implements <see cref="IDisplayNameFormatter"/> for custom formatting.
    /// </summary>
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)]
    public Type? DisplayNameFormatterType { get; init; }

    /// <summary>
    /// Gets or initializes the execution priority for the test.
    /// Higher values indicate higher priority (run first). Default is 0.
    /// </summary>
    public int Priority { get; init; }
}

/// <summary>
/// Specifies the kind of data source for a parameter.
/// </summary>
public enum ParameterDataSourceKind
{
    /// <summary>
    /// Inline values from [Values] attribute.
    /// </summary>
    Inline,

    /// <summary>
    /// Values from a static member via [ValuesFromMember] attribute.
    /// </summary>
    Member,

    /// <summary>
    /// Values from a class data source via [ValuesFrom&lt;T&gt;] attribute.
    /// </summary>
    Class
}

/// <summary>
/// Describes a data source for a single parameter in a combined data source test.
/// </summary>
public sealed class ParameterDataSource
{
    /// <summary>
    /// Gets or initializes the zero-based index of the parameter.
    /// </summary>
    public int ParameterIndex { get; init; }

    /// <summary>
    /// Gets or initializes the name of the parameter.
    /// </summary>
    public string ParameterName { get; init; } = "";

    /// <summary>
    /// Gets or initializes the kind of data source.
    /// </summary>
    public ParameterDataSourceKind Kind { get; init; }

    /// <summary>
    /// Gets or initializes the inline values for [Values] attribute.
    /// Null for other kinds.
    /// </summary>
    public object?[]? InlineValues { get; init; }

    /// <summary>
    /// Gets or initializes the member name for [ValuesFromMember] attribute.
    /// Null for other kinds.
    /// </summary>
    public string? MemberName { get; init; }

    /// <summary>
    /// Gets or initializes the type containing the member.
    /// Null if the test class should be used.
    /// </summary>
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods | DynamicallyAccessedMemberTypes.NonPublicMethods | DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.NonPublicProperties | DynamicallyAccessedMemberTypes.PublicFields | DynamicallyAccessedMemberTypes.NonPublicFields)]
    public Type? MemberType { get; init; }

    /// <summary>
    /// Gets or initializes the generated member accessor.
    /// </summary>
    public DataSourceProviderDelegate? MemberProvider { get; init; }

    /// <summary>
    /// Gets or initializes the type of the class data source.
    /// Null for non-class kinds.
    /// </summary>
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)]
    public Type? ClassDataSourceType { get; init; }

    /// <summary>
    /// Gets or initializes the generated class data source factory.
    /// </summary>
    public DataSourceProviderDelegate? ClassDataSourceFactory { get; init; }

    /// <summary>
    /// Gets or initializes the sharing scope for class data sources.
    /// </summary>
    public SharedType SharedType { get; init; } = SharedType.None;

    /// <summary>
    /// Gets or initializes the key for keyed sharing.
    /// Only applicable when SharedType is Keyed.
    /// </summary>
    public string? SharedKey { get; init; }
}

/// <summary>
/// Describes a test that uses combined data sources for its parameters.
/// </summary>
/// <remarks>
/// This descriptor is generated for tests using parameter-level data source attributes
/// such as [Values], [ValuesFromMember], and [ValuesFrom&lt;T&gt;].
/// At runtime, the Cartesian product of all parameter values is computed.
/// </remarks>
public sealed class CombinedDataSourceDescriptor
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
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicMethods | DynamicallyAccessedMemberTypes.NonPublicMethods | DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.NonPublicProperties | DynamicallyAccessedMemberTypes.PublicFields | DynamicallyAccessedMemberTypes.NonPublicFields)]
    public Type TestClass { get; init; } = typeof(object);

    /// <summary>
    /// Gets or initializes the name of the test method.
    /// </summary>
    public string MethodName { get; init; } = "";

    /// <summary>
    /// Gets or initializes the generated test class factory.
    /// </summary>
    public TestClassFactoryDelegate? TestClassFactory { get; init; }

    /// <summary>
    /// Gets or initializes the generated runtime-argument test invoker.
    /// </summary>
    public TestMethodWithArgumentsDelegate? TestMethodWithArguments { get; init; }

    /// <summary>
    /// Gets or initializes the data sources for each parameter.
    /// </summary>
    public ParameterDataSource[] ParameterSources { get; init; } = [];

    /// <summary>
    /// Gets or initializes the parameter types for the test method.
    /// Used to resolve the correct method overload when invoking via reflection.
    /// </summary>
    public Type[] ParameterTypes { get; init; } = [];

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
    /// Gets or initializes the detailed dependency information including proceed-on-failure settings.
    /// </summary>
    public IReadOnlyList<DependencyInfo> DependencyInfos { get; init; } = Array.Empty<DependencyInfo>();

    /// <summary>
    /// Gets or initializes a value indicating whether the test should be skipped.
    /// </summary>
    public bool IsSkipped { get; init; }

    /// <summary>
    /// Gets or initializes the reason why the test is skipped, or <c>null</c> if not skipped.
    /// </summary>
    public string? SkipReason { get; init; }

    /// <summary>
    /// Gets or initializes a value indicating whether the test is marked as explicit.
    /// Explicit tests only run when specifically selected or when the --explicit flag is used.
    /// </summary>
    public bool IsExplicit { get; init; }

    /// <summary>
    /// Gets or initializes the reason why the test is marked as explicit, or <c>null</c> if not provided.
    /// </summary>
    public string? ExplicitReason { get; init; }

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

    /// <summary>
    /// Gets or initializes a value indicating whether the test class constructor requires an ITestContext parameter.
    /// </summary>
    public bool RequiresTestContext { get; init; }

    /// <summary>
    /// Gets or initializes the timeout for the test in milliseconds, or <c>null</c> if no timeout is specified.
    /// </summary>
    public int? TimeoutMs { get; init; }

    /// <summary>
    /// Gets or initializes the retry configuration for the test.
    /// </summary>
    public RetryInfo Retry { get; init; } = new();

    /// <summary>
    /// Gets or initializes the cultures the test runs under.
    /// </summary>
    public TestCultureInfo Culture { get; init; } = TestCultureInfo.None;

    /// <summary>
    /// Gets or initializes the custom display name template with optional placeholders ({0}, {1}, etc.).
    /// </summary>
    public string? CustomDisplayNameTemplate { get; init; }

    /// <summary>
    /// Gets or initializes the type that implements <see cref="IDisplayNameFormatter"/> for custom formatting.
    /// </summary>
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)]
    public Type? DisplayNameFormatterType { get; init; }

    /// <summary>
    /// Gets or initializes the execution priority for the test.
    /// Higher values indicate higher priority (run first). Default is 0.
    /// </summary>
    public int Priority { get; init; }
}

/// <summary>
/// Describes a class-based data source that provides test cases at runtime.
/// </summary>
/// <remarks>
/// This descriptor is generated for tests using [ClassDataSource&lt;T&gt;] attribute.
/// At runtime, the data source class is instantiated to produce the actual test cases.
/// </remarks>
public sealed class ClassDataSourceDescriptor
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
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicMethods | DynamicallyAccessedMemberTypes.NonPublicMethods)]
    public Type TestClass { get; init; } = typeof(object);

    /// <summary>
    /// Gets or initializes the name of the test method.
    /// </summary>
    public string MethodName { get; init; } = "";

    /// <summary>
    /// Gets or initializes the generated test class factory.
    /// </summary>
    public TestClassFactoryDelegate? TestClassFactory { get; init; }

    /// <summary>
    /// Gets or initializes the generated runtime-argument test invoker.
    /// </summary>
    public TestMethodWithArgumentsDelegate? TestMethodWithArguments { get; init; }

    /// <summary>
    /// Gets or initializes the types that provide the test data.
    /// Each type must implement <see cref="System.Collections.Generic.IEnumerable{T}"/> where T is object?[].
    /// </summary>
    public Type[] DataSourceTypes { get; init; } = [];

    /// <summary>
    /// Gets or initializes generated factories aligned with <see cref="DataSourceTypes"/>.
    /// </summary>
    public DataSourceProviderDelegate?[] DataSourceFactories { get; init; } = [];

    /// <summary>
    /// Gets or initializes the sharing scope for data source instances.
    /// </summary>
    public SharedType SharedType { get; init; } = SharedType.None;

    /// <summary>
    /// Gets or initializes the key for keyed sharing.
    /// Required when <see cref="SharedType"/> is <see cref="NextUnit.SharedType.Keyed"/>.
    /// </summary>
    public string? SharedKey { get; init; }

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
    /// Gets or initializes the detailed dependency information including proceed-on-failure settings.
    /// </summary>
    public IReadOnlyList<DependencyInfo> DependencyInfos { get; init; } = Array.Empty<DependencyInfo>();

    /// <summary>
    /// Gets or initializes a value indicating whether the test should be skipped.
    /// </summary>
    public bool IsSkipped { get; init; }

    /// <summary>
    /// Gets or initializes the reason why the test is skipped, or <c>null</c> if not skipped.
    /// </summary>
    public string? SkipReason { get; init; }

    /// <summary>
    /// Gets or initializes a value indicating whether the test is marked as explicit.
    /// Explicit tests only run when specifically selected or when the --explicit flag is used.
    /// </summary>
    public bool IsExplicit { get; init; }

    /// <summary>
    /// Gets or initializes the reason why the test is marked as explicit, or <c>null</c> if not provided.
    /// </summary>
    public string? ExplicitReason { get; init; }

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

    /// <summary>
    /// Gets or initializes a value indicating whether the test class constructor requires an ITestContext parameter.
    /// </summary>
    public bool RequiresTestContext { get; init; }

    /// <summary>
    /// Gets or initializes the timeout for the test in milliseconds, or <c>null</c> if no timeout is specified.
    /// </summary>
    public int? TimeoutMs { get; init; }

    /// <summary>
    /// Gets or initializes the retry configuration for the test.
    /// </summary>
    public RetryInfo Retry { get; init; } = new();

    /// <summary>
    /// Gets or initializes the cultures the test runs under.
    /// </summary>
    public TestCultureInfo Culture { get; init; } = TestCultureInfo.None;

    /// <summary>
    /// Gets or initializes the custom display name template with optional placeholders ({0}, {1}, etc.).
    /// </summary>
    public string? CustomDisplayNameTemplate { get; init; }

    /// <summary>
    /// Gets or initializes the type that implements <see cref="IDisplayNameFormatter"/> for custom formatting.
    /// </summary>
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)]
    public Type? DisplayNameFormatterType { get; init; }

    /// <summary>
    /// Gets or initializes the execution priority for the test.
    /// Higher values indicate higher priority (run first). Default is 0.
    /// </summary>
    public int Priority { get; init; }
}
