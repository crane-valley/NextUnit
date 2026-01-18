using System.Collections;

namespace NextUnit;

/// <summary>
/// Provides assertion methods for verifying test conditions.
/// </summary>
public static class Assert
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

    /// <summary>
    /// Verifies that an action throws a specific type of exception with a message matching the expected message.
    /// </summary>
    /// <typeparam name="TException">The expected exception type.</typeparam>
    /// <param name="action">The action to execute.</param>
    /// <param name="expectedMessage">The expected exception message.</param>
    /// <param name="message">Optional custom message to display if the assertion fails.</param>
    /// <returns>The exception that was thrown.</returns>
    /// <exception cref="AssertionFailedException">Thrown when no exception is thrown, a different exception type is thrown, or the message doesn't match.</exception>
    public static TException Throws<TException>(Action action, string expectedMessage, string? message = null)
        where TException : Exception
    {
        ArgumentNullException.ThrowIfNull(expectedMessage);

        try
        {
            action();
        }
        catch (TException ex)
        {
            if (ex.Message != expectedMessage)
            {
                throw new AssertionFailedException(
                    message ?? $"Expected exception message: \"{expectedMessage}\"\nActual exception message: \"{ex.Message}\"",
                    ex);
            }
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
    /// Verifies that an asynchronous action throws a specific type of exception with a message matching the expected message.
    /// </summary>
    /// <typeparam name="TException">The expected exception type.</typeparam>
    /// <param name="action">The asynchronous action to execute.</param>
    /// <param name="expectedMessage">The expected exception message.</param>
    /// <param name="message">Optional custom message to display if the assertion fails.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the exception that was thrown.</returns>
    /// <exception cref="AssertionFailedException">Thrown when no exception is thrown, a different exception type is thrown, or the message doesn't match.</exception>
    public static async Task<TException> ThrowsAsync<TException>(
        Func<Task> action,
        string expectedMessage,
        string? message = null)
        where TException : Exception
    {
        ArgumentNullException.ThrowIfNull(expectedMessage);

        try
        {
            await action().ConfigureAwait(false);
        }
        catch (TException ex)
        {
            if (ex.Message != expectedMessage)
            {
                throw new AssertionFailedException(
                    message ?? $"Expected exception message: \"{expectedMessage}\"\nActual exception message: \"{ex.Message}\"",
                    ex);
            }
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
    /// <param name="filter">The predicate to match elements against.</param>
    /// <param name="message">Optional custom message to display if the assertion fails.</param>
    /// <returns>The first element matching the predicate.</returns>
    /// <exception cref="AssertionFailedException">Thrown when the collection does not contain an element matching the predicate.</exception>
    public static T Contains<T>(IEnumerable<T> collection, Predicate<T> filter, string? message = null)
    {
        ArgumentNullException.ThrowIfNull(collection);
        ArgumentNullException.ThrowIfNull(filter);

        foreach (var item in collection)
        {
            if (filter(item))
            {
                return item;
            }
        }

        throw new AssertionFailedException(
            message ?? "Collection does not contain an element matching the predicate.");
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
    /// <param name="filter">The predicate to match elements against.</param>
    /// <param name="message">Optional custom message to display if the assertion fails.</param>
    /// <exception cref="AssertionFailedException">Thrown when the collection contains an element matching the predicate.</exception>
    public static void DoesNotContain<T>(IEnumerable<T> collection, Predicate<T> filter, string? message = null)
    {
        ArgumentNullException.ThrowIfNull(collection);
        ArgumentNullException.ThrowIfNull(filter);

        foreach (var item in collection)
        {
            if (filter(item))
            {
                throw new AssertionFailedException(
                    message ?? "Collection should not contain an element matching the predicate.");
            }
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
    /// <param name="filter">The predicate to match elements against.</param>
    /// <param name="message">Optional custom message to display if the assertion fails.</param>
    /// <returns>The single element matching the predicate.</returns>
    /// <exception cref="AssertionFailedException">Thrown when the collection does not contain exactly one element matching the predicate.</exception>
    public static T Single<T>(IEnumerable<T> collection, Predicate<T> filter, string? message = null)
    {
        ArgumentNullException.ThrowIfNull(collection);
        ArgumentNullException.ThrowIfNull(filter);

        T? matchingItem = default;
        var hasMatch = false;
        var multipleMatches = false;

        foreach (var item in collection)
        {
            if (filter(item))
            {
                if (hasMatch)
                {
                    multipleMatches = true;
                    break;
                }
                matchingItem = item;
                hasMatch = true;
            }
        }

        if (!hasMatch)
        {
            throw new AssertionFailedException(
                message ?? "Collection does not contain an element matching the predicate. Expected exactly one matching element.");
        }

        if (multipleMatches)
        {
            throw new AssertionFailedException(
                message ?? "Collection contains multiple elements matching the predicate. Expected exactly one matching element.");
        }

        // matchingItem is guaranteed to be non-null because hasMatch is true
        return matchingItem!;
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

    /// <summary>
    /// Verifies that two collections contain the same elements in any order.
    /// </summary>
    /// <typeparam name="T">The type of elements in the collections.</typeparam>
    /// <param name="expected">The expected collection.</param>
    /// <param name="actual">The actual collection.</param>
    /// <param name="message">Optional custom message to display if the assertion fails.</param>
    /// <exception cref="AssertionFailedException">Thrown when the collections are not equivalent.</exception>
    public static void Equivalent<T>(IEnumerable<T> expected, IEnumerable<T> actual, string? message = null)
        where T : notnull
    {
        ArgumentNullException.ThrowIfNull(expected);
        ArgumentNullException.ThrowIfNull(actual);

        var expectedList = expected.ToList();
        var actualList = actual.ToList();

        if (expectedList.Count != actualList.Count)
        {
            throw new AssertionFailedException(
                message ?? $"Collections have different counts. Expected: {expectedList.Count}; Actual: {actualList.Count}");
        }

        var expectedCounts = new Dictionary<T, int>(EqualityComparer<T>.Default);
        var actualCounts = new Dictionary<T, int>(EqualityComparer<T>.Default);

        foreach (var item in expectedList)
        {
            expectedCounts.TryGetValue(item, out var count);
            expectedCounts[item] = count + 1;
        }

        foreach (var item in actualList)
        {
            actualCounts.TryGetValue(item, out var count);
            actualCounts[item] = count + 1;
        }

        if (expectedCounts.Count != actualCounts.Count)
        {
            throw new AssertionFailedException(
                message ?? "Collections are not equivalent.");
        }

        foreach (var kvp in expectedCounts)
        {
            if (!actualCounts.TryGetValue(kvp.Key, out var actualCount) || actualCount != kvp.Value)
            {
                throw new AssertionFailedException(
                    message ?? "Collections are not equivalent.");
            }
        }
    }

    /// <summary>
    /// Verifies that all elements of a subset collection are present in a superset collection.
    /// </summary>
    /// <typeparam name="T">The type of elements in the collections.</typeparam>
    /// <param name="subset">The collection that should be a subset.</param>
    /// <param name="superset">The collection that should be a superset.</param>
    /// <param name="message">Optional custom message to display if the assertion fails.</param>
    /// <exception cref="AssertionFailedException">Thrown when the subset is not contained in the superset.</exception>
    public static void Subset<T>(IEnumerable<T> subset, IEnumerable<T> superset, string? message = null)
    {
        ArgumentNullException.ThrowIfNull(subset);
        ArgumentNullException.ThrowIfNull(superset);

        var supersetSet = new HashSet<T>(superset, EqualityComparer<T>.Default);

        foreach (var item in subset)
        {
            if (!supersetSet.Contains(item))
            {
                throw new AssertionFailedException(
                    message ?? $"Subset contains element '{item}' not found in superset.");
            }
        }
    }

    /// <summary>
    /// Verifies that two collections have no common elements.
    /// </summary>
    /// <typeparam name="T">The type of elements in the collections.</typeparam>
    /// <param name="collection1">The first collection.</param>
    /// <param name="collection2">The second collection.</param>
    /// <param name="message">Optional custom message to display if the assertion fails.</param>
    /// <exception cref="AssertionFailedException">Thrown when the collections have common elements.</exception>
    public static void Disjoint<T>(IEnumerable<T> collection1, IEnumerable<T> collection2, string? message = null)
    {
        ArgumentNullException.ThrowIfNull(collection1);
        ArgumentNullException.ThrowIfNull(collection2);

        var set1 = new HashSet<T>(collection1, EqualityComparer<T>.Default);

        foreach (var item in collection2)
        {
            if (set1.Contains(item))
            {
                throw new AssertionFailedException(
                    message ?? $"Collections have common element: {item}");
            }
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

    // Runtime Skip Assertions

    /// <summary>
    /// Skips the current test with a specified reason.
    /// </summary>
    /// <param name="reason">The reason for skipping the test.</param>
    /// <exception cref="TestSkippedException">Always thrown to indicate the test should be skipped.</exception>
    /// <remarks>
    /// Use this method to skip a test at runtime based on dynamic conditions.
    /// For compile-time skipping, use the <see cref="SkipAttribute"/> instead.
    /// </remarks>
    [System.Diagnostics.CodeAnalysis.DoesNotReturn]
    public static void Skip(string reason)
    {
        throw new TestSkippedException(reason);
    }

    /// <summary>
    /// Skips the current test if the specified condition is true.
    /// </summary>
    /// <param name="condition">If <c>true</c>, the test will be skipped.</param>
    /// <param name="reason">The reason for skipping the test.</param>
    /// <exception cref="TestSkippedException">Thrown when the condition is true.</exception>
    public static void SkipWhen(bool condition, string reason)
    {
        if (condition)
        {
            throw new TestSkippedException(reason);
        }
    }

    /// <summary>
    /// Skips the current test unless the specified condition is true.
    /// </summary>
    /// <param name="condition">If <c>false</c>, the test will be skipped.</param>
    /// <param name="reason">The reason for skipping the test.</param>
    /// <exception cref="TestSkippedException">Thrown when the condition is false.</exception>
    public static void SkipUnless(bool condition, string reason)
    {
        if (!condition)
        {
            throw new TestSkippedException(reason);
        }
    }

    /// <summary>
    /// Skips the current test when running on Windows.
    /// </summary>
    /// <param name="reason">The reason for skipping on Windows. If null, a default message is used.</param>
    /// <exception cref="TestSkippedException">Thrown when running on Windows.</exception>
    public static void SkipOnWindows(string? reason = null)
    {
        SkipWhen(
            System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(
                System.Runtime.InteropServices.OSPlatform.Windows),
            reason ?? "Test skipped on Windows.");
    }

    /// <summary>
    /// Skips the current test when running on Linux.
    /// </summary>
    /// <param name="reason">The reason for skipping on Linux. If null, a default message is used.</param>
    /// <exception cref="TestSkippedException">Thrown when running on Linux.</exception>
    public static void SkipOnLinux(string? reason = null)
    {
        SkipWhen(
            System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(
                System.Runtime.InteropServices.OSPlatform.Linux),
            reason ?? "Test skipped on Linux.");
    }

    /// <summary>
    /// Skips the current test when running on macOS.
    /// </summary>
    /// <param name="reason">The reason for skipping on macOS. If null, a default message is used.</param>
    /// <exception cref="TestSkippedException">Thrown when running on macOS.</exception>
    public static void SkipOnMacOS(string? reason = null)
    {
        SkipWhen(
            System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(
                System.Runtime.InteropServices.OSPlatform.OSX),
            reason ?? "Test skipped on macOS.");
    }

    /// <summary>
    /// Skips the current test when running on FreeBSD.
    /// </summary>
    /// <param name="reason">The reason for skipping on FreeBSD. If null, a default message is used.</param>
    /// <exception cref="TestSkippedException">Thrown when running on FreeBSD.</exception>
    public static void SkipOnFreeBSD(string? reason = null)
    {
        SkipWhen(
            System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(
                System.Runtime.InteropServices.OSPlatform.FreeBSD),
            reason ?? "Test skipped on FreeBSD.");
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

/// <summary>
/// Represents an exception that is thrown when a test is skipped during execution.
/// </summary>
/// <remarks>
/// This exception is thrown by <see cref="Assert.Skip"/> and related methods
/// to indicate that a test should be skipped at runtime rather than failing.
/// </remarks>
public sealed class TestSkippedException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TestSkippedException"/> class
    /// with a default message.
    /// </summary>
    public TestSkippedException() : base("Test was skipped.") { }

    /// <summary>
    /// Initializes a new instance of the <see cref="TestSkippedException"/> class
    /// with a specified reason for skipping the test.
    /// </summary>
    /// <param name="reason">The reason why the test is being skipped.</param>
    public TestSkippedException(string reason) : base(reason) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="TestSkippedException"/> class
    /// with a specified reason and inner exception.
    /// </summary>
    /// <param name="reason">The reason why the test is being skipped.</param>
    /// <param name="inner">The exception that is the cause of the current exception.</param>
    public TestSkippedException(string reason, Exception inner) : base(reason, inner) { }
}
