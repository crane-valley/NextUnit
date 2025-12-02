namespace NextUnit.SampleTests;

/// <summary>
/// Tests demonstrating dependency ordering.
/// </summary>
public class DependencyTests
{
    private static bool _testACompleted;
    private static bool _testBCompleted;

    [Test]
    public void TestA_Setup()
    {
        _testACompleted = true;
        Assert.True(true);
    }

    [Test]
    [DependsOn(nameof(TestA_Setup))]
    public void TestB_RequiresA()
    {
        Assert.True(_testACompleted, "TestA should have completed before TestB");
        _testBCompleted = true;
    }

    [Test]
    [DependsOn(nameof(TestA_Setup), nameof(TestB_RequiresA))]
    public void TestC_RequiresAandB()
    {
        Assert.True(_testACompleted, "TestA should have completed");
        Assert.True(_testBCompleted, "TestB should have completed");
    }
}
