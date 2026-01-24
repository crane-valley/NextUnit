namespace NextUnit.SampleTests;

/// <summary>
/// Demonstrates the [Explicit] attribute for tests that should only run when explicitly requested.
/// </summary>
public class ExplicitTests
{
    /// <summary>
    /// This test is marked as explicit without a reason.
    /// It will be skipped unless --explicit flag is used.
    /// </summary>
    [Test]
    [Explicit]
    public void ExplicitTest_NoReason()
    {
        Assert.Equal(2, 1 + 1);
    }

    /// <summary>
    /// This test is marked as explicit with a reason explaining why.
    /// It will be skipped unless --explicit flag is used.
    /// </summary>
    [Test]
    [Explicit("Requires manual database setup")]
    public void ExplicitTest_WithReason()
    {
        Assert.Contains("ell", "Hello");
    }

    /// <summary>
    /// This is a normal test that should always run.
    /// </summary>
    [Test]
    public void NormalTest_ShouldAlwaysRun()
    {
        Assert.True(true);
    }
}

/// <summary>
/// Demonstrates the [Explicit] attribute applied at the class level.
/// All tests in this class will only run when explicitly requested.
/// </summary>
[Explicit("Long-running performance tests")]
public class ExplicitClassTests
{
    /// <summary>
    /// This test is in an explicit class, so it will be skipped unless --explicit flag is used.
    /// </summary>
    [Test]
    public void TestInExplicitClass()
    {
        Assert.Equal(42, 42);
    }

    /// <summary>
    /// Another test in the explicit class.
    /// </summary>
    [Test]
    public void AnotherTestInExplicitClass()
    {
        Assert.Contains("plic", "explicit");
    }
}
