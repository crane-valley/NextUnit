namespace NextUnit.Internal;

/// <summary>
/// Helper for disposing objects that implement IDisposable or IAsyncDisposable.
/// </summary>
internal static class DisposeHelper
{
    /// <summary>
    /// Disposes an object if it implements IDisposable or IAsyncDisposable.
    /// Prefers IDisposable for backward compatibility with existing test classes.
    /// </summary>
    /// <param name="instance">The object to dispose.</param>
    /// <returns>A task representing the asynchronous dispose operation.</returns>
    public static async ValueTask DisposeAsync(object? instance)
    {
        if (instance is null)
        {
            return;
        }

        // Prefer IDisposable for backward compatibility with existing test classes
        if (instance is IDisposable disposable)
        {
            disposable.Dispose();
        }
        else if (instance is IAsyncDisposable asyncDisposable)
        {
            await asyncDisposable.DisposeAsync().ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Disposes an object synchronously if it implements IDisposable.
    /// Falls back to blocking on IAsyncDisposable if only that interface is implemented.
    /// </summary>
    /// <param name="instance">The object to dispose.</param>
    public static void Dispose(object? instance)
    {
        if (instance is null)
        {
            return;
        }

        if (instance is IDisposable disposable)
        {
            disposable.Dispose();
        }
        else if (instance is IAsyncDisposable asyncDisposable)
        {
            // ConfigureAwait(false) avoids synchronization-context deadlocks when blocking
            asyncDisposable.DisposeAsync().AsTask().ConfigureAwait(false).GetAwaiter().GetResult();
        }
    }
}
