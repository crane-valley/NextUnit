namespace NextUnit.Core.Tests;

/// <summary>
/// Behavioral tests for Assert.True and Assert.False.
/// </summary>
public class AssertBooleanTests
{
    [Test]
    public void True_ConditionTrue_DoesNotThrow()
    {
        Assert.True(true);
    }

    [Test]
    public void True_ConditionFalse_Throws()
    {
        var ex = Assert.Throws<AssertionFailedException>(() => Assert.True(false));
        Assert.Contains("Expected true but was false", ex.Message);
    }

    [Test]
    public void True_ConditionFalse_UsesCustomMessage()
    {
        var ex = Assert.Throws<AssertionFailedException>(() => Assert.True(false, "custom boolean message"));
        Assert.Equal("custom boolean message", ex.Message);
    }

    [Test]
    public void False_ConditionFalse_DoesNotThrow()
    {
        Assert.False(false);
    }

    [Test]
    public void False_ConditionTrue_Throws()
    {
        var ex = Assert.Throws<AssertionFailedException>(() => Assert.False(true));
        Assert.Contains("Expected false but was true", ex.Message);
    }

    [Test]
    public void False_ConditionTrue_UsesCustomMessage()
    {
        var ex = Assert.Throws<AssertionFailedException>(() => Assert.False(true, "custom false message"));
        Assert.Equal("custom false message", ex.Message);
    }
}
