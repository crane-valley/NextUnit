namespace NextUnit.Core.Tests;

/// <summary>
/// Behavioral tests for Assert.Same and Assert.NotSame, which compare object
/// identity via ReferenceEquals rather than value equality.
/// </summary>
public class AssertSameTests
{
    // Distinct instances that are value-equal, to prove Same/NotSame use reference
    // identity rather than Equals.
    private sealed record Box(int Value);

    [Test]
    public void Same_SameReference_DoesNotThrow()
    {
        var instance = new Box(1);
        Assert.Same(instance, instance);
    }

    [Test]
    public void Same_BothNull_DoesNotThrow()
    {
        Assert.Same(null, null);
    }

    [Test]
    public void Same_ValueEqualButDifferentInstances_Throws()
    {
        var ex = Assert.Throws<AssertionFailedException>(
            () => Assert.Same(new Box(1), new Box(1)));
        Assert.Contains("same instance", ex.Message);
    }

    [Test]
    public void Same_ExpectedNullActualNonNull_Throws()
    {
        Assert.Throws<AssertionFailedException>(() => Assert.Same(null, new Box(1)));
    }

    [Test]
    public void Same_DifferentInstances_UsesCustomMessage()
    {
        var ex = Assert.Throws<AssertionFailedException>(
            () => Assert.Same(new Box(1), new Box(2), "should be same"));
        Assert.Equal("should be same", ex.Message);
    }

    [Test]
    public void NotSame_DifferentInstances_DoesNotThrow()
    {
        Assert.NotSame(new Box(1), new Box(1));
    }

    [Test]
    public void NotSame_ExpectedNullActualNonNull_DoesNotThrow()
    {
        Assert.NotSame(null, new Box(1));
    }

    [Test]
    public void NotSame_SameReference_Throws()
    {
        var instance = new Box(1);
        var ex = Assert.Throws<AssertionFailedException>(
            () => Assert.NotSame(instance, instance));
        Assert.Contains("different instances", ex.Message);
    }

    [Test]
    public void NotSame_BothNull_Throws()
    {
        Assert.Throws<AssertionFailedException>(() => Assert.NotSame(null, null));
    }

    [Test]
    public void NotSame_SameReference_UsesCustomMessage()
    {
        var instance = new Box(1);
        var ex = Assert.Throws<AssertionFailedException>(
            () => Assert.NotSame(instance, instance, "must differ"));
        Assert.Equal("must differ", ex.Message);
    }
}
