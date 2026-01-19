namespace NextUnit.Internal;

/// <summary>
/// Helper methods for exception handling.
/// </summary>
internal static class ExceptionHelper
{
    /// <summary>
    /// Determines whether the specified exception is a critical exception
    /// that should not be caught and handled normally.
    /// </summary>
    /// <param name="ex">The exception to check.</param>
    /// <returns>
    /// <c>true</c> if the exception is critical and should be re-thrown;
    /// otherwise, <c>false</c>.
    /// </returns>
    public static bool IsCriticalException(Exception ex)
    {
        return ex is OutOfMemoryException
            or StackOverflowException
            or ThreadAbortException
            or AccessViolationException;
    }
}
