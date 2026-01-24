using System.Diagnostics;

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

    /// <summary>
    /// Disposes all objects in a collection.
    /// </summary>
    /// <param name="instances">The collection of objects to dispose.</param>
    public static void DisposeAllIn(IEnumerable<object> instances)
    {
        foreach (var instance in instances)
        {
            DisposeIfNeeded(instance);
        }
    }

    /// <summary>
    /// Disposes an object with error handling for cleanup scenarios.
    /// Logs errors but does not throw (except for fatal exceptions).
    /// </summary>
    /// <param name="instance">The object to dispose.</param>
    /// <remarks>
    /// <para>
    /// <b>Warning:</b> This method blocks on async disposal using GetAwaiter().GetResult().
    /// In synchronization contexts that don't allow blocking (e.g., UI threads),
    /// this could potentially cause deadlocks. In test frameworks, this is typically safe
    /// as tests run on thread pool threads without special synchronization contexts.
    /// </para>
    /// <para>
    /// If deadlocks occur in production use, consider implementing a fully async cleanup path.
    /// </para>
    /// </remarks>
    public static void DisposeIfNeeded(object? instance)
    {
        if (instance is null)
        {
            return;
        }

        try
        {
            if (instance is IAsyncDisposable asyncDisposable)
            {
                asyncDisposable.DisposeAsync().AsTask().GetAwaiter().GetResult();
            }
            else if (instance is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
        catch (OutOfMemoryException)
        {
            throw; // Fatal exception - do not swallow
        }
        catch (OperationCanceledException)
        {
            throw; // Cancellation should propagate
        }
        catch (Exception ex) when (ex is not StackOverflowException)
        {
            // Best-effort disposal: log full exception and continue to avoid failing test cleanup
            Debug.WriteLine($"[NextUnit] Failed to dispose shared instance '{instance.GetType().FullName}': {ex}");
        }
    }
}
