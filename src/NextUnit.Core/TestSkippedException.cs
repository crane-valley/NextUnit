namespace NextUnit;

/// <summary>
/// Represents an exception that is thrown when a test is skipped during execution.
/// </summary>
/// <remarks>
/// This exception is thrown by <see cref="Assert.Skip"/> and related methods
/// to indicate that a test should be skipped at runtime rather than failing.
/// </remarks>
public sealed class TestSkippedException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TestSkippedException"/> class
    /// with a default message.
    /// </summary>
    public TestSkippedException() : base("Test was skipped.") { }

    /// <summary>
    /// Initializes a new instance of the <see cref="TestSkippedException"/> class
    /// with a specified message describing why the test is being skipped.
    /// </summary>
    /// <param name="message">The message that describes why the test is being skipped.</param>
    public TestSkippedException(string message) : base(message) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="TestSkippedException"/> class
    /// with a specified message and inner exception.
    /// </summary>
    /// <param name="message">The message that describes why the test is being skipped.</param>
    /// <param name="inner">The exception that is the cause of the current exception.</param>
    public TestSkippedException(string message, Exception inner) : base(message, inner) { }
}
