namespace NextUnit.SampleTests;

/// <summary>
/// Tests demonstrating lifecycle hooks.
/// </summary>
public class LifecycleTests
{
    private int _instanceSetupCount;

    [Before(LifecycleScope.Test)]
    public void Setup()
    {
        _instanceSetupCount++;
    }

    [After(LifecycleScope.Test)]
    public void Teardown()
    {
        // Cleanup after each test
    }

    [Test]
    public void FirstTest()
    {
        Assert.Equal(1, _instanceSetupCount);
    }

    [Test]
    public void SecondTest()
    {
        // Each test gets a new instance
        Assert.Equal(1, _instanceSetupCount);
    }
}
