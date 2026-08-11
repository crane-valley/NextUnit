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
    /// one. The walk is bounded by the exceptions it has already seen rather than by a depth limit:
    /// a limit would have to answer for a chain longer than itself, and the only safe answer there is
    /// the one that stops the run, which makes the limit worse than useless. Reference identity ends
    /// a hand-built cycle without capping anything.
    /// </para>
    /// <para>
    /// An exception that wraps nothing is answered before anything is allocated, which is what nearly
    /// every call is.
    /// </para>
    /// </remarks>
    public static bool IsCriticalFailure(Exception? exception)
    {
        if (exception is null)
        {
            return false;
        }

        if (IsCriticalException(exception))
        {
            return true;
        }

        if (exception is not AggregateException && exception.InnerException is null)
        {
            return false;
        }

        var pending = new Stack<Exception>();
        var seen = new HashSet<Exception>(ReferenceEqualityComparer.Instance);
        pending.Push(exception);

        while (pending.Count > 0)
        {
            var current = pending.Pop();

            if (!seen.Add(current))
            {
                continue;
            }

            if (IsCriticalException(current))
            {
                return true;
            }

            if (current is AggregateException aggregate)
            {
                foreach (var inner in aggregate.InnerExceptions)
                {
                    pending.Push(inner);
                }
            }
            else if (current.InnerException is { } inner)
            {
                pending.Push(inner);
            }
        }

        return false;
    }
}
