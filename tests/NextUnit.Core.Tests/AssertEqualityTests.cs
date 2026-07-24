namespace NextUnit.Core.Tests;

/// <summary>
/// Behavioral tests for Assert.Equal and Assert.NotEqual across all overloads,
/// including rich message formatting for strings, collections, and complex objects.
/// </summary>
public class AssertEqualityTests
{
    private sealed record Point(int X, int Y);

    private sealed class AbsoluteIntComparer : IEqualityComparer<int>
    {
        public bool Equals(int x, int y) => Math.Abs(x) == Math.Abs(y);

        public int GetHashCode(int obj) => Math.Abs(obj).GetHashCode();
    }

    [Test]
    public void Equal_EqualIntegers_DoesNotThrow()
    {
        Assert.Equal(5, 5);
    }

    [Test]
    public void Equal_UnequalIntegers_ThrowsWithBothValues()
    {
        var ex = Assert.Throws<AssertionFailedException>(() => Assert.Equal(5, 7));
        Assert.Contains("5", ex.Message);
        Assert.Contains("7", ex.Message);
    }

    [Test]
    public void Equal_BothNull_DoesNotThrow()
    {
        Assert.Equal<object?>(null, null);
    }

    [Test]
    public void Equal_EqualStrings_DoesNotThrow()
    {
        Assert.Equal("abc", "abc");
    }

    [Test]
    public void Equal_UnequalStrings_ThrowsWithRichStringDiff()
    {
        var ex = Assert.Throws<AssertionFailedException>(() => Assert.Equal("abc", "abd"));
        Assert.Contains("String assertion failed", ex.Message);
        Assert.Contains("First difference at index", ex.Message);
    }

    [Test]
    public void Equal_EqualCollections_DoesNotThrow()
    {
        Assert.Equal(new[] { 1, 2, 3 }, new[] { 1, 2, 3 });
    }

    [Test]
    public void Equal_UnequalCollections_ThrowsWithRichCollectionDiff()
    {
        var ex = Assert.Throws<AssertionFailedException>(
            () => Assert.Equal(new[] { 1, 2, 3 }, new[] { 1, 2, 4 }));
        Assert.Contains("Collection assertion failed", ex.Message);
    }

    [Test]
    public void Equal_DifferentLengthCollections_Throws()
    {
        var ex = Assert.Throws<AssertionFailedException>(
            () => Assert.Equal(new[] { 1, 2 }, new[] { 1, 2, 3 }));
        Assert.Contains("Collection assertion failed", ex.Message);
    }

    [Test]
    public void Equal_UnequalComplexObjects_ThrowsWithRichObjectDiff()
    {
        var ex = Assert.Throws<AssertionFailedException>(
            () => Assert.Equal(new Point(1, 2), new Point(3, 4)));
        Assert.Contains("Object assertion failed", ex.Message);
    }

    [Test]
    public void Equal_CustomMessageOverridesRichMessage()
    {
        var ex = Assert.Throws<AssertionFailedException>(() => Assert.Equal(1, 2, "mismatch happened"));
        Assert.Equal("mismatch happened", ex.Message);
    }

    [Test]
    public void Equal_WithComparer_ConsideredEqual_DoesNotThrow()
    {
        Assert.Equal(-5, 5, new AbsoluteIntComparer());
    }

    [Test]
    public void Equal_WithComparer_NotEqual_Throws()
    {
        var ex = Assert.Throws<AssertionFailedException>(
            () => Assert.Equal(5, 6, new AbsoluteIntComparer()));
        Assert.Contains("Expected", ex.Message);
    }

    [Test]
    public void Equal_WithNullComparer_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(
            () => Assert.Equal(1, 1, (IEqualityComparer<int>)null!));
    }

    [Test]
    public void Equal_DoubleWithinPrecision_DoesNotThrow()
    {
        Assert.Equal(1.0, 1.0001, 3);
    }

    [Test]
    public void Equal_DoubleOutsidePrecision_ThrowsWithDifference()
    {
        var ex = Assert.Throws<AssertionFailedException>(() => Assert.Equal(1.0, 1.5, 3));
        Assert.Contains("Difference", ex.Message);
    }

    [Test]
    public void Equal_DoubleNegativePrecision_ThrowsArgumentOutOfRange()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Assert.Equal(1.0, 1.0, -1));
    }

    [Test]
    public void Equal_DoubleNaNEqualsNaN_DoesNotThrow()
    {
        Assert.Equal(double.NaN, double.NaN, 3);
    }

    [Test]
    public void Equal_DoubleNaNVersusNumber_Throws()
    {
        Assert.Throws<AssertionFailedException>(() => Assert.Equal(double.NaN, 1.0, 3));
    }

    [Test]
    public void Equal_DecimalWithinPrecision_DoesNotThrow()
    {
        Assert.Equal(1.0m, 1.001m, 2);
    }

    [Test]
    public void Equal_DecimalOutsidePrecision_ThrowsWithDifference()
    {
        var ex = Assert.Throws<AssertionFailedException>(() => Assert.Equal(1.0m, 1.5m, 2));
        Assert.Contains("Difference", ex.Message);
    }

    [Test]
    public void NotEqual_DifferentValues_DoesNotThrow()
    {
        Assert.NotEqual(1, 2);
    }

    [Test]
    public void NotEqual_EqualValues_ThrowsWithActual()
    {
        var ex = Assert.Throws<AssertionFailedException>(() => Assert.NotEqual(3, 3));
        Assert.Contains("3", ex.Message);
    }

    [Test]
    public void NotEqual_DoubleOutsidePrecision_DoesNotThrow()
    {
        Assert.NotEqual(1.0, 1.5, 3);
    }

    [Test]
    public void NotEqual_DoubleWithinPrecision_Throws()
    {
        Assert.Throws<AssertionFailedException>(() => Assert.NotEqual(1.0, 1.0001, 3));
    }

    [Test]
    public void NotEqual_DoubleNegativePrecision_ThrowsArgumentOutOfRange()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Assert.NotEqual(1.0, 2.0, -1));
    }

    [Test]
    public void NotEqual_DecimalOutsidePrecision_DoesNotThrow()
    {
        Assert.NotEqual(1.0m, 1.5m, 2);
    }

    [Test]
    public void NotEqual_DecimalWithinPrecision_Throws()
    {
        Assert.Throws<AssertionFailedException>(() => Assert.NotEqual(1.0m, 1.001m, 2));
    }
}
