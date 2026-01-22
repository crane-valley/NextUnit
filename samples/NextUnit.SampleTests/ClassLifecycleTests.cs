namespace NextUnit.SampleTests;

/// <summary>
/// Tests demonstrating class-scoped lifecycle functionality.
/// </summary>
[NotInParallel]
public class ClassLifecycleTests
{
    private static int _classSetupCount;
    private static int _classTeardownCount;
    private static int _testExecutionCount;

    [Before(LifecycleScope.Class)]
    public static void ClassSetup()
    {
        _classSetupCount++;
        _testExecutionCount = 0;
    }

    [After(LifecycleScope.Class)]
    public static void ClassTeardown()
    {
        _classTeardownCount++;
    }

    [Before(LifecycleScope.Test)]
    public void TestSetup()
    {
        _testExecutionCount++;
    }

    [Test]
    public void FirstTest()
    {
        // ClassSetup should have run once before this test
        Assert.Equal(1, _classSetupCount);
        // This is the first test execution
        Assert.Equal(1, _testExecutionCount);
    }

    [Test]
    public void SecondTest()
    {
        // ClassSetup should still be 1 (runs once per class)
        Assert.Equal(1, _classSetupCount);
        // This is the second test execution
        Assert.Equal(2, _testExecutionCount);
    }

    [Test]
    public void ThirdTest()
    {
        // ClassSetup should still be 1 (runs once per class)
        Assert.Equal(1, _classSetupCount);
        // This is the third test execution
        Assert.Equal(3, _testExecutionCount);
    }
}

/// <summary>
/// Second test class to verify class-scoped lifecycle isolation.
/// </summary>
public class SecondClassLifecycleTests
{
    private static int _setupCount;

    [Before(LifecycleScope.Class)]
    public void Setup()
    {
        _setupCount++;
    }

    [Test]
    public void Test1()
    {
        // Each class gets its own setup
        Assert.Equal(1, _setupCount);
    }

    [Test]
    public void Test2()
    {
        // Setup should still be 1 for this class
        Assert.Equal(1, _setupCount);
    }
}
