namespace NextUnit.SampleTests;

/// <summary>
/// Demonstrates timeout functionality in NextUnit.
/// </summary>
public class TimeoutTests
{
    [Test]
    [Timeout(5000)] // 5 seconds timeout
    public void TestWithTimeout_Passes()
    {
        // This test should pass because it completes within the timeout
        Assert.True(true);
    }

    [Test]
    [Timeout(100)] // 100ms timeout - this test will timeout
    public async Task TestWithTimeout_TimesOut()
    {
        // This test will exceed the timeout and fail
        await Task.Delay(500); // Wait 500ms, exceeding the 100ms timeout
        Assert.True(true);
    }

    [Test]
    [Timeout(2000)] // 2 seconds timeout
    public async Task TestWithTimeout_AsyncPasses()
    {
        // This async test should pass because it completes within the timeout
        await Task.Delay(100);
        Assert.True(true);
    }

    [Test]
    [Timeout(1000)] // 1 second timeout
    public void TestWithTimeout_QuickExecution()
    {
        // This test completes quickly within the 1 second timeout
        Assert.True(true);
    }
}

/// <summary>
/// Demonstrates class-level timeout that applies to all tests in the class.
/// </summary>
[Timeout(3000)] // 3 seconds default timeout for all tests in this class
public class ClassLevelTimeoutTests
{
    [Test]
    public void TestInheritsClassTimeout()
    {
        // This test inherits the 3 second timeout from the class
        Assert.True(true);
    }

    [Test]
    [Timeout(1000)] // Override class timeout with method-level timeout
    public void TestOverridesClassTimeout()
    {
        // This test uses its own 1 second timeout instead of the class's 3 seconds
        Assert.True(true);
    }

    [Test]
    public async Task AsyncTestInheritsClassTimeout()
    {
        // This async test also inherits the 3 second timeout from the class
        await Task.Delay(100);
        Assert.True(true);
    }
}
