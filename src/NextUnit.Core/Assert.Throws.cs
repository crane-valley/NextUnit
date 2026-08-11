namespace NextUnit;

public static partial class Assert
{
    /// <summary>
    /// Verifies that an action throws a specific type of exception.
    /// </summary>
    /// <typeparam name="TException">The expected exception type.</typeparam>
    /// <param name="action">The action to execute.</param>
    /// <param name="message">Optional custom message to display if the assertion fails.</param>
    /// <returns>The exception that was thrown.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="action"/> is null.</exception>
    /// <exception cref="AssertionFailedException">Thrown when no exception is thrown or a different exception type is thrown.</exception>
    public static TException Throws<TException>(Action action, string? message = null)
        where TException : Exception
    {
        ArgumentNullException.ThrowIfNull(action);

        try
        {
            action();
        }
        catch (TException ex)
        {
            return ex;
        }
        catch (Exception ex)
        {
            throw new AssertionFailedException(
                message ?? $"Expected {typeof(TException).Name} but got {ex.GetType().Name}.",
                ex);
        }

        throw new AssertionFailedException(
            message ?? $"Expected {typeof(TException).Name} but no exception was thrown.");
    }

    /// <summary>
    /// Verifies that an asynchronous action throws a specific type of exception.
    /// </summary>
    /// <typeparam name="TException">The expected exception type.</typeparam>
    /// <param name="action">The asynchronous action to execute.</param>
    /// <param name="message">Optional custom message to display if the assertion fails.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the exception that was thrown.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="action"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="action"/> returns a null task.</exception>
    /// <exception cref="AssertionFailedException">Thrown when no exception is thrown or a different exception type is thrown.</exception>
    public static async Task<TException> ThrowsAsync<TException>(
        Func<Task> action,
        string? message = null)
        where TException : Exception
    {
        ArgumentNullException.ThrowIfNull(action);

        Task? task = null;
        try
        {
            task = action();

            // Awaiting inside the try keeps synchronously thrown expected exceptions matching,
            // while the null task is reported after the try so the misuse is not reclassified
            // as an assertion failure by the catch clauses below.
            if (task is not null)
            {
                await task.ConfigureAwait(false);
            }
        }
        catch (TException ex)
        {
            return ex;
        }
        catch (Exception ex)
        {
            throw new AssertionFailedException(
                message ?? $"Expected {typeof(TException).Name} but got {ex.GetType().Name}.",
                ex);
        }

        if (task is null)
        {
            throw new ArgumentException(NullTaskMessage, nameof(action));
        }

        throw new AssertionFailedException(
            message ?? $"Expected {typeof(TException).Name} but no exception was thrown.");
    }

    /// <summary>
    /// Verifies that an action does not throw any exception.
    /// </summary>
    /// <param name="action">The action to execute.</param>
    /// <param name="message">Optional custom message to display if the assertion fails.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="action"/> is null.</exception>
    /// <exception cref="AssertionFailedException">Thrown when the action throws a non-control-flow exception.</exception>
    /// <remarks>
    /// Exceptions that the test engine handles specially are excluded by the catch filter, so
    /// they propagate unchanged (keeping their original stack) rather than being wrapped, and
    /// this assertion stays transparent to the runtime: runtime skips
    /// (<see cref="TestSkippedException"/>), cancellation (<see cref="OperationCanceledException"/>,
    /// including the derived <see cref="TaskCanceledException"/>), an inner
    /// <see cref="AssertionFailedException"/> (its original formatted message is preserved),
    /// and critical fail-fast exceptions (out-of-memory, stack overflow, thread abort, and
    /// access violation, per the shared critical-exception check). Cancellation propagates
    /// unconditionally because the engine decides timeout-versus-failure from its own timeout
    /// token state, not from the exception, so the outcome matches a bare test body throwing
    /// the same exception.
    /// </remarks>
    public static void DoesNotThrow(Action action, string? message = null)
    {
        ArgumentNullException.ThrowIfNull(action);

        try
        {
            action();
        }
        catch (Exception ex) when (IsUnexpectedFailure(ex))
        {
            // Filter (not catch-and-rethrow) so control-flow and critical exceptions keep
            // their original stack and first-chance debugger behavior; see IsUnexpectedFailure.
            throw new AssertionFailedException(
                message ?? $"Expected no exception but got {ex.GetType().Name}: {ex.Message}",
                ex);
        }
    }

    /// <summary>
    /// Verifies that an asynchronous action does not throw any exception.
    /// </summary>
    /// <param name="action">The asynchronous action to execute.</param>
    /// <param name="message">Optional custom message to display if the assertion fails.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="action"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="action"/> returns a null task.</exception>
    /// <exception cref="AssertionFailedException">Thrown when the action throws a non-control-flow exception.</exception>
    /// <remarks>
    /// Exceptions that the test engine handles specially are excluded by the catch filter, so
    /// they propagate unchanged (keeping their original stack) rather than being wrapped, and
    /// this assertion stays transparent to the runtime: runtime skips
    /// (<see cref="TestSkippedException"/>), cancellation (<see cref="OperationCanceledException"/>,
    /// including the derived <see cref="TaskCanceledException"/>), an inner
    /// <see cref="AssertionFailedException"/> (its original formatted message is preserved),
    /// and critical fail-fast exceptions (out-of-memory, stack overflow, thread abort, and
    /// access violation, per the shared critical-exception check). Cancellation propagates
    /// unconditionally because the engine decides timeout-versus-failure from its own timeout
    /// token state, not from the exception, so the outcome matches a bare test body throwing
    /// the same exception.
    /// </remarks>
    public static async Task DoesNotThrowAsync(Func<Task> action, string? message = null)
    {
        ArgumentNullException.ThrowIfNull(action);

        Task? task = null;
        try
        {
            task = action();

            // Awaiting inside the try keeps synchronously thrown exceptions wrapped as before,
            // while the null task is reported after the try so the misuse is not turned into an
            // assertion failure by the catch clause below.
            if (task is not null)
            {
                await task.ConfigureAwait(false);
            }
        }
        catch (Exception ex) when (IsUnexpectedFailure(ex))
        {
            // Filter (not catch-and-rethrow) so control-flow and critical exceptions keep
            // their original stack and first-chance debugger behavior; see IsUnexpectedFailure.
            throw new AssertionFailedException(
                message ?? $"Expected no exception but got {ex.GetType().Name}: {ex.Message}",
                ex);
        }

        if (task is null)
        {
            throw new ArgumentException(NullTaskMessage, nameof(action));
        }
    }

    // Awaiting a null Task raises an opaque NullReferenceException that the assertion would
    // otherwise report as the tested code failing, so the delegate misuse is named explicitly.
    private const string NullTaskMessage = "The action delegate returned a null Task.";

    // Determines whether an exception raised inside a DoesNotThrow action is a genuine
    // failure to wrap, versus one that must reach the test engine untouched. Excluded:
    // TestSkippedException (runtime skip control flow); OperationCanceledException, incl. the
    // derived TaskCanceledException (the engine classifies timeout-versus-failure from its own
    // timeout-token state, not the exception, so wrapping could hide a genuine timeout);
    // AssertionFailedException (an inner assert already carries a formatted message);
    // and critical fail-fast exceptions (out-of-memory, stack overflow, thread abort, access
    // violation) via the shared repo check. Used as an exception filter so the excluded
    // exceptions propagate without stack unwinding.
    private static bool IsUnexpectedFailure(Exception ex) =>
        ex is not TestSkippedException
            and not OperationCanceledException
            and not AssertionFailedException
        && !Internal.ExceptionHelper.IsCriticalException(ex);
}
