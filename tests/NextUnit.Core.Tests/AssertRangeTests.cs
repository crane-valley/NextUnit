namespace NextUnit.Core.Tests;

/// <summary>
/// Behavioral tests for the numeric range assertions InRange and NotInRange.
/// </summary>
public class AssertRangeTests
{
    [Test]
    public void InRange_ValueInside_DoesNotThrow()
    {
        Assert.InRange(5, 1, 10);
    }

    [Test]
    public void InRange_ValueAtLowerBound_DoesNotThrow()
    {
        Assert.InRange(1, 1, 10);
    }

    [Test]
    public void InRange_ValueAtUpperBound_DoesNotThrow()
    {
        Assert.InRange(10, 1, 10);
    }

    [Test]
    public void InRange_ValueBelow_Throws()
    {
        var ex = Assert.Throws<AssertionFailedException>(() => Assert.InRange(0, 1, 10));
        Assert.Contains("not in range", ex.Message);
    }

    [Test]
    public void InRange_ValueAbove_Throws()
    {
        Assert.Throws<AssertionFailedException>(() => Assert.InRange(11, 1, 10));
    }

    [Test]
    public void NotInRange_ValueOutside_DoesNotThrow()
    {
        Assert.NotInRange(20, 1, 10);
    }

    [Test]
    public void NotInRange_ValueInside_Throws()
    {
        var ex = Assert.Throws<AssertionFailedException>(() => Assert.NotInRange(5, 1, 10));
        Assert.Contains("in range", ex.Message);
    }

    [Test]
    public void NotInRange_ValueAtBoundary_Throws()
    {
        // Boundaries are treated as inside the range by the implementation.
        Assert.Throws<AssertionFailedException>(() => Assert.NotInRange(1, 1, 10));
    }
}
