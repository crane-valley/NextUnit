namespace NextUnit.SampleTests;

/// <summary>
/// Tests demonstrating Category and Tag filtering functionality.
/// </summary>
[Category("Integration")]
public class CategoryAndTagTests
{
    [Test]
    [Category("Database")]
    [Tag("Slow")]
    public void DatabaseQuery_ReturnsResults()
    {
        // Simulated database test
        Assert.True(true);
    }

    [Test]
    [Category("API")]
    [Tag("Fast")]
    public void ApiCall_ReturnsSuccess()
    {
        // Simulated API test
        Assert.True(true);
    }

    [Test]
    [Category("Database")]
    [Category("API")]
    [Tag("Slow")]
    [Tag("RequiresNetwork")]
    public void IntegrationTest_DatabaseAndApi()
    {
        // Combined integration test
        Assert.True(true);
    }

    [Test]
    [Tag("Fast")]
    public void UnitTest_SimpleLogic()
    {
        // Simple unit test - inherits Integration category from class
        var result = 2 + 2;
        Assert.Equal(4, result);
    }
}

/// <summary>
/// Test class without categories demonstrating filtering differences.
/// </summary>
public class UncategorizedTests
{
    [Test]
    [Tag("Smoke")]
    public void SmokeTest_BasicFunctionality()
    {
        Assert.NotNull(this);
    }

    [Test]
    public void PlainTest_NoMetadata()
    {
        Assert.True(true);
    }
}
