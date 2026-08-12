namespace NextUnit.CodeAnalysis.Shared;

/// <summary>
/// Why the generator declines to bind a data source member it did find.
/// </summary>
/// <remarks>
/// Recorded on the resolution result rather than recomputed by each caller, because the generator
/// and the analyzers have to agree: the generator emits no direct access for a member carrying an
/// issue, and the analyzers report the matching diagnostic for exactly the same members.
/// <para>
/// The invariant is exact and worth keeping exact: <see cref="None"/> if and only if the generator
/// emits a provider. Every reason the generator has for emitting nothing is a value here, so no
/// caller has to re-derive one. A shape that produced no provider while the result still read as
/// success was the one exception, and it made a resolution that only exists to be reported look
/// indistinguishable from a usable one.
/// </para>
/// </remarks>
internal enum DataSourceBindingIssue
{
    /// <summary>
    /// The member binds normally, and the generator emits a provider for it.
    /// </summary>
    None,

    /// <summary>
    /// The member exists but the generated registry cannot name it, so emitting direct access would
    /// fail the consumer's build with <c>CS0122</c>. Reported as <c>NU0020</c>.
    /// </summary>
    MemberNotAccessible,

    /// <summary>
    /// The member takes the discovery cancellation token but returns a type that classifies as a
    /// synchronous collection because it implements <c>IEnumerable</c>, generic or not, as well as
    /// <c>IAsyncEnumerable&lt;T&gt;</c>. The synchronous provider takes no arguments, so there is no
    /// token to pass. Reported as <c>NU0021</c>.
    /// </summary>
    CancellationTokenOnSynchronousSource,

    /// <summary>
    /// The member returns an awaitable that cannot produce rows, such as bare <c>Task</c> or
    /// <c>Task&lt;int&gt;</c>. Reported as <c>NU0014</c>.
    /// </summary>
    /// <remarks>
    /// The member is still returned with this issue, because naming it is the whole point of the
    /// diagnostic. Carrying the issue is what keeps that from reading as a successful binding: the
    /// generator has always emitted nothing for this shape, and a caller asking whether resolution
    /// succeeded now gets the same answer the generator acts on.
    /// </remarks>
    UnsupportedAwaitable
}
