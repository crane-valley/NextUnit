namespace NextUnit.SampleTests;

/// <summary>
/// Tests demonstrating assembly-scoped lifecycle functionality.
/// </summary>
public class AssemblyLifecycleTests
{
    private static int _assemblySetupCount;
    private static int _assemblyTeardownCount;

    [Before(LifecycleScope.Assembly)]
    public static void AssemblySetup()
    {
        _assemblySetupCount++;
    }

    [After(LifecycleScope.Assembly)]
    public static void AssemblyTeardown()
    {
        _assemblyTeardownCount++;
    }

    [Test]
    public void FirstAssemblyTest()
    {
        // Assembly setup should have run once
        Assert.Equal(1, _assemblySetupCount);
    }

    [Test]
    public void SecondAssemblyTest()
    {
        // Assembly setup should still be 1 (runs once per assembly)
        Assert.Equal(1, _assemblySetupCount);
    }
}
