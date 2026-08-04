using NextUnit.Core;

namespace NextUnit.SampleTests;

public class RetryTests
{
    private static int _retryCounter;
    private static int _flakyCounter;

    [Test]
    public void RegularTestWithoutRetry()
    {
        // This test runs once and passes
        Assert.True(true);
    }

    [Test]
    [Retry(3)]
    public void RetryEventuallyPasses()
    {
        // This test fails twice and then passes on the third attempt
        var attempt = Interlocked.Increment(ref _retryCounter);
        if (attempt < 3)
        {
            Assert.True(false, $"Intentional failure on attempt {attempt}");
        }
        Assert.True(true, "Passed on third attempt");
    }

    [Test]
    [Retry(2, 100)]
    public void RetryWithDelay()
    {
        // This test uses a 100ms delay between retries
        Assert.True(true, "This test passes immediately");
    }

    [Test]
    [Flaky("Known intermittent issue due to timing")]
    public void FlakyTestMarkedAsFlaky()
    {
        // This test is marked as flaky for documentation purposes
        Assert.True(true);
    }

    [Test]
    [Flaky]
    [Retry(3)]
    public void FlakyTestWithRetry()
    {
        // This test is both flaky and has retry enabled
        var attempt = Interlocked.Increment(ref _flakyCounter);
        if (attempt < 2)
        {
            Assert.True(false, $"Intentional failure on attempt {attempt}");
        }
        Assert.True(true, "Passed on second attempt");
    }

    [Test]
    [Retry(2)]
    public void RetryAttemptIsVisibleInTheContext()
    {
        // The one-based attempt number is available to the test itself, so a test can log or branch
        // on it without the framework keeping separate statistics.
        Assert.True(TestContext.Current!.RetryAttempt >= 1);
    }

    [Before(LifecycleScope.Class)]
    public void ResetCounters()
    {
        // Reset counters before each class run using thread-safe operations
        Interlocked.Exchange(ref _retryCounter, 0);
        Interlocked.Exchange(ref _flakyCounter, 0);
    }
}

/// <summary>
/// Retries only the failures that are worth another attempt.
/// </summary>
/// <remarks>
/// The generator emits a direct <c>new RetryOnTransientFailure()</c>, so the policy works under
/// Native AOT with no reflection and no trimming annotation.
/// </remarks>
public sealed class RetryOnTransientFailure : IRetryPolicy
{
    public ValueTask<bool> ShouldRetryAsync(RetryContext context) =>
        ValueTask.FromResult(context.Exception is TimeoutException);
}

public class SelectiveRetryTests
{
    private static int _transientCounter;

    [Test]
    [Retry<RetryOnTransientFailure>(3)]
    public void RetriesOnlyTheFailuresThePolicyAccepts()
    {
        // Fails twice with the exception the policy accepts, then passes. A failure the policy
        // rejects - anything that is not a TimeoutException - would be reported immediately instead
        // of spending the remaining attempts, and the attempt count reaches the failure output.
        var attempt = Interlocked.Increment(ref _transientCounter);
        if (attempt < 3)
        {
            throw new TimeoutException($"Intentional transient failure on attempt {attempt}");
        }

        Assert.Equal(3, TestContext.Current!.RetryAttempt);
    }

    [Before(LifecycleScope.Class)]
    public void ResetCounters()
    {
        Interlocked.Exchange(ref _transientCounter, 0);
    }
}

[Retry(2)]
public class ClassLevelRetryTests
{
    private static int _classRetryCounter;

    [Test]
    public void InheritedRetryFromClass()
    {
        // This test inherits retry count from the class
        var attempt = Interlocked.Increment(ref _classRetryCounter);
        if (attempt < 2)
        {
            Assert.True(false, $"Intentional failure on attempt {attempt}");
        }
        Assert.True(true, "Passed on second attempt");
    }

    [Test]
    [Retry(3)]
    public void MethodOverridesClassRetry()
    {
        // Method-level retry overrides class-level
        Assert.True(true, "This test has 3 retries");
    }

    [Before(LifecycleScope.Class)]
    public void ResetCounters()
    {
        Interlocked.Exchange(ref _classRetryCounter, 0);
    }
}

[Flaky("All tests in this class are known to be flaky")]
public class FlakyTestClass
{
    [Test]
    public void AllTestsInClassAreFlaky()
    {
        Assert.True(true);
    }

    [Test]
    [Flaky("This specific test has its own flaky reason")]
    public void IndividualFlakyReason()
    {
        Assert.True(true);
    }
}
