using NextUnit.Internal;

namespace NextUnit.Platform.Tests;

/// <summary>
/// Builds executable <see cref="TestCaseDescriptor"/> instances for the platform tests.
/// </summary>
/// <remarks>
/// The descriptor carries around two dozen initializer properties, so hand-writing one per test
/// buried the handful of properties a test actually exercises under boilerplate that had to be
/// kept in sync by hand. This builder supplies the identity and activation defaults once and lets
/// each test state only what it varies.
/// </remarks>
internal sealed class TestCaseDescriptorBuilder
{
    private readonly string _id;
    private readonly Type _testClass;
    private readonly TestClassFactoryDelegate? _testClassFactory;

    private string _methodName = "Ok";
    private TestMethodDelegate _testMethod = static (_, _) => Task.CompletedTask;
    private LifecycleInfo _lifecycle = new();
    private ParallelInfo _parallel = new();
    private RetryInfo _retry = new();
    private int? _timeoutMs;
    private int _priority;

    private TestCaseDescriptorBuilder(string id, Type testClass, TestClassFactoryDelegate? testClassFactory)
    {
        _id = id;
        _testClass = testClass;
        _testClassFactory = testClassFactory;
    }

    /// <summary>
    /// Starts a descriptor whose instance comes from a factory delegate, which is the shape the
    /// source generator emits.
    /// </summary>
    public static TestCaseDescriptorBuilder For<TTestClass>(string id)
        where TTestClass : new() =>
        new(id, typeof(TTestClass), static (_, _) => new TTestClass());

    /// <summary>
    /// Starts a descriptor with no factory delegate, leaving the engine to activate the class by
    /// reflection. Kept distinct from <see cref="For{TTestClass}"/> because the two activation
    /// paths are separate engine code.
    /// </summary>
    public static TestCaseDescriptorBuilder ForReflectionActivation(string id, Type testClass) =>
        new(id, testClass, testClassFactory: null);

    public TestCaseDescriptorBuilder WithMethodName(string methodName)
    {
        _methodName = methodName;
        return this;
    }

    public TestCaseDescriptorBuilder WithMethod(TestMethodDelegate testMethod)
    {
        _testMethod = testMethod;
        return this;
    }

    /// <summary>
    /// Marks the test as globally serial. Several cancellation tests depend on this, because a
    /// serial batch keeps the parallel loop from observing the run token and so makes a cleanup
    /// hook the first place cancellation is seen.
    /// </summary>
    public TestCaseDescriptorBuilder Serial()
    {
        _parallel = new ParallelInfo { NotInParallel = true };
        return this;
    }

    public TestCaseDescriptorBuilder WithParallel(ParallelInfo parallel)
    {
        _parallel = parallel;
        return this;
    }

    public TestCaseDescriptorBuilder WithRetry(int count, int? delayMs = null)
    {
        _retry = delayMs is null
            ? new RetryInfo { Count = count }
            : new RetryInfo { Count = count, DelayMs = delayMs.Value };
        return this;
    }

    /// <summary>
    /// Configures retry with the policy factory shape the generator emits for
    /// <c>[Retry&lt;TPolicy&gt;]</c>.
    /// </summary>
    public TestCaseDescriptorBuilder WithRetryPolicy(int count, RetryPolicyFactoryDelegate policyFactory, int? delayMs = null)
    {
        _retry = new RetryInfo
        {
            Count = count,
            DelayMs = delayMs ?? 0,
            PolicyFactory = policyFactory
        };
        return this;
    }

    public TestCaseDescriptorBuilder WithTimeout(int timeoutMs)
    {
        _timeoutMs = timeoutMs;
        return this;
    }

    public TestCaseDescriptorBuilder WithPriority(int priority)
    {
        _priority = priority;
        return this;
    }

    public TestCaseDescriptorBuilder WithLifecycle(LifecycleInfo lifecycle)
    {
        _lifecycle = lifecycle;
        return this;
    }

    public TestCaseDescriptorBuilder WithBeforeClass(params LifecycleMethodDelegate[] methods)
    {
        _lifecycle = new LifecycleInfo { BeforeClassMethods = methods };
        return this;
    }

    public TestCaseDescriptorBuilder WithAfterClass(params LifecycleMethodDelegate[] methods)
    {
        _lifecycle = new LifecycleInfo { AfterClassMethods = methods };
        return this;
    }

    public TestCaseDescriptor Build() => new()
    {
        Id = new TestCaseId(_id),
        DisplayName = _id,
        TestClass = _testClass,
        MethodName = _methodName,
        TestMethod = _testMethod,
        TestClassFactory = _testClassFactory,
        Lifecycle = _lifecycle,
        Parallel = _parallel,
        Retry = _retry,
        TimeoutMs = _timeoutMs,
        Priority = _priority
    };
}
