namespace NextUnit;

/// <summary>
/// Marks a method as a test case to be executed by the NextUnit test framework.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class TestAttribute : Attribute
{
}

/// <summary>
/// Defines the lifecycle scope for setup and teardown methods.
/// </summary>
public enum LifecycleScope
{
    /// <summary>
    /// Executes before or after each individual test.
    /// </summary>
    Test,

    /// <summary>
    /// Executes before or after all tests in a class.
    /// </summary>
    Class,

    /// <summary>
    /// Executes before or after all tests in an assembly.
    /// </summary>
    Assembly,

    /// <summary>
    /// Executes before or after all tests in a test session.
    /// </summary>
    Session,

    /// <summary>
    /// Executes during the test discovery phase.
    /// </summary>
    Discovery,
}

/// <summary>
/// Marks a method to be executed before tests at the specified lifecycle scope.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public sealed class BeforeAttribute : Attribute
{
    /// <summary>
    /// Gets the lifecycle scope at which this setup method should execute.
    /// </summary>
    public LifecycleScope Scope { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="BeforeAttribute"/> class.
    /// </summary>
    /// <param name="scope">The lifecycle scope for the setup method.</param>
    public BeforeAttribute(LifecycleScope scope)
    {
        Scope = scope;
    }
}

/// <summary>
/// Marks a method to be executed after tests at the specified lifecycle scope.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public sealed class AfterAttribute : Attribute
{
    /// <summary>
    /// Gets the lifecycle scope at which this teardown method should execute.
    /// </summary>
    public LifecycleScope Scope { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="AfterAttribute"/> class.
    /// </summary>
    /// <param name="scope">The lifecycle scope for the teardown method.</param>
    public AfterAttribute(LifecycleScope scope)
    {
        Scope = scope;
    }
}

/// <summary>
/// Specifies the maximum degree of parallelism for test execution at the assembly, class, or method level.
/// </summary>
[AttributeUsage(AttributeTargets.Assembly | AttributeTargets.Class | AttributeTargets.Method)]
public sealed class ParallelLimitAttribute : Attribute
{
    /// <summary>
    /// Gets the maximum number of tests that can run in parallel.
    /// </summary>
    public int MaxDegreeOfParallelism { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ParallelLimitAttribute"/> class.
    /// </summary>
    /// <param name="maxDegreeOfParallelism">The maximum number of tests to run in parallel.</param>
    public ParallelLimitAttribute(int maxDegreeOfParallelism)
    {
        MaxDegreeOfParallelism = maxDegreeOfParallelism;
    }
}

/// <summary>
/// Indicates that a test class or method should not be executed in parallel with other tests
/// that share the same constraint keys.
/// </summary>
/// <remarks>
/// <para>
/// When used without constraint keys, the test will not run in parallel with any other test
/// marked with <see cref="NotInParallelAttribute"/>.
/// </para>
/// <para>
/// When used with constraint keys, the test will only be serialized with other tests that
/// share at least one constraint key. This allows fine-grained control over which tests
/// need exclusive access to shared resources.
/// </para>
/// <example>
/// <code>
/// // Tests that need exclusive database access
/// [NotInParallel("Database")]
/// [Test]
/// public void TestDatabaseOperation() { }
///
/// // Tests that need exclusive file system and database access
/// [NotInParallel("Database", "FileSystem")]
/// [Test]
/// public void TestDatabaseAndFileOperation() { }
/// </code>
/// </example>
/// </remarks>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class NotInParallelAttribute : Attribute
{
    /// <summary>
    /// Gets the constraint keys that determine which tests should not run in parallel together.
    /// </summary>
    /// <remarks>
    /// Tests with overlapping constraint keys will not run in parallel with each other.
    /// An empty array means the test will not run in parallel with any other
    /// <see cref="NotInParallelAttribute"/> marked tests.
    /// </remarks>
    public string[] ConstraintKeys { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="NotInParallelAttribute"/> class
    /// with no constraint keys (fully serial execution).
    /// </summary>
    public NotInParallelAttribute()
    {
        ConstraintKeys = Array.Empty<string>();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="NotInParallelAttribute"/> class
    /// with the specified constraint keys.
    /// </summary>
    /// <param name="constraintKeys">
    /// The constraint keys that identify shared resources.
    /// Tests sharing any constraint key will not run in parallel.
    /// </param>
    public NotInParallelAttribute(params string[] constraintKeys)
    {
        ConstraintKeys = constraintKeys ?? Array.Empty<string>();
    }
}

/// <summary>
/// Groups tests together for exclusive execution within the group.
/// </summary>
/// <remarks>
/// <para>
/// Tests in the same parallel group will run in parallel with each other,
/// but the entire group will not run in parallel with other groups or ungrouped tests.
/// </para>
/// <para>
/// This is useful when you have a set of tests that can safely run in parallel with each other
/// but need isolation from other test groups (e.g., tests sharing a test database).
/// </para>
/// <example>
/// <code>
/// // These tests run in parallel with each other, but not with other tests
/// [ParallelGroup("UserTests")]
/// public class UserRepositoryTests
/// {
///     [Test] public void CreateUser() { }
///     [Test] public void DeleteUser() { }
/// }
///
/// // These tests also run in parallel with each other, but not with UserTests
/// [ParallelGroup("OrderTests")]
/// public class OrderRepositoryTests
/// {
///     [Test] public void CreateOrder() { }
///     [Test] public void CancelOrder() { }
/// }
/// </code>
/// </example>
/// </remarks>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class ParallelGroupAttribute : Attribute
{
    /// <summary>
    /// Gets the name of the parallel group.
    /// </summary>
    public string GroupName { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ParallelGroupAttribute"/> class.
    /// </summary>
    /// <param name="groupName">The name of the parallel group.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="groupName"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="groupName"/> is empty or whitespace.</exception>
    public ParallelGroupAttribute(string groupName)
    {
        if (groupName is null)
        {
            throw new ArgumentNullException(nameof(groupName));
        }
        if (string.IsNullOrWhiteSpace(groupName))
        {
            throw new ArgumentException("Group name cannot be empty or whitespace.", nameof(groupName));
        }
        GroupName = groupName;
    }
}

/// <summary>
/// Specifies that a test method depends on other test methods.
/// </summary>
/// <remarks>
/// <para>
/// By default, if a dependency fails, the dependent test will be skipped.
/// Use <see cref="ProceedOnFailure"/> to run the test even if dependencies fail.
/// </para>
/// <example>
/// <code>
/// // This test only runs if SetupDatabase passes
/// [DependsOn(nameof(SetupDatabase))]
/// [Test]
/// public void TestDatabaseQuery() { }
///
/// // This test runs even if SetupDatabase fails (for cleanup scenarios)
/// [DependsOn(nameof(SetupDatabase), ProceedOnFailure = true)]
/// [Test]
/// public void CleanupDatabase() { }
/// </code>
/// </example>
/// </remarks>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public sealed class DependsOnAttribute : Attribute
{
    /// <summary>
    /// Gets the names of the test methods that must complete before this test can run.
    /// </summary>
    public string[] MethodNames { get; }

    /// <summary>
    /// Gets or sets a value indicating whether the test should proceed even if dependencies fail.
    /// </summary>
    /// <remarks>
    /// When <c>true</c>, the test will run regardless of whether the dependency passed, failed, or was skipped.
    /// When <c>false</c> (default), the test will be skipped if any dependency fails or is skipped.
    /// </remarks>
    public bool ProceedOnFailure { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="DependsOnAttribute"/> class.
    /// </summary>
    /// <param name="methodNames">The names of the test methods this test depends on.</param>
    public DependsOnAttribute(params string[] methodNames)
    {
        MethodNames = methodNames;
        ProceedOnFailure = false;
    }
}

/// <summary>
/// Specifies the maximum execution time for a test, class, or assembly before it is considered timed out.
/// </summary>
/// <remarks>
/// When applied to a test method, the timeout applies to that specific test.
/// When applied to a class, the timeout applies to all tests in that class (unless overridden by method-level timeout).
/// When applied to an assembly, the timeout applies to all tests in that assembly (unless overridden by class or method-level timeout).
/// </remarks>
[AttributeUsage(AttributeTargets.Assembly | AttributeTargets.Class | AttributeTargets.Method)]
public sealed class TimeoutAttribute : Attribute
{
    /// <summary>
    /// Gets the timeout duration in milliseconds.
    /// </summary>
    public int Milliseconds { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="TimeoutAttribute"/> class.
    /// </summary>
    /// <param name="milliseconds">The timeout duration in milliseconds.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="milliseconds"/> is less than or equal to zero.</exception>
    public TimeoutAttribute(int milliseconds)
    {
        if (milliseconds <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(milliseconds), "Timeout must be greater than zero.");
        }
        Milliseconds = milliseconds;
    }
}

/// <summary>
/// Specifies that a test should be automatically retried on failure.
/// </summary>
/// <remarks>
/// When a test fails, it will be retried up to the specified number of times.
/// The test passes if any retry succeeds. This is useful for handling intermittent failures.
/// </remarks>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public sealed class RetryAttribute : Attribute
{
    /// <summary>
    /// Gets the maximum number of retry attempts.
    /// </summary>
    public int Count { get; }

    /// <summary>
    /// Gets the delay in milliseconds between retry attempts.
    /// </summary>
    public int DelayMs { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="RetryAttribute"/> class.
    /// </summary>
    /// <param name="count">The maximum number of retry attempts. Must be at least 1.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="count"/> is less than 1.</exception>
    public RetryAttribute(int count)
    {
        if (count < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(count), "Retry count must be at least 1.");
        }
        Count = count;
        DelayMs = 0;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="RetryAttribute"/> class with a delay between retries.
    /// </summary>
    /// <param name="count">The maximum number of retry attempts. Must be at least 1.</param>
    /// <param name="delayMs">The delay in milliseconds between retry attempts. Must be non-negative.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="count"/> is less than 1 or <paramref name="delayMs"/> is negative.</exception>
    public RetryAttribute(int count, int delayMs)
    {
        if (count < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(count), "Retry count must be at least 1.");
        }
        if (delayMs < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(delayMs), "Delay must be non-negative.");
        }
        Count = count;
        DelayMs = delayMs;
    }
}

/// <summary>
/// Marks a test as known to be flaky (intermittently failing).
/// </summary>
/// <remarks>
/// This attribute is informational and can be used to:
/// <list type="bullet">
/// <item>Document tests that are known to have intermittent failures</item>
/// <item>Filter or group flaky tests in test reports</item>
/// <item>Apply special handling to flaky tests</item>
/// </list>
/// Consider using <see cref="RetryAttribute"/> in combination with this attribute
/// to automatically retry flaky tests.
/// </remarks>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public sealed class FlakyAttribute : Attribute
{
    /// <summary>
    /// Gets the reason why the test is considered flaky.
    /// </summary>
    public string? Reason { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="FlakyAttribute"/> class.
    /// </summary>
    public FlakyAttribute()
    {
        Reason = null;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="FlakyAttribute"/> class with a reason.
    /// </summary>
    /// <param name="reason">The reason why the test is considered flaky.</param>
    public FlakyAttribute(string reason)
    {
        Reason = reason;
    }
}

/// <summary>
/// Specifies that a test should be repeated multiple times.
/// </summary>
/// <remarks>
/// Each repeat is executed as a separate test case, allowing individual tracking
/// of pass/fail status per repeat. The repeat index is available via
/// <see cref="NextUnit.Core.TestContext.Current"/>.<see cref="NextUnit.Core.ITestContext.RepeatIndex"/>.
/// </remarks>
/// <example>
/// <code>
/// [Test]
/// [Repeat(5)]
/// public void TestRunsFiveTimes()
/// {
///     var repeatIndex = TestContext.Current?.RepeatIndex;
///     // repeatIndex will be 0, 1, 2, 3, 4 for each repeat
/// }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class RepeatAttribute : Attribute
{
    /// <summary>
    /// Gets the number of times to repeat the test.
    /// </summary>
    public int Count { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="RepeatAttribute"/> class.
    /// </summary>
    /// <param name="count">The number of times to repeat the test. Must be at least 1.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="count"/> is less than 1.</exception>
    public RepeatAttribute(int count)
    {
        if (count < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(count), "Repeat count must be at least 1.");
        }
        Count = count;
    }
}
