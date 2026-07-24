namespace NextUnit.Core.Tests;

/// <summary>
/// Behavioral tests for Assert.Fail, which always fails the current test.
/// </summary>
public class AssertFailTests
{
    [Test]
    public void Fail_NoMessage_ThrowsWithDefaultMessage()
    {
        var ex = Assert.Throws<AssertionFailedException>(() => Assert.Fail());
        Assert.Contains("Assert.Fail", ex.Message);
    }

    [Test]
    public void Fail_WithMessage_ThrowsWithThatMessage()
    {
        var ex = Assert.Throws<AssertionFailedException>(() => Assert.Fail("explicit failure"));
        Assert.Equal("explicit failure", ex.Message);
    }
}
