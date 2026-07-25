using System.Collections;

namespace NextUnit;

public static partial class Assert
{
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
}
