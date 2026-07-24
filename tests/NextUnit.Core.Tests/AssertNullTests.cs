namespace NextUnit.Core.Tests;

/// <summary>
/// Behavioral tests for Assert.Null and Assert.NotNull.
/// </summary>
public class AssertNullTests
{
    [Test]
    public void Null_ValueIsNull_DoesNotThrow()
    {
        Assert.Null(null);
    }

    [Test]
    public void Null_ValueIsNotNull_Throws()
    {
        var ex = Assert.Throws<AssertionFailedException>(() => Assert.Null("value"));
        Assert.Contains("Expected null", ex.Message);
    }

    [Test]
    public void Null_ValueIsNotNull_UsesCustomMessage()
    {
        var ex = Assert.Throws<AssertionFailedException>(() => Assert.Null("value", "should be null"));
        Assert.Equal("should be null", ex.Message);
    }

    [Test]
    public void NotNull_ValueIsNotNull_DoesNotThrow()
    {
        Assert.NotNull("value");
    }

    [Test]
    public void NotNull_ValueIsNull_Throws()
    {
        var ex = Assert.Throws<AssertionFailedException>(() => Assert.NotNull(null));
        Assert.Contains("Expected non-null", ex.Message);
    }

    [Test]
    public void NotNull_ValueIsNull_UsesCustomMessage()
    {
        var ex = Assert.Throws<AssertionFailedException>(() => Assert.NotNull(null, "must not be null"));
        Assert.Equal("must not be null", ex.Message);
    }
}
