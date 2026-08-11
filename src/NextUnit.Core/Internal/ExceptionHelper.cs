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

    /// <summary>
    /// Determines whether an exception is critical, or carries a critical exception inside it.
    /// </summary>
    /// <param name="exception">The exception to classify.</param>
    /// <returns>
    /// <c>true</c> if the exception or anything it wraps must not be swallowed; otherwise
    /// <c>false</c>.
    /// </returns>
    /// <remarks>
    /// <para>
    /// Cleanup paths aggregate what they catch, so testing only the outer exception lets an
    /// <see cref="OutOfMemoryException"/> reach a caller disguised as an ordinary cleanup failure:
    /// one disposer failing that way is reported as a bad data source while the process is actually
    /// out of memory. Anything that catches broadly in order to keep going should use this rather
    /// than <see cref="IsCriticalException"/>.
    /// </para>
    /// <para>
    /// Both wrapping shapes are walked, because either can hide the same failure: an
    /// <see cref="AggregateException"/> holds many inner exceptions, and an ordinary exception holds
    /// one. The depth limit is a guard against a hand-built cycle, not an expected shape; real
    /// exception chains are a few links long.
    /// </para>
    /// </remarks>
    public static bool IsCriticalFailure(Exception? exception) => IsCriticalFailure(exception, depth: 0);

    private static bool IsCriticalFailure(Exception? exception, int depth)
    {
        const int MaxDepth = 16;

        if (exception is null || depth > MaxDepth)
        {
            return false;
        }

        if (IsCriticalException(exception))
        {
            return true;
        }

        if (exception is AggregateException aggregate)
        {
            foreach (var inner in aggregate.InnerExceptions)
            {
                if (IsCriticalFailure(inner, depth + 1))
                {
                    return true;
                }
            }

            return false;
        }

        return IsCriticalFailure(exception.InnerException, depth + 1);
    }
}
