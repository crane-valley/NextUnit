namespace NextUnit.Platform;

/// <summary>
/// Runs one asynchronous initialization at most once, making concurrent callers wait for the run that
/// is already in flight instead of proceeding past it or starting a second one.
/// </summary>
/// <remarks>
/// <para>
/// A plain "check a flag, await, then set it" sequence cannot provide this: the window between the
/// check and the set lets a second caller run the operation again, and setting the flag before the
/// await instead reports an operation as done while it is still running.
/// </para>
/// <para>
/// A failed or cancelled operation is not recorded as complete, so the next caller retries it. The
/// gate is deliberately not disposable: its lifetime is its owner's, and the owner outlives any single
/// request that passes through it.
/// </para>
/// </remarks>
internal sealed class AsyncOnceGate
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private bool _completed;

    /// <summary>
    /// Gets a value indicating whether the operation has completed successfully.
    /// </summary>
    public bool HasCompleted => Volatile.Read(ref _completed);

    /// <summary>
    /// Runs <paramref name="operation"/> if it has not already completed successfully; otherwise waits
    /// for the run in flight and returns.
    /// </summary>
    public async Task RunOnceAsync(Func<CancellationToken, Task> operation, CancellationToken cancellationToken)
    {
        // Fast path for the common case, where the operation completed long ago. The volatile read
        // pairs with the write below so a caller that sees the flag also sees the operation's effects.
        if (Volatile.Read(ref _completed))
        {
            return;
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_completed)
            {
                return;
            }

            await operation(cancellationToken).ConfigureAwait(false);

            // Published only after the operation succeeds: a throw leaves the gate open so the next
            // caller retries, which is what the un-synchronized flag it replaced also did.
            Volatile.Write(ref _completed, true);
        }
        finally
        {
            _gate.Release();
        }
    }
}
