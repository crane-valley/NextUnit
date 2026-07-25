namespace NextUnit;

public static partial class Assert
{
    /// <summary>
    /// Verifies that a value is within a specified range (inclusive).
    /// </summary>
    /// <typeparam name="T">The type of the value to verify.</typeparam>
    /// <param name="actual">The value to verify.</param>
    /// <param name="min">The minimum value (inclusive).</param>
    /// <param name="max">The maximum value (inclusive).</param>
    /// <param name="message">Optional custom message to display if the assertion fails.</param>
    /// <exception cref="AssertionFailedException">Thrown when the value is not within the specified range.</exception>
    public static void InRange<T>(T actual, T min, T max, string? message = null)
        where T : IComparable<T>
    {
        if (actual.CompareTo(min) < 0 || actual.CompareTo(max) > 0)
        {
            throw new AssertionFailedException(
                message ?? $"Value {actual} is not in range [{min}, {max}].");
        }
    }

    /// <summary>
    /// Verifies that a value is outside a specified range whose bounds are inclusive.
    /// A value equal to <paramref name="min"/> or <paramref name="max"/> is inside the range and fails the assertion.
    /// </summary>
    /// <typeparam name="T">The type of the value to verify.</typeparam>
    /// <param name="actual">The value to verify.</param>
    /// <param name="min">The minimum value (inclusive).</param>
    /// <param name="max">The maximum value (inclusive).</param>
    /// <param name="message">Optional custom message to display if the assertion fails.</param>
    /// <exception cref="AssertionFailedException">Thrown when the value is within the specified inclusive range.</exception>
    public static void NotInRange<T>(T actual, T min, T max, string? message = null)
        where T : IComparable<T>
    {
        if (actual.CompareTo(min) >= 0 && actual.CompareTo(max) <= 0)
        {
            throw new AssertionFailedException(
                message ?? $"Value {actual} is in range [{min}, {max}] but should not be.");
        }
    }
}
