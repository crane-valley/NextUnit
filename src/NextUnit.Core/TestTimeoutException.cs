namespace NextUnit;

/// <summary>
/// Represents an exception that is thrown when a test exceeds its configured timeout.
/// </summary>
/// <remarks>
/// This exception is thrown by the test execution engine when a test takes longer than
/// the time specified by the <see cref="TimeoutAttribute"/>.
/// </remarks>
public sealed class TestTimeoutException : Exception
{
    /// <summary>
    /// Gets the timeout value in milliseconds that was exceeded.
    /// </summary>
    public int TimeoutMs { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="TestTimeoutException"/> class.
    /// </summary>
    public TestTimeoutException()
        : base("Test execution exceeded the configured timeout.")
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TestTimeoutException"/> class
    /// with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the timeout.</param>
    public TestTimeoutException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TestTimeoutException"/> class
    /// with a specified error message and inner exception.
    /// </summary>
    /// <param name="message">The message that describes the timeout.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public TestTimeoutException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TestTimeoutException"/> class
    /// with a specified timeout value.
    /// </summary>
    /// <param name="timeoutMs">The timeout value in milliseconds that was exceeded.</param>
    public TestTimeoutException(int timeoutMs)
        : base($"Test execution exceeded the timeout of {timeoutMs}ms.")
    {
        TimeoutMs = timeoutMs;
    }
}
