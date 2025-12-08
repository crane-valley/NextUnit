namespace NextUnit.SampleTests;

/// <summary>
/// Demonstrates session-scoped lifecycle methods.
/// Session setup runs once before all tests in the entire test session.
/// Session teardown runs once after all tests in the entire test session.
/// </summary>
[NotInParallel]
public class SessionLifecycleTests
{
    private static int _sessionCounter;

    [Before(LifecycleScope.Session)]
    public static void SessionSetup()
    {
        _sessionCounter = 0;
    }

    [After(LifecycleScope.Session)]
    public static void SessionTeardown()
    {
        _sessionCounter = 0;
    }

    [Test]
    public static void SessionTest1()
    {
        _sessionCounter++;
        Assert.Equal(1, _sessionCounter);
    }

    [Test]
    public static void SessionTest2()
    {
        _sessionCounter++;
        Assert.Equal(2, _sessionCounter);
    }

    [Test]
    public static void SessionTest3()
    {
        _sessionCounter++;
        Assert.Equal(3, _sessionCounter);
    }
}
