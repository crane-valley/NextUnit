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
    /// <para>
    /// The awaited collection is read through the non-generic <see cref="IEnumerable"/>, so a source
    /// implementing <see cref="IEnumerable{T}"/> more than once yields the arm its type mapped that
    /// interface to. The overload taking a reader is what pins the arm instead; this one is emitted
    /// for a source that offers a single row type, where there is no arm to choose.
    /// </para>
    /// </remarks>
    public static IAsyncEnumerable<object?> FromTaskAsync<TRows>(
        Task<TRows> source,
        CancellationToken cancellationToken)
        where TRows : IEnumerable =>
        StreamAsync(source, static rows => rows, cancellationToken);

    /// <summary>
    /// Streams the rows of a task-wrapped collection data source member, read through the row type
    /// <paramref name="reader"/> names.
    /// </summary>
    /// <typeparam name="TRows">The collection type the task produces.</typeparam>
    /// <param name="source">The data source member's return value.</param>
    /// <param name="reader">
    /// Reads the awaited collection as one named element interface, or returns <see langword="null"/>
    /// for a collection that offered no rows to read.
    /// </param>
    /// <param name="cancellationToken">The token that cancels enumeration.</param>
    /// <returns>The rows of the awaited collection as untyped values.</returns>
    /// <remarks>
    /// A converter rather than a second type parameter: the call site would have to write the
    /// awaited collection type as well as the row type, since C# cannot infer one type argument and
    /// take the other, and the collection type would then have to join the emitted descriptor model
    /// and carry its own bindability check. The generator emits
    /// <c>static rows =&gt; DataSourceAdapter.FromEnumerable&lt;TRow&gt;(rows)</c> here, which is
    /// where the arm gets chosen, and only for a source that offers more than one row type.
    /// </remarks>
    public static IAsyncEnumerable<object?> FromTaskAsync<TRows>(
        Task<TRows> source,
        Func<TRows, IEnumerable<object?>?> reader,
        CancellationToken cancellationToken) =>
        StreamAsync(source, reader, cancellationToken);

    /// <summary>
    /// Awaits the member's task and streams what <paramref name="reader"/> reads from it.
    /// </summary>
    /// <remarks>
    /// The argument checks stay inside the iterator, where they have always been: both public
    /// overloads hand the enumerable back before anything is enumerated, and a null argument has
    /// reported itself at the first <c>MoveNextAsync</c> since the adapter existed.
    /// </remarks>
    private static async IAsyncEnumerable<object?> StreamAsync<TRows>(
        Task<TRows> source,
        Func<TRows, IEnumerable?> reader,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(reader);

        // A task-wrapped member takes no token, so awaiting it directly would make discovery
        // uninterruptible for as long as the member takes. WaitAsync gives the wait back to the
        // caller on cancellation; the member's own task is left to finish on its own, because
        // there is no way to reach into it and nothing useful left to do with its result.
        TRows rows;
        try
        {
            rows = await source.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            // The task left running here is the abandoned one, not the enumerator the expander
            // races: cancellation can win this wait while the member's own task keeps going and
            // faults later, with nobody holding it. Observing the enumerator alone left that
            // failure to surface as an unobserved exception from a run that cancelled cleanly.
            if (!source.IsCompletedSuccessfully)
            {
                AbandonedWork.Observe(source);
            }
        }

        // The reader answers null for the same thing the awaited value being null answers: a source
        // that produced no collection to read. Both reach the run as the one message that names the
        // failure, rather than as a NullReferenceException from inside the enumeration.
        var readRows = rows is null ? null : reader(rows);

        if (readRows is null)
        {
            throw new InvalidOperationException(
                "An asynchronous test data source completed with a null collection.");
        }

        // Non-generic here on purpose: the arm was already chosen by the reader's own parameter
        // type, and what it returns is the sequence that choice produced.
        foreach (var row in readRows)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return row;
        }
    }
}
