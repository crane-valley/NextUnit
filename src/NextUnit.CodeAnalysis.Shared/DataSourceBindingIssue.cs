namespace NextUnit.CodeAnalysis.Shared;

/// <summary>
/// Why the generator declines to bind a data source member it did find.
/// </summary>
/// <remarks>
/// Recorded on the resolution result rather than recomputed by each caller, because the generator
/// and the analyzers have to agree: the generator emits no direct access for a member carrying an
/// issue, and the analyzers report the matching diagnostic for exactly the same members.
/// </remarks>
internal enum DataSourceBindingIssue
{
    /// <summary>
    /// The member binds normally.
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
    CancellationTokenOnSynchronousSource
}
