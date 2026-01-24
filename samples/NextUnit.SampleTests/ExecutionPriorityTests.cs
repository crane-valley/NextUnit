namespace NextUnit.SampleTests;

/// <summary>
/// Tests demonstrating the [ExecutionPriority] attribute functionality.
/// Tests must run serially to validate execution order.
/// </summary>
[NotInParallel("execution-priority")]
public class ExecutionPriorityTests
{
    private static readonly List<string> _executionOrder = new();

    /// <summary>
    /// Runs first (highest priority in this class).
    /// </summary>
    [Test]
    [ExecutionPriority(100)]
    public void HighPriorityTest()
    {
        _executionOrder.Add(nameof(HighPriorityTest));
    }

    /// <summary>
    /// Runs second (medium priority).
    /// </summary>
    [Test]
    [ExecutionPriority(50)]
    public void MediumPriorityTest()
    {
        _executionOrder.Add(nameof(MediumPriorityTest));
    }

    /// <summary>
    /// Runs third (default priority = 0).
    /// </summary>
    [Test]
    public void DefaultPriorityTest()
    {
        _executionOrder.Add(nameof(DefaultPriorityTest));
    }

    /// <summary>
    /// Runs last (negative priority).
    /// </summary>
    [Test]
    [ExecutionPriority(-100)]
    public void LowPriorityTest()
    {
        _executionOrder.Add(nameof(LowPriorityTest));
    }

    /// <summary>
    /// Validates the execution order. This test depends on all others completing first.
    /// </summary>
    [Test]
    [ExecutionPriority(-1000)]
    [DependsOn(
        nameof(HighPriorityTest),
        nameof(MediumPriorityTest),
        nameof(DefaultPriorityTest),
        nameof(LowPriorityTest))]
    public void Validate_executionOrder()
    {
        // Higher priority runs first: 100 > 50 > 0 > -100
        Assert.Equal(4, _executionOrder.Count);
        Assert.Equal(nameof(HighPriorityTest), _executionOrder[0]);
        Assert.Equal(nameof(MediumPriorityTest), _executionOrder[1]);
        Assert.Equal(nameof(DefaultPriorityTest), _executionOrder[2]);
        Assert.Equal(nameof(LowPriorityTest), _executionOrder[3]);
    }
}

/// <summary>
/// Tests class-level ExecutionPriority inheritance.
/// </summary>
[ExecutionPriority(50)]
public class ClassLevelPriorityTests
{
    /// <summary>
    /// Inherits class-level priority of 50.
    /// </summary>
    [Test]
    public void InheritsClassPriority()
    {
        // This test has priority 50 from the class
    }

    /// <summary>
    /// Overrides class-level priority with method-level priority.
    /// </summary>
    [Test]
    [ExecutionPriority(200)]
    public void OverridesClassPriority()
    {
        // This test has priority 200 (overrides class-level 50)
    }
}
