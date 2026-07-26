using System.Globalization;

namespace NextUnit.Core.Tests;

/// <summary>
/// Characterization tests for the Assert equality family. These pin the exact
/// per-overload semantics (NaN, infinities, signed zero, tolerance boundary) and the
/// exact failure message texts so that restructuring the six tolerance bodies behind a
/// shared comparison core cannot change observable behavior.
/// </summary>
/// <remarks>
/// The precision overloads compare non-finite values exactly and report a plain
/// "Expected/Actual" message, while the absolute-tolerance overloads always report the
/// tolerance and the difference. Both families treat NaN as equal to NaN and each
/// infinity as equal to itself.
/// <para>
/// Every number inside an expected message is interpolated from the same value the
/// assertion under test receives, never written out as a literal. The product formats
/// messages with the current culture, so a hardcoded "0.5" or "NaN" would pin the message
/// to one culture and fail under, say, de-DE. Interpolating derives the expectation through
/// the same formatting the product uses, which leaves only the literal skeleton of each
/// message pinned. <see cref="Messages_AreFormattedWithTheCurrentCulture"/> guards that
/// derivation.
/// </para>
/// </remarks>
public class AssertEqualityCharacterizationTests
{
    private const long PositiveNaNBits = 0x7FF8000000000000L;

    private static double PositiveNaN => BitConverter.Int64BitsToDouble(PositiveNaNBits);

    private readonly record struct Cell(int Value);

    /// <summary>
    /// Implements <see cref="IEquatable{T}"/> without overriding <c>object.Equals</c>, which is
    /// the only shape where <c>EqualityComparer&lt;T&gt;.Default</c> and the static
    /// <c>object.Equals</c> disagree.
    /// </summary>
    private sealed class EquatableOnly(int value) : IEquatable<EquatableOnly>
    {
        public int Value { get; } = value;

        public bool Equals(EquatableOnly? other) => other is not null && other.Value == Value;
    }

    // Assert.Equal(double, double, int precision)

    [Test]
    public void EqualDoublePrecision_NaNVersusNaN_Passes()
    {
        Assert.Equal(double.NaN, double.NaN, 3);
    }

    [Test]
    public void EqualDoublePrecision_NaNVersusNumber_ThrowsWithPlainMessage()
    {
        var ex = Assert.Throws<AssertionFailedException>(() => Assert.Equal(double.NaN, 1.0, 3));
        Assert.Equal($"Expected: {double.NaN}; Actual: {1.0}", ex.Message);
    }

    [Test]
    public void EqualDoublePrecision_PositiveInfinityPair_Passes()
    {
        Assert.Equal(double.PositiveInfinity, double.PositiveInfinity, 3);
    }

    [Test]
    public void EqualDoublePrecision_NegativeInfinityPair_Passes()
    {
        Assert.Equal(double.NegativeInfinity, double.NegativeInfinity, 3);
    }

    [Test]
    public void EqualDoublePrecision_OppositeInfinities_ThrowsWithPlainMessage()
    {
        var ex = Assert.Throws<AssertionFailedException>(
            () => Assert.Equal(double.PositiveInfinity, double.NegativeInfinity, 3));
        Assert.Equal(
            $"Expected: {double.PositiveInfinity}; Actual: {double.NegativeInfinity}",
            ex.Message);
    }

    [Test]
    public void EqualDoublePrecision_SignedZero_Passes()
    {
        Assert.Equal(0.0, -0.0, 3);
    }

    [Test]
    public void EqualDoublePrecision_DifferenceEqualsTolerance_Passes()
    {
        // Precision 0 => tolerance exactly 1.0, so the boundary is exactly representable.
        Assert.Equal(1.0, 2.0, 0);
    }

    [Test]
    public void EqualDoublePrecision_OutsideTolerance_ThrowsWithToleranceMessage()
    {
        var ex = Assert.Throws<AssertionFailedException>(() => Assert.Equal(1.0, 2.5, 0));
        Assert.Equal(
            $"Expected: {1.0} (\u00b1{1.0}); Actual: {2.5}; Difference: {1.5}",
            ex.Message);
    }

    [Test]
    public void EqualDoublePrecision_BeyondLookupTable_UsesMathPowTolerance()
    {
        var ex = Assert.Throws<AssertionFailedException>(() => Assert.Equal(1.0, 2.0, 16));
        Assert.Equal(
            $"Expected: {1.0} (\u00b1{Math.Pow(10, -16)}); Actual: {2.0}; Difference: {1.0}",
            ex.Message);
    }

    [Test]
    public void EqualDoublePrecision_NegativePrecision_ThrowsArgumentOutOfRangeForPrecision()
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => Assert.Equal(1.0, 1.0, -1));
        Assert.Equal("precision", ex.ParamName);
    }

    [Test]
    public void EqualDoublePrecision_CustomMessageReplacesToleranceMessage()
    {
        var ex = Assert.Throws<AssertionFailedException>(() => Assert.Equal(1.0, 2.5, 0, "custom"));
        Assert.Equal("custom", ex.Message);
    }

    [Test]
    public void EqualDoublePrecision_CustomMessageReplacesPlainMessage()
    {
        var ex = Assert.Throws<AssertionFailedException>(
            () => Assert.Equal(double.NaN, 1.0, 3, "custom"));
        Assert.Equal("custom", ex.Message);
    }

    // Assert.Equal(double, double, double tolerance)

    [Test]
    public void EqualDoubleTolerance_NaNVersusNaN_Passes()
    {
        Assert.Equal(double.NaN, double.NaN, 0.5);
    }

    [Test]
    public void EqualDoubleTolerance_NaNVersusNumber_ThrowsWithNaNDifference()
    {
        var ex = Assert.Throws<AssertionFailedException>(() => Assert.Equal(double.NaN, 1.0, 0.5));
        Assert.Equal(
            $"Expected: {double.NaN} (\u00b1{0.5}); Actual: {1.0}; Difference: {double.NaN}",
            ex.Message);
    }

    [Test]
    public void EqualDoubleTolerance_PositiveInfinityPair_Passes()
    {
        Assert.Equal(double.PositiveInfinity, double.PositiveInfinity, 0.5);
    }

    [Test]
    public void EqualDoubleTolerance_NegativeInfinityPair_Passes()
    {
        Assert.Equal(double.NegativeInfinity, double.NegativeInfinity, 0.5);
    }

    [Test]
    public void EqualDoubleTolerance_OppositeInfinities_ThrowsWithInfiniteDifference()
    {
        var ex = Assert.Throws<AssertionFailedException>(
            () => Assert.Equal(double.PositiveInfinity, double.NegativeInfinity, 0.5));
        Assert.Equal(
            $"Expected: {double.PositiveInfinity} (\u00b1{0.5}); Actual: {double.NegativeInfinity}; "
            + $"Difference: {double.PositiveInfinity}",
            ex.Message);
    }

    [Test]
    public void EqualDoubleTolerance_SignedZeroWithZeroTolerance_Passes()
    {
        Assert.Equal(0.0, -0.0, 0.0);
    }

    [Test]
    public void EqualDoubleTolerance_DifferenceEqualsTolerance_Passes()
    {
        Assert.Equal(1.0, 1.5, 0.5);
    }

    [Test]
    public void EqualDoubleTolerance_OutsideTolerance_ThrowsWithToleranceMessage()
    {
        var ex = Assert.Throws<AssertionFailedException>(() => Assert.Equal(1.0, 2.0, 0.5));
        Assert.Equal(
            $"Expected: {1.0} (\u00b1{0.5}); Actual: {2.0}; Difference: {1.0}",
            ex.Message);
    }

    [Test]
    public void EqualDoubleTolerance_NegativeToleranceWithEqualValues_ValidatesBeforeComparing()
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => Assert.Equal(1.0, 1.0, -0.5));
        Assert.Equal("tolerance", ex.ParamName);
    }

    [Test]
    public void EqualDoubleTolerance_NaNToleranceWithEqualValues_ValidatesBeforeComparing()
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(
            () => Assert.Equal(1.0, 1.0, double.NaN));
        Assert.Equal("tolerance", ex.ParamName);
    }

    [Test]
    public void EqualDoubleTolerance_PositiveNaNTolerance_ReportsToleranceMustBeANumber()
    {
        // double.NaN has its sign bit set, so ThrowIfNegative rejects it first; only a
        // NaN with a clear sign bit reaches the explicit IsNaN guard.
        var ex = Assert.Throws<ArgumentOutOfRangeException>(
            () => Assert.Equal(1.0, 1.0, PositiveNaN));
        Assert.Equal("tolerance", ex.ParamName);
        Assert.Contains("Tolerance must be a number.", ex.Message);
    }

    // Assert.Equal(decimal, decimal, int precision)

    [Test]
    public void EqualDecimalPrecision_DifferenceEqualsTolerance_Passes()
    {
        Assert.Equal(1.00m, 1.01m, 2);
    }

    [Test]
    public void EqualDecimalPrecision_SignedZero_Passes()
    {
        Assert.Equal(0.0m, -0.0m, 2);
    }

    [Test]
    public void EqualDecimalPrecision_OutsideTolerance_ThrowsWithToleranceMessage()
    {
        var ex = Assert.Throws<AssertionFailedException>(() => Assert.Equal(1.0m, 1.5m, 2));
        Assert.Equal(
            $"Expected: {1.0m} (\u00b1{0.01m}); Actual: {1.5m}; Difference: {0.5m}",
            ex.Message);
    }

    [Test]
    public void EqualDecimalPrecision_BeyondLookupTable_UsesRepeatedDivisionTolerance()
    {
        var ex = Assert.Throws<AssertionFailedException>(() => Assert.Equal(1m, 2m, 28));
        Assert.Equal(
            $"Expected: {1m} (\u00b1{0.0000000000000000000000000001m}); Actual: {2m}; "
            + $"Difference: {1m}",
            ex.Message);
    }

    [Test]
    public void EqualDecimalPrecision_NegativePrecision_ThrowsArgumentOutOfRangeForPrecision()
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => Assert.Equal(1.0m, 1.0m, -1));
        Assert.Equal("precision", ex.ParamName);
    }

    [Test]
    public void EqualDecimalPrecision_CustomMessageReplacesToleranceMessage()
    {
        var ex = Assert.Throws<AssertionFailedException>(
            () => Assert.Equal(1.0m, 1.5m, 2, "custom"));
        Assert.Equal("custom", ex.Message);
    }

    // Assert.NotEqual(double, double, int precision)

    [Test]
    public void NotEqualDoublePrecision_NaNVersusNaN_ThrowsWithPlainMessage()
    {
        var ex = Assert.Throws<AssertionFailedException>(
            () => Assert.NotEqual(double.NaN, double.NaN, 3));
        Assert.Equal($"Did not expect: {double.NaN}", ex.Message);
    }

    [Test]
    public void NotEqualDoublePrecision_NaNVersusNumber_Passes()
    {
        Assert.NotEqual(double.NaN, 1.0, 3);
    }

    [Test]
    public void NotEqualDoublePrecision_PositiveInfinityPair_ThrowsWithPlainMessage()
    {
        var ex = Assert.Throws<AssertionFailedException>(
            () => Assert.NotEqual(double.PositiveInfinity, double.PositiveInfinity, 3));
        Assert.Equal($"Did not expect: {double.PositiveInfinity}", ex.Message);
    }

    [Test]
    public void NotEqualDoublePrecision_OppositeInfinities_Passes()
    {
        Assert.NotEqual(double.PositiveInfinity, double.NegativeInfinity, 3);
    }

    [Test]
    public void NotEqualDoublePrecision_SignedZero_ThrowsWithToleranceMessage()
    {
        var ex = Assert.Throws<AssertionFailedException>(() => Assert.NotEqual(0.0, -0.0, 3));
        Assert.Equal(
            $"Did not expect: {-0.0} (within \u00b1{0.001} of {0.0})",
            ex.Message);
    }

    [Test]
    public void NotEqualDoublePrecision_DifferenceEqualsTolerance_ThrowsWithToleranceMessage()
    {
        var ex = Assert.Throws<AssertionFailedException>(() => Assert.NotEqual(1.0, 2.0, 0));
        Assert.Equal($"Did not expect: {2.0} (within \u00b1{1.0} of {1.0})", ex.Message);
    }

    [Test]
    public void NotEqualDoublePrecision_OutsideTolerance_Passes()
    {
        Assert.NotEqual(1.0, 2.5, 0);
    }

    [Test]
    public void NotEqualDoublePrecision_BeyondLookupTable_UsesMathPowTolerance()
    {
        var ex = Assert.Throws<AssertionFailedException>(() => Assert.NotEqual(1.0, 1.0, 16));
        Assert.Equal(
            $"Did not expect: {1.0} (within \u00b1{Math.Pow(10, -16)} of {1.0})",
            ex.Message);
    }

    [Test]
    public void NotEqualDoublePrecision_NegativePrecision_ThrowsArgumentOutOfRangeForPrecision()
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => Assert.NotEqual(1.0, 2.0, -1));
        Assert.Equal("precision", ex.ParamName);
    }

    [Test]
    public void NotEqualDoublePrecision_CustomMessageReplacesPlainMessage()
    {
        var ex = Assert.Throws<AssertionFailedException>(
            () => Assert.NotEqual(double.NaN, double.NaN, 3, "custom"));
        Assert.Equal("custom", ex.Message);
    }

    // Assert.NotEqual(double, double, double tolerance)

    [Test]
    public void NotEqualDoubleTolerance_NaNVersusNaN_ThrowsWithToleranceMessage()
    {
        var ex = Assert.Throws<AssertionFailedException>(
            () => Assert.NotEqual(double.NaN, double.NaN, 0.5));
        Assert.Equal(
            $"Did not expect: {double.NaN} (within \u00b1{0.5} of {double.NaN})",
            ex.Message);
    }

    [Test]
    public void NotEqualDoubleTolerance_NaNVersusNumber_Passes()
    {
        Assert.NotEqual(double.NaN, 1.0, 0.5);
    }

    [Test]
    public void NotEqualDoubleTolerance_PositiveInfinityPair_ThrowsWithToleranceMessage()
    {
        var ex = Assert.Throws<AssertionFailedException>(
            () => Assert.NotEqual(double.PositiveInfinity, double.PositiveInfinity, 0.5));
        Assert.Equal(
            $"Did not expect: {double.PositiveInfinity} (within \u00b1{0.5} of "
            + $"{double.PositiveInfinity})",
            ex.Message);
    }

    [Test]
    public void NotEqualDoubleTolerance_OppositeInfinities_Passes()
    {
        Assert.NotEqual(double.PositiveInfinity, double.NegativeInfinity, 0.5);
    }

    [Test]
    public void NotEqualDoubleTolerance_SignedZero_ThrowsWithToleranceMessage()
    {
        var ex = Assert.Throws<AssertionFailedException>(() => Assert.NotEqual(0.0, -0.0, 0.5));
        Assert.Equal($"Did not expect: {-0.0} (within \u00b1{0.5} of {0.0})", ex.Message);
    }

    [Test]
    public void NotEqualDoubleTolerance_DifferenceEqualsTolerance_ThrowsWithToleranceMessage()
    {
        var ex = Assert.Throws<AssertionFailedException>(() => Assert.NotEqual(1.0, 1.5, 0.5));
        Assert.Equal($"Did not expect: {1.5} (within \u00b1{0.5} of {1.0})", ex.Message);
    }

    [Test]
    public void NotEqualDoubleTolerance_OutsideTolerance_Passes()
    {
        Assert.NotEqual(1.0, 2.0, 0.5);
    }

    [Test]
    public void NotEqualDoubleTolerance_NegativeToleranceWithUnequalValues_ValidatesBeforeComparing()
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => Assert.NotEqual(1.0, 2.0, -0.5));
        Assert.Equal("tolerance", ex.ParamName);
    }

    [Test]
    public void NotEqualDoubleTolerance_NaNToleranceWithUnequalValues_ValidatesBeforeComparing()
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(
            () => Assert.NotEqual(1.0, 2.0, double.NaN));
        Assert.Equal("tolerance", ex.ParamName);
    }

    [Test]
    public void NotEqualDoubleTolerance_PositiveNaNTolerance_ReportsToleranceMustBeANumber()
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(
            () => Assert.NotEqual(1.0, 2.0, PositiveNaN));
        Assert.Equal("tolerance", ex.ParamName);
        Assert.Contains("Tolerance must be a number.", ex.Message);
    }

    [Test]
    public void NotEqualDoubleTolerance_CustomMessageReplacesToleranceMessage()
    {
        var ex = Assert.Throws<AssertionFailedException>(
            () => Assert.NotEqual(1.0, 1.5, 0.5, "custom"));
        Assert.Equal("custom", ex.Message);
    }

    // Assert.NotEqual(decimal, decimal, int precision)

    [Test]
    public void NotEqualDecimalPrecision_DifferenceEqualsTolerance_ThrowsWithToleranceMessage()
    {
        var ex = Assert.Throws<AssertionFailedException>(() => Assert.NotEqual(1.00m, 1.01m, 2));
        Assert.Equal(
            $"Did not expect: {1.01m} (within \u00b1{0.01m} of {1.00m})",
            ex.Message);
    }

    [Test]
    public void NotEqualDecimalPrecision_OutsideTolerance_Passes()
    {
        Assert.NotEqual(1.0m, 1.5m, 2);
    }

    [Test]
    public void NotEqualDecimalPrecision_SignedZero_ThrowsWithToleranceMessage()
    {
        var ex = Assert.Throws<AssertionFailedException>(() => Assert.NotEqual(0.0m, -0.0m, 2));
        Assert.Equal(
            $"Did not expect: {-0.0m} (within \u00b1{0.01m} of {0.0m})",
            ex.Message);
    }

    [Test]
    public void NotEqualDecimalPrecision_BeyondLookupTable_UsesRepeatedDivisionTolerance()
    {
        var ex = Assert.Throws<AssertionFailedException>(() => Assert.NotEqual(1m, 1m, 28));
        Assert.Equal(
            $"Did not expect: {1m} (within \u00b1{0.0000000000000000000000000001m} of {1m})",
            ex.Message);
    }

    [Test]
    public void NotEqualDecimalPrecision_NegativePrecision_ThrowsArgumentOutOfRangeForPrecision()
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => Assert.NotEqual(1.0m, 1.5m, -1));
        Assert.Equal("precision", ex.ParamName);
    }

    [Test]
    public void NotEqualDecimalPrecision_CustomMessageReplacesToleranceMessage()
    {
        var ex = Assert.Throws<AssertionFailedException>(
            () => Assert.NotEqual(1.00m, 1.01m, 2, "custom"));
        Assert.Equal("custom", ex.Message);
    }

    // Assert.Equal<T> / Assert.NotEqual<T> exact equality (no tolerance)

    [Test]
    public void EqualGeneric_NaNVersusNaN_Passes()
    {
        Assert.Equal(double.NaN, double.NaN);
    }

    [Test]
    public void EqualGeneric_NaNVersusNumber_ThrowsWithPlainMessage()
    {
        var ex = Assert.Throws<AssertionFailedException>(() => Assert.Equal(double.NaN, 1.0));
        Assert.Equal($"Expected: {double.NaN}; Actual: {1.0}", ex.Message);
    }

    [Test]
    public void EqualGeneric_PositiveInfinityPair_Passes()
    {
        Assert.Equal(double.PositiveInfinity, double.PositiveInfinity);
    }

    [Test]
    public void EqualGeneric_OppositeInfinities_ThrowsWithPlainMessage()
    {
        var ex = Assert.Throws<AssertionFailedException>(
            () => Assert.Equal(double.PositiveInfinity, double.NegativeInfinity));
        Assert.Equal(
            $"Expected: {double.PositiveInfinity}; Actual: {double.NegativeInfinity}",
            ex.Message);
    }

    [Test]
    public void EqualGeneric_SignedZero_Passes()
    {
        Assert.Equal(0.0, -0.0);
    }

    [Test]
    public void EqualGeneric_NullableWithBothNull_Passes()
    {
        Assert.Equal<int?>(null, null);
    }

    [Test]
    public void EqualGeneric_NullableWithOneNull_ThrowsWithEmptyExpected()
    {
        var ex = Assert.Throws<AssertionFailedException>(() => Assert.Equal<int?>(null, 1));
        Assert.Equal($"Expected: ; Actual: {1}", ex.Message);
    }

    [Test]
    public void EqualGeneric_EqualValueTypeStructs_Passes()
    {
        Assert.Equal(new Cell(1), new Cell(1));
    }

    [Test]
    public void EqualGeneric_UnequalValueTypeStructs_ThrowsWithObjectDifference()
    {
        var ex = Assert.Throws<AssertionFailedException>(
            () => Assert.Equal(new Cell(1), new Cell(2)));
        Assert.Contains("Object assertion failed", ex.Message);
    }

    [Test]
    public void NotEqualGeneric_NaNVersusNaN_ThrowsWithPlainMessage()
    {
        var ex = Assert.Throws<AssertionFailedException>(
            () => Assert.NotEqual(double.NaN, double.NaN));
        Assert.Equal($"Did not expect: {double.NaN}", ex.Message);
    }

    [Test]
    public void NotEqualGeneric_NaNVersusNumber_Passes()
    {
        Assert.NotEqual(double.NaN, 1.0);
    }

    [Test]
    public void NotEqualGeneric_SignedZero_ThrowsWithPlainMessage()
    {
        var ex = Assert.Throws<AssertionFailedException>(() => Assert.NotEqual(0.0, -0.0));
        Assert.Equal($"Did not expect: {-0.0}", ex.Message);
    }

    [Test]
    public void NotEqualGeneric_NullableWithBothNull_Throws()
    {
        var ex = Assert.Throws<AssertionFailedException>(() => Assert.NotEqual<int?>(null, null));
        Assert.Equal("Did not expect: ", ex.Message);
    }

    [Test]
    public void NotEqualGeneric_EqualValueTypeStructs_Throws()
    {
        var ex = Assert.Throws<AssertionFailedException>(
            () => Assert.NotEqual(new Cell(1), new Cell(1)));
        Assert.Equal($"Did not expect: {new Cell(1)}", ex.Message);
    }

    [Test]
    public void NotEqualGeneric_UnequalValueTypeStructs_Passes()
    {
        Assert.NotEqual(new Cell(1), new Cell(2));
    }

    // The generic overloads compare through EqualityComparer<T>.Default, which prefers
    // IEquatable<T>.Equals over object.Equals. .NET requires both to agree, so this is only
    // observable for types that break that contract, as EquatableOnly deliberately does.

    [Test]
    public void EqualGeneric_EquatableOnlyType_UsesIEquatable()
    {
        Assert.Equal(new EquatableOnly(1), new EquatableOnly(1));
    }

    [Test]
    public void NotEqualGeneric_EquatableOnlyType_UsesIEquatable()
    {
        Assert.Throws<AssertionFailedException>(
            () => Assert.NotEqual(new EquatableOnly(1), new EquatableOnly(1)));
        Assert.NotEqual(new EquatableOnly(1), new EquatableOnly(2));
    }

    // Extreme tolerance and precision paths: infinite and negative-zero tolerance, differences
    // that overflow to infinity, and precisions large enough for the tolerance to reach zero.

    [Test]
    public void EqualDoubleTolerance_InfiniteTolerance_AcceptsOppositeInfinities()
    {
        Assert.Equal(double.PositiveInfinity, double.NegativeInfinity, double.PositiveInfinity);
    }

    [Test]
    public void EqualDoubleTolerance_InfiniteTolerance_AcceptsOverflowingDifference()
    {
        Assert.Equal(double.MaxValue, -double.MaxValue, double.PositiveInfinity);
    }

    [Test]
    public void EqualDoubleTolerance_InfiniteTolerance_StillRejectsNaNVersusNumber()
    {
        var ex = Assert.Throws<AssertionFailedException>(
            () => Assert.Equal(double.NaN, 1.0, double.PositiveInfinity));
        Assert.Equal(
            $"Expected: {double.NaN} (\u00b1{double.PositiveInfinity}); Actual: {1.0}; "
            + $"Difference: {double.NaN}",
            ex.Message);
    }

    [Test]
    public void EqualDoubleTolerance_NegativeZeroTolerance_ThrowsArgumentOutOfRange()
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => Assert.Equal(1.0, 1.0, -0.0));
        Assert.Equal("tolerance", ex.ParamName);
    }

    [Test]
    public void NotEqualDoubleTolerance_NegativeZeroTolerance_ThrowsArgumentOutOfRange()
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => Assert.NotEqual(1.0, 2.0, -0.0));
        Assert.Equal("tolerance", ex.ParamName);
    }

    [Test]
    public void EqualDoublePrecision_OverflowingDifference_ReportsInfiniteDifference()
    {
        var ex = Assert.Throws<AssertionFailedException>(
            () => Assert.Equal(double.MaxValue, -double.MaxValue, 3));
        Assert.Equal(
            $"Expected: {double.MaxValue} (\u00b1{0.001}); Actual: {-double.MaxValue}; "
            + $"Difference: {double.PositiveInfinity}",
            ex.Message);
    }

    [Test]
    public void EqualDoublePrecision_ToleranceUnderflowsToZero_AcceptsOnlyExactValues()
    {
        Assert.Equal(1.0, 1.0, 400);

        const double actual = 1.0000000000000002;
        var ex = Assert.Throws<AssertionFailedException>(() => Assert.Equal(1.0, actual, 400));
        Assert.Equal(
            $"Expected: {1.0} (\u00b1{Math.Pow(10, -400)}); Actual: {actual}; "
            + $"Difference: {Math.Abs(1.0 - actual)}",
            ex.Message);
    }

    [Test]
    public void NotEqualDoublePrecision_ToleranceUnderflowsToZero_RejectsExactValues()
    {
        Assert.NotEqual(1.0, 1.0000000000000002, 400);

        var ex = Assert.Throws<AssertionFailedException>(() => Assert.NotEqual(1.0, 1.0, 400));
        Assert.Equal(
            $"Did not expect: {1.0} (within \u00b1{Math.Pow(10, -400)} of {1.0})",
            ex.Message);
    }

    [Test]
    public void EqualDecimalPrecision_ToleranceRoundsToZero_AcceptsOnlyExactValues()
    {
        Assert.Equal(1m, 1m, 29);

        var ex = Assert.Throws<AssertionFailedException>(() => Assert.Equal(1m, 2m, 29));
        // Dividing past decimal's scale limit yields plain zero, not a scaled zero.
        Assert.Equal($"Expected: {1m} (\u00b1{0m}); Actual: {2m}; Difference: {1m}", ex.Message);
    }

    [Test]
    public void EqualDecimalPrecision_OverflowingDifference_PropagatesOverflowException()
    {
        Assert.Throws<OverflowException>(() => Assert.Equal(decimal.MaxValue, decimal.MinValue, 2));
    }

    [Test]
    public void EqualDecimalPrecision_NegativePrecisionWithOverflowingValues_ValidatesFirst()
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(
            () => Assert.Equal(decimal.MaxValue, decimal.MinValue, -1));
        Assert.Equal("precision", ex.ParamName);
    }

    [Test]
    public void NotEqualDecimalPrecision_OverflowingDifference_PropagatesOverflowException()
    {
        Assert.Throws<OverflowException>(
            () => Assert.NotEqual(decimal.MaxValue, decimal.MinValue, 2));
    }

    [Test]
    public void Messages_AreFormattedWithTheCurrentCulture()
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");

            var doubleFailure = Assert.Throws<AssertionFailedException>(
                () => Assert.Equal(1.0, 2.0, 0.5));
            var decimalFailure = Assert.Throws<AssertionFailedException>(
                () => Assert.NotEqual(1.00m, 1.01m, 2));

            // Proves the culture switch reached the product formatting, so the derived
            // expectations below are not vacuously true.
            Assert.Contains("0,5", doubleFailure.Message);
            Assert.Contains("0,01", decimalFailure.Message);

            Assert.Equal(
                $"Expected: {1.0} (\u00b1{0.5}); Actual: {2.0}; Difference: {1.0}",
                doubleFailure.Message);
            Assert.Equal(
                $"Did not expect: {1.01m} (within \u00b1{0.01m} of {1.00m})",
                decimalFailure.Message);
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }
}
