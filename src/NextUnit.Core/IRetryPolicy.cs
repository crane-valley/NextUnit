using NextUnit.Core;

namespace NextUnit;

/// <summary>
/// Decides whether a failed test attempt should run again.
/// </summary>
/// <remarks>
/// <para>
/// Attach a policy with <see cref="RetryAttribute{TPolicy}"/>. A test that uses the non-generic
/// <see cref="RetryAttribute"/> has no policy and keeps the default behavior: every failure that is
/// not a timeout, a runtime skip, or run cancellation is retried until the attempt budget is spent.
/// </para>
/// <para>
/// The policy is consulted only when a further attempt is actually available, so it never runs after
/// the last attempt, and never for a passing, skipped, timed-out, or cancelled attempt.
/// </para>
/// <para>
/// One instance is created per test execution, on the first decision, and reused for the remaining
/// decisions of that test. Instances are never shared between test cases and are never disposed, so
/// a policy that needs owned resources should acquire and release them inside
/// <see cref="ShouldRetryAsync"/>.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// public sealed class RetryOnlyTimeouts : IRetryPolicy
/// {
///     public ValueTask&lt;bool&gt; ShouldRetryAsync(RetryContext context) =>
///         ValueTask.FromResult(context.Exception is TimeoutException);
/// }
///
/// public class OrderTests
/// {
///     [Test]
///     [Retry&lt;RetryOnlyTimeouts&gt;(3)]
///     public async Task PlacesOrder()
///     {
///     }
/// }
/// </code>
/// </example>
public interface IRetryPolicy
{
    /// <summary>
    /// Decides whether the failed attempt described by <paramref name="context"/> should run again.
    /// </summary>
    /// <param name="context">The failed attempt, its exception, and the attempt budget.</param>
    /// <returns><c>true</c> to run another attempt; <c>false</c> to report the failure now.</returns>
    /// <remarks>
    /// An exception thrown here is reported together with the test's own failure rather than
    /// swallowed, and it stops further attempts: a policy that cannot decide must not silently
    /// turn into either answer.
    /// </remarks>
    public ValueTask<bool> ShouldRetryAsync(RetryContext context);
}

/// <summary>
/// Describes the failed test attempt that a retry decision is being made for.
/// </summary>
public sealed class RetryContext
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RetryContext"/> class.
    /// </summary>
    /// <param name="exception">The exception that failed the attempt.</param>
    /// <param name="testContext">The context of the test being executed.</param>
    /// <param name="attempt">The one-based number of the attempt that just failed.</param>
    /// <param name="maxAttempts">The total number of attempts configured for the test.</param>
    /// <param name="cancellationToken">The run cancellation token.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="exception"/> or <paramref name="testContext"/> is null.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="attempt"/> is less than 1, or <paramref name="maxAttempts"/> is
    /// less than <paramref name="attempt"/>.
    /// </exception>
    public RetryContext(
        Exception exception,
        ITestContext testContext,
        int attempt,
        int maxAttempts,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(exception);
        ArgumentNullException.ThrowIfNull(testContext);
        ArgumentOutOfRangeException.ThrowIfLessThan(attempt, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxAttempts, attempt);

        Exception = exception;
        TestContext = testContext;
        Attempt = attempt;
        MaxAttempts = maxAttempts;
        CancellationToken = cancellationToken;
    }

    /// <summary>
    /// Gets the exception that failed the attempt.
    /// </summary>
    public Exception Exception { get; }

    /// <summary>
    /// Gets the context of the test being executed, including its name, arguments, output, and
    /// <see cref="ITestContext.StateBag"/>.
    /// </summary>
    /// <remarks>
    /// This is the context of the attempt that just failed. Each attempt gets a fresh context, so
    /// state stored in the <see cref="ITestContext.StateBag"/> does not carry into the next attempt.
    /// </remarks>
    public ITestContext TestContext { get; }

    /// <summary>
    /// Gets the one-based number of the attempt that just failed.
    /// </summary>
    public int Attempt { get; }

    /// <summary>
    /// Gets the total number of attempts configured for the test, including the first one.
    /// </summary>
    /// <remarks>
    /// This is the <c>count</c> given to <see cref="RetryAttribute{TPolicy}"/>, so
    /// <see cref="Attempt"/> is always less than this value: the policy is not consulted once the
    /// budget is spent.
    /// </remarks>
    public int MaxAttempts { get; }

    /// <summary>
    /// Gets the run cancellation token, so a policy that waits or probes before deciding can stop
    /// when the run is cancelled.
    /// </summary>
    /// <remarks>
    /// This is the run token rather than the failed attempt's token: the attempt's token may carry a
    /// spent per-attempt <see cref="TimeoutAttribute"/> budget, which says nothing about whether the
    /// run is still going.
    /// </remarks>
    public CancellationToken CancellationToken { get; }
}
