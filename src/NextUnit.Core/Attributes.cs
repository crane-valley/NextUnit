using System;

namespace NextUnit;

/// <summary>
/// Marks a method as a test case to be executed by the NextUnit test framework.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class TestAttribute : Attribute { }

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
    public BeforeAttribute(LifecycleScope scope) => Scope = scope;
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
    public AfterAttribute(LifecycleScope scope) => Scope = scope;
}

/// <summary>
/// Specifies the maximum degree of parallelism for test execution at the assembly, class, or method level.
/// </summary>
[AttributeUsage(
    AttributeTargets.Assembly | AttributeTargets.Class | AttributeTargets.Method)]
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
    public ParallelLimitAttribute(int maxDegreeOfParallelism) => MaxDegreeOfParallelism = maxDegreeOfParallelism;
}

/// <summary>
/// Indicates that a test class or method should not be executed in parallel with other tests.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class NotInParallelAttribute : Attribute { }

/// <summary>
/// Specifies that a test method depends on the successful completion of other test methods.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public sealed class DependsOnAttribute : Attribute
{
    /// <summary>
    /// Gets the names of the test methods that must complete before this test can run.
    /// </summary>
    public string[] MethodNames { get; }
    
    /// <summary>
    /// Initializes a new instance of the <see cref="DependsOnAttribute"/> class.
    /// </summary>
    /// <param name="methodNames">The names of the test methods this test depends on.</param>
    public DependsOnAttribute(params string[] methodNames) => MethodNames = methodNames;
}
