namespace NextUnit.Core.Tests;

/// <summary>
/// Behavioral tests for the tolerance-based Assert.Equal and Assert.NotEqual double
/// overloads, including the overload-resolution boundary against the existing
/// precision-based (int third argument) overload, and NaN/Infinity handling.
/// </summary>
public class AssertToleranceTests
{
    [Test]
    public void Equal_WithinTolerance_DoesNotThrow()
    {
        Assert.Equal(1.0, 1.4, 0.5);
    }

    [Test]
    public void Equal_ExactMatch_DoesNotThrow()
    {
        Assert.Equal(2.5, 2.5, 0.0);
    }

    [Test]
    public void Equal_OutsideTolerance_ThrowsWithDifference()
    {
        var ex = Assert.Throws<AssertionFailedException>(() => Assert.Equal(1.0, 2.0, 0.5));
        Assert.Contains("Difference", ex.Message);
    }

    [Test]
    public void Equal_OutsideTolerance_UsesCustomMessage()
    {
        var ex = Assert.Throws<AssertionFailedException>(
            () => Assert.Equal(1.0, 2.0, 0.5, "too far apart"));
        Assert.Equal("too far apart", ex.Message);
    }

    [Test]
    public void Equal_NegativeTolerance_ThrowsArgumentOutOfRange()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Assert.Equal(1.0, 1.0, -0.5));
    }

    [Test]
    public void Equal_NaNTolerance_ThrowsArgumentOutOfRange()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Assert.Equal(1.0, 1.0, double.NaN));
    }

    [Test]
    public void Equal_NaNEqualsNaN_DoesNotThrow()
    {
        // xUnit semantics: NaN is considered equal to NaN for tolerance comparison.
        Assert.Equal(double.NaN, double.NaN, 0.5);
    }

    [Test]
    public void Equal_NaNVersusNumber_Throws()
    {
        Assert.Throws<AssertionFailedException>(() => Assert.Equal(double.NaN, 1.0, 0.5));
    }

    [Test]
    public void Equal_SameInfinity_DoesNotThrow()
    {
        Assert.Equal(double.PositiveInfinity, double.PositiveInfinity, 0.5);
    }

    [Test]
    public void Equal_OppositeInfinities_Throws()
    {
        Assert.Throws<AssertionFailedException>(
            () => Assert.Equal(double.PositiveInfinity, double.NegativeInfinity, 0.5));
    }

    // Overload-resolution proof: an int literal as the third argument must keep binding
    // to the precision overload (tolerance 10^-precision), not the double tolerance
    // overload. With precision 1 the tolerance is 0.1, so a 0.4 gap fails; had it bound
    // to the double overload (tolerance 1.0) the same call would pass.
    [Test]
    public void Equal_IntThirdArgument_ResolvesToPrecisionOverload()
    {
        Assert.Throws<AssertionFailedException>(() => Assert.Equal(1.0, 1.4, 1));
    }

    [Test]
    public void Equal_IntThirdArgument_PrecisionSemanticsPass()
    {
        // Precision 1 => tolerance 0.1; a 0.05 gap is within it.
        Assert.Equal(1.0, 1.05, 1);
    }

    [Test]
    public void NotEqual_OutsideTolerance_DoesNotThrow()
    {
        Assert.NotEqual(1.0, 2.0, 0.5);
    }

    [Test]
    public void NotEqual_WithinTolerance_Throws()
    {
        var ex = Assert.Throws<AssertionFailedException>(() => Assert.NotEqual(1.0, 1.4, 0.5));
        Assert.Contains("Did not expect", ex.Message);
    }

    [Test]
    public void NotEqual_NegativeTolerance_ThrowsArgumentOutOfRange()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Assert.NotEqual(1.0, 2.0, -0.5));
    }

    [Test]
    public void NotEqual_NaNTolerance_ThrowsArgumentOutOfRange()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Assert.NotEqual(1.0, 2.0, double.NaN));
    }

    [Test]
    public void NotEqual_NaNVersusNumber_DoesNotThrow()
    {
        Assert.NotEqual(double.NaN, 1.0, 0.5);
    }

    [Test]
    public void NotEqual_NaNEqualsNaN_Throws()
    {
        // NaN equals NaN under tolerance semantics, so NotEqual must fail.
        Assert.Throws<AssertionFailedException>(() => Assert.NotEqual(double.NaN, double.NaN, 0.5));
    }

    [Test]
    public void NotEqual_IntThirdArgument_ResolvesToPrecisionOverload()
    {
        // Precision 1 => tolerance 0.1; a 0.4 gap is outside it, so NotEqual passes.
        // Had this bound to the double overload (tolerance 1.0), it would have thrown.
        Assert.NotEqual(1.0, 1.4, 1);
    }
}
