using System.Collections;
using System.Runtime.CompilerServices;

namespace NextUnit.Internal;

/// <summary>
/// Bridges a generated asynchronous <c>[TestData]</c> member to the untyped row sequence the
/// expander consumes.
/// </summary>
/// <remarks>
/// These helpers exist because C# has no async iterator lambda, so the source generator cannot
/// inline the conversion into the registry. Every call site it emits binds the type argument
/// statically, which is what keeps the generated path free of runtime reflection and of runtime
/// generic instantiation, and therefore Native AOT safe.
/// </remarks>
public static class AsyncDataSourceAdapter
{
    /// <summary>
    /// Streams the rows of an <see cref="IAsyncEnumerable{T}"/> data source member.
    /// </summary>
    /// <typeparam name="TRow">The row type the member yields.</typeparam>
    /// <param name="source">The data source member's return value.</param>
    /// <param name="cancellationToken">The token that cancels enumeration.</param>
    /// <returns>The rows of <paramref name="source"/> as untyped values.</returns>
    public static async IAsyncEnumerable<object?> FromAsyncEnumerableAsync<TRow>(
        IAsyncEnumerable<TRow> source,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);

        await foreach (var row in source.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            // A user iterator is free to ignore the token it was handed, so the token is enforced
            // here as well: discovery has to stay interruptible whatever the source does with it.
            cancellationToken.ThrowIfCancellationRequested();
            yield return row;
        }
    }

    /// <summary>
    /// Streams the rows of a task-wrapped collection data source member.
    /// </summary>
    /// <typeparam name="TRows">The collection type the task produces.</typeparam>
    /// <param name="source">The data source member's return value.</param>
    /// <param name="cancellationToken">The token that cancels enumeration.</param>
    /// <returns>The rows of the awaited collection as untyped values.</returns>
    /// <remarks>
    /// A <c>ValueTask&lt;TRows&gt;</c> member reaches this method through <c>AsTask()</c> at the
    /// generated call site, so the awaited instance is always safe to hold until enumeration starts.
    /// </remarks>
    public static async IAsyncEnumerable<object?> FromTaskAsync<TRows>(
        Task<TRows> source,
        [EnumeratorCancellation] CancellationToken cancellationToken)
        where TRows : IEnumerable
    {
        ArgumentNullException.ThrowIfNull(source);

        var rows = await source.ConfigureAwait(false);
        if (rows is null)
        {
            throw new InvalidOperationException(
                "An asynchronous test data source completed with a null collection.");
        }

        foreach (var row in rows)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return row;
        }
    }
}
