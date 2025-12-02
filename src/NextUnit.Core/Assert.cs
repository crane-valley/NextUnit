namespace NextUnit;

/// <summary>
/// Provides assertion methods for verifying test conditions.
/// </summary>
public static class Assert
{
    /// <summary>
    /// Verifies that a condition is true.
    /// </summary>
    /// <param name="condition">The condition to verify.</param>
    /// <param name="message">Optional custom message to display if the assertion fails.</param>
    /// <exception cref="AssertionFailedException">Thrown when the condition is false.</exception>
    public static void True(bool condition, string? message = null)
    {
        if (!condition)
            throw new AssertionFailedException(message ?? "Expected true but was false.");
    }

    /// <summary>
    /// Verifies that a condition is false.
    /// </summary>
    /// <param name="condition">The condition to verify.</param>
    /// <param name="message">Optional custom message to display if the assertion fails.</param>
    /// <exception cref="AssertionFailedException">Thrown when the condition is true.</exception>
    public static void False(bool condition, string? message = null)
    {
        if (condition)
            throw new AssertionFailedException(message ?? "Expected false but was true.");
    }

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
        if (!Equals(expected, actual))
            throw new AssertionFailedException(
                message ?? $"Expected: {expected}; Actual: {actual}");
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
            throw new AssertionFailedException(
                message ?? $"Did not expect: {actual}");
    }

    /// <summary>
    /// Verifies that a value is null.
    /// </summary>
    /// <param name="value">The value to verify.</param>
    /// <param name="message">Optional custom message to display if the assertion fails.</param>
    /// <exception cref="AssertionFailedException">Thrown when the value is not null.</exception>
    public static void Null(object? value, string? message = null)
    {
        if (value is not null)
            throw new AssertionFailedException(message ?? "Expected null.");
    }

    /// <summary>
    /// Verifies that a value is not null.
    /// </summary>
    /// <param name="value">The value to verify.</param>
    /// <param name="message">Optional custom message to display if the assertion fails.</param>
    /// <exception cref="AssertionFailedException">Thrown when the value is null.</exception>
    public static void NotNull(object? value, string? message = null)
    {
        if (value is null)
            throw new AssertionFailedException(message ?? "Expected non-null.");
    }

    /// <summary>
    /// Verifies that an action throws a specific type of exception.
    /// </summary>
    /// <typeparam name="TException">The expected exception type.</typeparam>
    /// <param name="action">The action to execute.</param>
    /// <param name="message">Optional custom message to display if the assertion fails.</param>
    /// <returns>The exception that was thrown.</returns>
    /// <exception cref="AssertionFailedException">Thrown when no exception is thrown or a different exception type is thrown.</exception>
    public static TException Throws<TException>(Action action, string? message = null)
        where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException ex)
        {
            return ex;
        }
        catch (Exception ex)
        {
            throw new AssertionFailedException(
                message ?? $"Expected {typeof(TException).Name} but got {ex.GetType().Name}.",
                ex);
        }

        throw new AssertionFailedException(
            message ?? $"Expected {typeof(TException).Name} but no exception was thrown.");
    }

    /// <summary>
    /// Verifies that an asynchronous action throws a specific type of exception.
    /// </summary>
    /// <typeparam name="TException">The expected exception type.</typeparam>
    /// <param name="action">The asynchronous action to execute.</param>
    /// <param name="message">Optional custom message to display if the assertion fails.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the exception that was thrown.</returns>
    /// <exception cref="AssertionFailedException">Thrown when no exception is thrown or a different exception type is thrown.</exception>
    public static async Task<TException> ThrowsAsync<TException>(
        Func<Task> action,
        string? message = null)
        where TException : Exception
    {
        try
        {
            await action().ConfigureAwait(false);
        }
        catch (TException ex)
        {
            return ex;
        }
        catch (Exception ex)
        {
            throw new AssertionFailedException(
                message ?? $"Expected {typeof(TException).Name} but got {ex.GetType().Name}.",
                ex);
        }

        throw new AssertionFailedException(
            message ?? $"Expected {typeof(TException).Name} but no exception was thrown.");
    }
}

/// <summary>
/// Represents an exception that is thrown when an assertion fails during test execution.
/// </summary>
public sealed class AssertionFailedException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AssertionFailedException"/> class.
    /// </summary>
    public AssertionFailedException() : base() { }

    /// <summary>
    /// Initializes a new instance of the <see cref="AssertionFailedException"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the assertion failure.</param>
    public AssertionFailedException(string message) : base(message) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="AssertionFailedException"/> class with a specified error message and inner exception.
    /// </summary>
    /// <param name="message">The message that describes the assertion failure.</param>
    /// <param name="inner">The exception that is the cause of the current exception.</param>
    public AssertionFailedException(string message, Exception inner) : base(message, inner) { }
}
