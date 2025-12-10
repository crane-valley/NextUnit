using System.Collections;

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
        {
            throw new AssertionFailedException(message ?? "Expected true but was false.");
        }
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
        {
            throw new AssertionFailedException(message ?? "Expected false but was true.");
        }
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
        {
            throw new AssertionFailedException(
                message ?? $"Expected: {expected}; Actual: {actual}");
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
    /// Verifies that a value is null.
    /// </summary>
    /// <param name="value">The value to verify.</param>
    /// <param name="message">Optional custom message to display if the assertion fails.</param>
    /// <exception cref="AssertionFailedException">Thrown when the value is not null.</exception>
    public static void Null(object? value, string? message = null)
    {
        if (value is not null)
        {
            throw new AssertionFailedException(message ?? "Expected null.");
        }
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
        {
            throw new AssertionFailedException(message ?? "Expected non-null.");
        }
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

    // Collection Assertions

    /// <summary>
    /// Verifies that a collection contains a specific element.
    /// </summary>
    /// <typeparam name="T">The type of elements in the collection.</typeparam>
    /// <param name="expected">The element expected to be in the collection.</param>
    /// <param name="collection">The collection to search.</param>
    /// <param name="message">Optional custom message to display if the assertion fails.</param>
    /// <exception cref="AssertionFailedException">Thrown when the collection does not contain the expected element.</exception>
    public static void Contains<T>(T expected, IEnumerable<T> collection, string? message = null)
    {
        ArgumentNullException.ThrowIfNull(collection);

        if (!collection.Contains(expected))
        {
            throw new AssertionFailedException(
                message ?? $"Collection does not contain expected element: {expected}");
        }
    }

    /// <summary>
    /// Verifies that a collection contains an element matching a predicate.
    /// </summary>
    /// <typeparam name="T">The type of elements in the collection.</typeparam>
    /// <param name="collection">The collection to search.</param>
    /// <param name="predicate">The predicate to match elements against.</param>
    /// <param name="message">Optional custom message to display if the assertion fails.</param>
    /// <exception cref="AssertionFailedException">Thrown when the collection does not contain an element matching the predicate.</exception>
    public static void Contains<T>(IEnumerable<T> collection, Func<T, bool> predicate, string? message = null)
    {
        ArgumentNullException.ThrowIfNull(collection);
        ArgumentNullException.ThrowIfNull(predicate);

        if (!collection.Any(predicate))
        {
            throw new AssertionFailedException(
                message ?? "Collection does not contain an element matching the predicate.");
        }
    }

    /// <summary>
    /// Verifies that a collection does not contain a specific element.
    /// </summary>
    /// <typeparam name="T">The type of elements in the collection.</typeparam>
    /// <param name="notExpected">The element that should not be in the collection.</param>
    /// <param name="collection">The collection to search.</param>
    /// <param name="message">Optional custom message to display if the assertion fails.</param>
    /// <exception cref="AssertionFailedException">Thrown when the collection contains the element.</exception>
    public static void DoesNotContain<T>(T notExpected, IEnumerable<T> collection, string? message = null)
    {
        ArgumentNullException.ThrowIfNull(collection);

        if (collection.Contains(notExpected))
        {
            throw new AssertionFailedException(
                message ?? $"Collection should not contain element: {notExpected}");
        }
    }

    /// <summary>
    /// Verifies that a collection does not contain an element matching a predicate.
    /// </summary>
    /// <typeparam name="T">The type of elements in the collection.</typeparam>
    /// <param name="collection">The collection to search.</param>
    /// <param name="predicate">The predicate to match elements against.</param>
    /// <param name="message">Optional custom message to display if the assertion fails.</param>
    /// <exception cref="AssertionFailedException">Thrown when the collection contains an element matching the predicate.</exception>
    public static void DoesNotContain<T>(IEnumerable<T> collection, Func<T, bool> predicate, string? message = null)
    {
        ArgumentNullException.ThrowIfNull(collection);
        ArgumentNullException.ThrowIfNull(predicate);

        if (collection.Any(predicate))
        {
            throw new AssertionFailedException(
                message ?? "Collection should not contain an element matching the predicate.");
        }
    }

    /// <summary>
    /// Verifies that all elements in a collection satisfy a condition.
    /// </summary>
    /// <typeparam name="T">The type of elements in the collection.</typeparam>
    /// <param name="collection">The collection to verify.</param>
    /// <param name="action">The action to perform on each element.</param>
    /// <exception cref="AssertionFailedException">Thrown when any element fails the condition.</exception>
    public static void All<T>(IEnumerable<T> collection, Action<T> action)
    {
        ArgumentNullException.ThrowIfNull(collection);
        ArgumentNullException.ThrowIfNull(action);

        var index = 0;
        foreach (var item in collection)
        {
            try
            {
                action(item);
            }
            catch (AssertionFailedException ex)
            {
                throw new AssertionFailedException(
                    $"Assert.All failed at index {index}: {ex.Message}",
                    ex);
            }
            index++;
        }
    }

    /// <summary>
    /// Verifies that a collection contains exactly one element.
    /// </summary>
    /// <typeparam name="T">The type of elements in the collection.</typeparam>
    /// <param name="collection">The collection to verify.</param>
    /// <param name="message">Optional custom message to display if the assertion fails.</param>
    /// <returns>The single element in the collection.</returns>
    /// <exception cref="AssertionFailedException">Thrown when the collection does not contain exactly one element.</exception>
    public static T Single<T>(IEnumerable<T> collection, string? message = null)
    {
        ArgumentNullException.ThrowIfNull(collection);

        var list = collection as IList<T> ?? collection.ToList();
        if (list.Count == 0)
        {
            throw new AssertionFailedException(
                message ?? "Collection is empty. Expected exactly one element.");
        }

        if (list.Count > 1)
        {
            throw new AssertionFailedException(
                message ?? $"Collection contains {list.Count} elements. Expected exactly one element.");
        }

        return list[0];
    }

    /// <summary>
    /// Verifies that a collection contains exactly one element matching a predicate.
    /// </summary>
    /// <typeparam name="T">The type of elements in the collection.</typeparam>
    /// <param name="collection">The collection to search.</param>
    /// <param name="predicate">The predicate to match elements against.</param>
    /// <param name="message">Optional custom message to display if the assertion fails.</param>
    /// <returns>The single element matching the predicate.</returns>
    /// <exception cref="AssertionFailedException">Thrown when the collection does not contain exactly one element matching the predicate.</exception>
    public static T Single<T>(IEnumerable<T> collection, Func<T, bool> predicate, string? message = null)
    {
        ArgumentNullException.ThrowIfNull(collection);
        ArgumentNullException.ThrowIfNull(predicate);

        var matches = collection.Where(predicate).ToList();
        if (matches.Count == 0)
        {
            throw new AssertionFailedException(
                message ?? "Collection does not contain an element matching the predicate. Expected exactly one matching element.");
        }

        if (matches.Count > 1)
        {
            throw new AssertionFailedException(
                message ?? $"Collection contains {matches.Count} elements matching the predicate. Expected exactly one matching element.");
        }

        return matches[0];
    }

    /// <summary>
    /// Verifies that a collection is empty.
    /// </summary>
    /// <param name="collection">The collection to verify.</param>
    /// <param name="message">Optional custom message to display if the assertion fails.</param>
    /// <exception cref="AssertionFailedException">Thrown when the collection is not empty.</exception>
    public static void Empty(IEnumerable collection, string? message = null)
    {
        ArgumentNullException.ThrowIfNull(collection);

        var enumerator = collection.GetEnumerator();
        if (enumerator.MoveNext())
        {
            throw new AssertionFailedException(
                message ?? "Collection is not empty. Expected empty collection.");
        }
    }

    /// <summary>
    /// Verifies that a collection is not empty.
    /// </summary>
    /// <param name="collection">The collection to verify.</param>
    /// <param name="message">Optional custom message to display if the assertion fails.</param>
    /// <exception cref="AssertionFailedException">Thrown when the collection is empty.</exception>
    public static void NotEmpty(IEnumerable collection, string? message = null)
    {
        ArgumentNullException.ThrowIfNull(collection);

        var enumerator = collection.GetEnumerator();
        if (!enumerator.MoveNext())
        {
            throw new AssertionFailedException(
                message ?? "Collection is empty. Expected non-empty collection.");
        }
    }

    // String Assertions

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

    // Numeric Assertions

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
    /// Verifies that a value is not within a specified range (exclusive).
    /// </summary>
    /// <typeparam name="T">The type of the value to verify.</typeparam>
    /// <param name="actual">The value to verify.</param>
    /// <param name="min">The minimum value (inclusive).</param>
    /// <param name="max">The maximum value (inclusive).</param>
    /// <param name="message">Optional custom message to display if the assertion fails.</param>
    /// <exception cref="AssertionFailedException">Thrown when the value is within the specified range.</exception>
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
