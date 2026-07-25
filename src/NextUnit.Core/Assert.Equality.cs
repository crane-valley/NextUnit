using System.Collections;

namespace NextUnit;

public static partial class Assert
{
    // Lookup table for common precision values to avoid Math.Pow overhead
    // _powersOfTen[n] = 10^(-n) for precision values 0 through 15
    private static readonly double[] _powersOfTen =
    [
        1.0,                // 10^0
        0.1,                // 10^-1
        0.01,               // 10^-2
        0.001,              // 10^-3
        0.0001,             // 10^-4
        0.00001,            // 10^-5
        0.000001,           // 10^-6
        0.0000001,          // 10^-7
        0.00000001,         // 10^-8
        0.000000001,        // 10^-9
        0.0000000001,       // 10^-10
        0.00000000001,      // 10^-11
        0.000000000001,     // 10^-12
        0.0000000000001,    // 10^-13
        0.00000000000001,   // 10^-14
        0.000000000000001   // 10^-15
    ];

    // _powersOfTenDecimal[n] = 10^(-n) for precision values 0 through 27
    private static readonly decimal[] _powersOfTenDecimal =
    [
        1.0m,                // 10^0
        0.1m,                // 10^-1
        0.01m,               // 10^-2
        0.001m,              // 10^-3
        0.0001m,             // 10^-4
        0.00001m,            // 10^-5
        0.000001m,           // 10^-6
        0.0000001m,          // 10^-7
        0.00000001m,         // 10^-8
        0.000000001m,        // 10^-9
        0.0000000001m,       // 10^-10
        0.00000000001m,      // 10^-11
        0.000000000001m,     // 10^-12
        0.0000000000001m,    // 10^-13
        0.00000000000001m,   // 10^-14
        0.000000000000001m,  // 10^-15
        0.0000000000000001m, // 10^-16
        0.00000000000000001m, // 10^-17
        0.000000000000000001m, // 10^-18
        0.0000000000000000001m, // 10^-19
        0.00000000000000000001m, // 10^-20
        0.000000000000000000001m, // 10^-21
        0.0000000000000000000001m, // 10^-22
        0.00000000000000000000001m, // 10^-23
        0.000000000000000000000001m, // 10^-24
        0.0000000000000000000000001m, // 10^-25
        0.00000000000000000000000001m, // 10^-26
        0.000000000000000000000000001m // 10^-27
    ];

    /// <summary>
    /// Verifies that two values are equal.
    /// </summary>
    /// <typeparam name="T">The type of values to compare.</typeparam>
    /// <param name="expected">The expected value.</param>
    /// <param name="actual">The actual value.</param>
    /// <param name="message">Optional custom message to display if the assertion fails.</param>
    /// <exception cref="AssertionFailedException">Thrown when the values are not equal.</exception>
    public static void Equal<T>(T expected, T actual, string? message = null)
    {
        // Handle strings first (before generic Equals check)
        if (expected is string expectedStr && actual is string actualStr)
        {
            if (expectedStr != actualStr)
            {
                var richMessage = Internal.AssertionMessageFormatter.FormatStringDifference(expectedStr, actualStr);
                throw new AssertionFailedException(message ?? richMessage);
            }
            return;
        }

        // Handle collections (but not strings) before generic Equals to avoid double enumeration
        if (expected is IEnumerable expectedEnum && actual is IEnumerable actualEnum
            && expected is not string && actual is not string)
        {
            if (!AreCollectionsEqual(expectedEnum, actualEnum))
            {
                var richMessage = Internal.AssertionMessageFormatter.FormatCollectionDifference(
                    expectedEnum.Cast<object>(), actualEnum.Cast<object>());
                throw new AssertionFailedException(message ?? richMessage);
            }
            return;
        }

        // For all other types, use standard equality check
        if (!Equals(expected, actual))
        {
            // For complex objects, use rich formatting
            if (expected != null && actual != null &&
                !expected.GetType().IsPrimitive && !actual.GetType().IsPrimitive &&
                expected.GetType() != typeof(decimal) && actual.GetType() != typeof(decimal))
            {
                var richMessage = Internal.AssertionMessageFormatter.FormatObjectDifference(expected, actual);
                throw new AssertionFailedException(message ?? richMessage);
            }

            throw new AssertionFailedException(
                message ?? $"Expected: {expected}; Actual: {actual}");
        }
    }

    private static bool AreCollectionsEqual(IEnumerable expected, IEnumerable actual)
    {
        var expectedList = expected.Cast<object>().ToList();
        var actualList = actual.Cast<object>().ToList();

        if (expectedList.Count != actualList.Count)
        {
            return false;
        }

        for (int i = 0; i < expectedList.Count; i++)
        {
            if (!Equals(expectedList[i], actualList[i]))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Verifies that two values are equal using a custom comparer.
    /// </summary>
    /// <typeparam name="T">The type of values to compare.</typeparam>
    /// <param name="expected">The expected value.</param>
    /// <param name="actual">The actual value.</param>
    /// <param name="comparer">The comparer to use for equality comparison.</param>
    /// <param name="message">Optional custom message to display if the assertion fails.</param>
    /// <exception cref="AssertionFailedException">Thrown when the values are not equal.</exception>
    public static void Equal<T>(T expected, T actual, IEqualityComparer<T> comparer, string? message = null)
    {
        ArgumentNullException.ThrowIfNull(comparer);

        if (!comparer.Equals(expected, actual))
        {
            throw new AssertionFailedException(
                message ?? $"Expected: {expected}; Actual: {actual}");
        }
    }

    /// <summary>
    /// Verifies that two double values are equal within a specified precision.
    /// </summary>
    /// <param name="expected">The expected value.</param>
    /// <param name="actual">The actual value.</param>
    /// <param name="precision">The number of decimal places to compare.</param>
    /// <param name="message">Optional custom message to display if the assertion fails.</param>
    /// <exception cref="AssertionFailedException">Thrown when the values are not equal within the specified precision.</exception>
    public static void Equal(double expected, double actual, int precision, string? message = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(precision);

        if (double.IsNaN(expected) || double.IsNaN(actual) || double.IsInfinity(expected) || double.IsInfinity(actual))
        {
            if (!Equals(expected, actual))
            {
                throw new AssertionFailedException(
                    message ?? $"Expected: {expected}; Actual: {actual}");
            }
            return;
        }

        var tolerance = precision < _powersOfTen.Length
            ? _powersOfTen[precision]
            : Math.Pow(10, -precision);
        var difference = Math.Abs(expected - actual);

        if (difference > tolerance)
        {
            throw new AssertionFailedException(
                message ?? $"Expected: {expected} (±{tolerance}); Actual: {actual}; Difference: {difference}");
        }
    }

    /// <summary>
    /// Verifies that two double values are equal within an absolute tolerance.
    /// </summary>
    /// <param name="expected">The expected value.</param>
    /// <param name="actual">The actual value.</param>
    /// <param name="tolerance">The maximum allowed absolute difference. Must be zero or positive.</param>
    /// <param name="message">Optional custom message to display if the assertion fails.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="tolerance"/> is negative or NaN.</exception>
    /// <exception cref="AssertionFailedException">Thrown when the values differ by more than the tolerance.</exception>
    /// <remarks>
    /// Following xUnit semantics, NaN is considered equal to NaN and each infinity is
    /// considered equal to itself, so those cases pass regardless of the tolerance.
    /// </remarks>
    public static void Equal(double expected, double actual, double tolerance, string? message = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(tolerance);
        if (double.IsNaN(tolerance))
        {
            throw new ArgumentOutOfRangeException(nameof(tolerance), "Tolerance must be a number.");
        }

        // double.Equals (IEquatable<double>, no boxing) treats NaN as equal to NaN and
        // each infinity as equal to itself, matching xUnit's tolerance-comparison behavior.
        if (expected.Equals(actual))
        {
            return;
        }

        // Negated <= (rather than >) so a NaN difference, e.g. abs(NaN - 1.0), fails.
        var difference = Math.Abs(expected - actual);
        if (!(difference <= tolerance))
        {
            throw new AssertionFailedException(
                message ?? $"Expected: {expected} (±{tolerance}); Actual: {actual}; Difference: {difference}");
        }
    }

    /// <summary>
    /// Verifies that two decimal values are equal within a specified precision.
    /// </summary>
    /// <param name="expected">The expected value.</param>
    /// <param name="actual">The actual value.</param>
    /// <param name="precision">The number of decimal places to compare.</param>
    /// <param name="message">Optional custom message to display if the assertion fails.</param>
    /// <exception cref="AssertionFailedException">Thrown when the values are not equal within the specified precision.</exception>
    public static void Equal(decimal expected, decimal actual, int precision, string? message = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(precision);

        decimal tolerance;
        if (precision < _powersOfTenDecimal.Length)
        {
            tolerance = _powersOfTenDecimal[precision];
        }
        else
        {
            // Use decimal arithmetic for very high precision values
            tolerance = 1m;
            for (int i = 0; i < precision; i++)
            {
                tolerance /= 10m;
            }
        }

        var difference = Math.Abs(expected - actual);

        if (difference > tolerance)
        {
            throw new AssertionFailedException(
                message ?? $"Expected: {expected} (±{tolerance}); Actual: {actual}; Difference: {difference}");
        }
    }

    /// <summary>
    /// Verifies that two values are not equal.
    /// </summary>
    /// <typeparam name="T">The type of values to compare.</typeparam>
    /// <param name="notExpected">The value that should not match the actual value.</param>
    /// <param name="actual">The actual value.</param>
    /// <param name="message">Optional custom message to display if the assertion fails.</param>
    /// <exception cref="AssertionFailedException">Thrown when the values are equal.</exception>
    public static void NotEqual<T>(T notExpected, T actual, string? message = null)
    {
        if (Equals(notExpected, actual))
        {
            throw new AssertionFailedException(
                message ?? $"Did not expect: {actual}");
        }
    }

    /// <summary>
    /// Verifies that two double values are not equal within a specified precision.
    /// </summary>
    /// <param name="notExpected">The value that should not match the actual value.</param>
    /// <param name="actual">The actual value.</param>
    /// <param name="precision">The number of decimal places to compare.</param>
    /// <param name="message">Optional custom message to display if the assertion fails.</param>
    /// <exception cref="AssertionFailedException">Thrown when the values are equal within the specified precision.</exception>
    public static void NotEqual(double notExpected, double actual, int precision, string? message = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(precision);

        if (double.IsNaN(notExpected) || double.IsNaN(actual) || double.IsInfinity(notExpected) || double.IsInfinity(actual))
        {
            if (Equals(notExpected, actual))
            {
                throw new AssertionFailedException(
                    message ?? $"Did not expect: {actual}");
            }
            return;
        }

        var tolerance = precision < _powersOfTen.Length
            ? _powersOfTen[precision]
            : Math.Pow(10, -precision);
        var difference = Math.Abs(notExpected - actual);

        if (difference <= tolerance)
        {
            throw new AssertionFailedException(
                message ?? $"Did not expect: {actual} (within ±{tolerance} of {notExpected})");
        }
    }

    /// <summary>
    /// Verifies that two double values are not equal within an absolute tolerance.
    /// </summary>
    /// <param name="notExpected">The value that should not match the actual value.</param>
    /// <param name="actual">The actual value.</param>
    /// <param name="tolerance">The maximum allowed absolute difference. Must be zero or positive.</param>
    /// <param name="message">Optional custom message to display if the assertion fails.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="tolerance"/> is negative or NaN.</exception>
    /// <exception cref="AssertionFailedException">Thrown when the values differ by no more than the tolerance.</exception>
    /// <remarks>
    /// Following xUnit semantics, NaN is considered equal to NaN and each infinity is
    /// considered equal to itself, so those cases are treated as equal and therefore fail.
    /// </remarks>
    public static void NotEqual(double notExpected, double actual, double tolerance, string? message = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(tolerance);
        if (double.IsNaN(tolerance))
        {
            throw new ArgumentOutOfRangeException(nameof(tolerance), "Tolerance must be a number.");
        }

        // double.Equals (IEquatable<double>, no boxing) treats NaN as equal to NaN and
        // each infinity as equal to itself, matching xUnit's tolerance-comparison behavior.
        if (notExpected.Equals(actual))
        {
            throw new AssertionFailedException(
                message ?? $"Did not expect: {actual} (within ±{tolerance} of {notExpected})");
        }

        var difference = Math.Abs(notExpected - actual);
        if (difference <= tolerance)
        {
            throw new AssertionFailedException(
                message ?? $"Did not expect: {actual} (within ±{tolerance} of {notExpected})");
        }
    }

    /// <summary>
    /// Verifies that two decimal values are not equal within a specified precision.
    /// </summary>
    /// <param name="notExpected">The value that should not match the actual value.</param>
    /// <param name="actual">The actual value.</param>
    /// <param name="precision">The number of decimal places to compare.</param>
    /// <param name="message">Optional custom message to display if the assertion fails.</param>
    /// <exception cref="AssertionFailedException">Thrown when the values are equal within the specified precision.</exception>
    public static void NotEqual(decimal notExpected, decimal actual, int precision, string? message = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(precision);

        decimal tolerance;
        if (precision < _powersOfTenDecimal.Length)
        {
            tolerance = _powersOfTenDecimal[precision];
        }
        else
        {
            // Use decimal arithmetic for very high precision values
            tolerance = 1m;
            for (int i = 0; i < precision; i++)
            {
                tolerance /= 10m;
            }
        }

        var difference = Math.Abs(notExpected - actual);

        if (difference <= tolerance)
        {
            throw new AssertionFailedException(
                message ?? $"Did not expect: {actual} (within ±{tolerance} of {notExpected})");
        }
    }
}
