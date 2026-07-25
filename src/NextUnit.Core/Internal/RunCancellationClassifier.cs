namespace NextUnit.Internal;

/// <summary>
/// Decides whether an <see cref="OperationCanceledException"/> means "this run was cancelled" or
/// "this hook cancelled something of its own".
/// </summary>
/// <remarks>
/// The distinction matters because test adapters treat run cancellation as a normal outcome and
/// swallow it. An OCE thrown by a lifecycle hook for its own reasons would therefore disappear
/// silently, so it is wrapped in a non-OCE and surfaced as a failure instead.
/// </remarks>
internal static class RunCancellationClassifier
{
    /// <summary>
    /// Determines whether an <see cref="OperationCanceledException"/> represents cancellation of
    /// this run rather than a lifecycle hook's own unrelated cancellation.
    /// </summary>
    /// <remarks>
    /// The exception must carry the run token; an OCE bearing a different token (or
    /// <see cref="CancellationToken.None"/>) is a hook's own cancellation and is treated as a failure.
    /// </remarks>
    public static bool IsRunCancellation(
        OperationCanceledException exception,
        CancellationToken cancellationToken) =>
        cancellationToken.IsCancellationRequested && exception.CancellationToken == cancellationToken;

    /// <summary>
    /// Converts an exception into the form it should take in a failure list, wrapping a
    /// non-run-cancellation <see cref="OperationCanceledException"/> so it cannot be mistaken for
    /// run cancellation.
    /// </summary>
    /// <param name="exception">The exception observed.</param>
    /// <param name="wrapMessage">
    /// The message describing which stage threw, used only when wrapping is required.
    /// </param>
    public static Exception ToFailure(Exception exception, string wrapMessage) =>
        exception is OperationCanceledException
            ? new InvalidOperationException(wrapMessage, exception)
            : exception;
}
