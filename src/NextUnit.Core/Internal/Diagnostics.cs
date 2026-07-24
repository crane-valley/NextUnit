namespace NextUnit.Internal;

/// <summary>
/// Best-effort diagnostic output for non-fatal warnings.
/// </summary>
internal static class Diagnostics
{
    /// <summary>
    /// Writes a diagnostic message to standard error, swallowing any failure of the writer itself.
    /// </summary>
    /// <remarks>
    /// The error stream can be closed or redirected to a broken pipe (ObjectDisposedException / IOException).
    /// Diagnostic output must never turn into a discovery/cleanup failure or mask the original exception,
    /// so writer failures are ignored here.
    /// </remarks>
    public static void SafeWriteError(string message)
    {
        try
        {
            Console.Error.WriteLine(message);
        }
        catch (Exception ex) when (!ExceptionHelper.IsCriticalException(ex))
        {
            // Intentionally ignored: diagnostics must not throw.
        }
    }
}
