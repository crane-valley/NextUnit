namespace NextUnit.Internal;

/// <summary>
/// Bridges a generated synchronous <c>[TestData]</c> member to the untyped row sequence the
/// expander consumes, reading it through one named element interface.
/// </summary>
/// <remarks>
/// The expander reads a provider's result as the non-generic <see cref="System.Collections.IEnumerable"/>,
/// which dispatches to whichever <c>GetEnumerator</c> the source type maps that interface to. A
/// source implementing <see cref="IEnumerable{T}"/> more than once therefore yielded the rows of an
/// arm nothing had validated, and no cast at the reading end can change that: the value arrives as
/// <see cref="object"/> and is re-read virtually. The arm has to be chosen where a type argument can
/// still be written, which is the generated call site.
/// <para>
/// The generator emits this only for a source that offers more than one row type. Every other
/// synchronous source is still handed over as it was, so nothing pays for the wrapper where there
/// was never a choice to make.
/// </para>
/// </remarks>
public static class DataSourceAdapter
{
    /// <summary>
    /// Reads the rows of a synchronous data source member as <typeparamref name="TRow"/>.
    /// </summary>
    /// <typeparam name="TRow">The row type the member is read through.</typeparam>
    /// <param name="source">The data source member's return value.</param>
    /// <returns>
    /// The rows of <paramref name="source"/> as untyped values, or <see langword="null"/> when the
    /// member returned <see langword="null"/>.
    /// </returns>
    /// <remarks>
    /// A null source returns null instead of throwing, because the expander already has an answer
    /// for a provider that yields no collection: it reads the member by reflection and reports it as
    /// missing, naming the member and its type. Throwing here would replace that with a complaint
    /// about a parameter of a method the user never called.
    /// </remarks>
    public static IEnumerable<object?>? FromEnumerable<TRow>(IEnumerable<TRow>? source) =>
        source is null ? null : Enumerate(source);

    /// <summary>
    /// Yields the rows of a non-null source.
    /// </summary>
    /// <remarks>
    /// Split from the null check because an iterator body does not run until it is enumerated, and
    /// the expander chooses between the provider's result and its reflection fallback before
    /// enumerating anything. Keeping the check outside the iterator is what lets a null reach that
    /// decision as a null.
    /// </remarks>
    private static IEnumerable<object?> Enumerate<TRow>(IEnumerable<TRow> source)
    {
        // The parameter's static type is what selects the arm: this loop compiles to a call on
        // IEnumerable<TRow>.GetEnumerator through the interface map, where reading the same value as
        // a non-generic IEnumerable dispatches to whatever the source type mapped that to.
        foreach (var row in source)
        {
            yield return row;
        }
    }
}
