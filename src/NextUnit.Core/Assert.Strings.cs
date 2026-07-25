namespace NextUnit;

public static partial class Assert
{
    /// <summary>
    /// Verifies that a string starts with a specified substring.
    /// </summary>
    /// <param name="expectedStart">The expected start of the string.</param>
    /// <param name="actual">The actual string.</param>
    /// <param name="message">Optional custom message to display if the assertion fails.</param>
    /// <exception cref="AssertionFailedException">Thrown when the string does not start with the expected substring.</exception>
    public static void StartsWith(string expectedStart, string? actual, string? message = null)
    {
        ArgumentNullException.ThrowIfNull(expectedStart);

        if (actual is null || !actual.StartsWith(expectedStart, StringComparison.Ordinal))
        {
            throw new AssertionFailedException(
                message ?? $"String does not start with expected value.\nExpected start: \"{expectedStart}\"\nActual: \"{actual}\"");
        }
    }

    /// <summary>
    /// Verifies that a string ends with a specified substring.
    /// </summary>
    /// <param name="expectedEnd">The expected end of the string.</param>
    /// <param name="actual">The actual string.</param>
    /// <param name="message">Optional custom message to display if the assertion fails.</param>
    /// <exception cref="AssertionFailedException">Thrown when the string does not end with the expected substring.</exception>
    public static void EndsWith(string expectedEnd, string? actual, string? message = null)
    {
        ArgumentNullException.ThrowIfNull(expectedEnd);

        if (actual is null || !actual.EndsWith(expectedEnd, StringComparison.Ordinal))
        {
            throw new AssertionFailedException(
                message ?? $"String does not end with expected value.\nExpected end: \"{expectedEnd}\"\nActual: \"{actual}\"");
        }
    }

    /// <summary>
    /// Verifies that a string contains a specified substring.
    /// </summary>
    /// <param name="expectedSubstring">The substring expected to be in the string.</param>
    /// <param name="actual">The actual string.</param>
    /// <param name="message">Optional custom message to display if the assertion fails.</param>
    /// <exception cref="AssertionFailedException">Thrown when the string does not contain the expected substring.</exception>
    public static void Contains(string expectedSubstring, string? actual, string? message = null)
    {
        ArgumentNullException.ThrowIfNull(expectedSubstring);

        if (actual is null || !actual.Contains(expectedSubstring, StringComparison.Ordinal))
        {
            throw new AssertionFailedException(
                message ?? $"String does not contain expected substring.\nExpected substring: \"{expectedSubstring}\"\nActual: \"{actual}\"");
        }
    }
}
