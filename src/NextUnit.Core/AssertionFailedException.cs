namespace NextUnit;

/// <summary>
/// Represents an exception that is thrown when an assertion fails during test execution.
/// </summary>
public sealed class AssertionFailedException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AssertionFailedException"/> class.
    /// </summary>
    public AssertionFailedException() : base() { }

    /// <summary>
    /// Initializes a new instance of the <see cref="AssertionFailedException"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the assertion failure.</param>
    public AssertionFailedException(string message) : base(message) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="AssertionFailedException"/> class with a specified error message and inner exception.
    /// </summary>
    /// <param name="message">The message that describes the assertion failure.</param>
    /// <param name="inner">The exception that is the cause of the current exception.</param>
    public AssertionFailedException(string message, Exception inner) : base(message, inner) { }
}
