using System.Text;
using NextUnit.Core;

namespace NextUnit.Internal;

/// <summary>
/// Implementation of ITestOutput that captures output for a single test case.
/// Thread-safe implementation using lock for concurrent access.
/// </summary>
internal sealed class TestOutputCapture : ITestOutput
{
    private readonly StringBuilder _output = new();
    private readonly object _lock = new();

    /// <summary>
    /// Gets the captured output as a string.
    /// </summary>
    public string GetOutput()
    {
        lock (_lock)
        {
            return _output.ToString();
        }
    }

    /// <summary>
    /// Writes a line of text to the test output.
    /// </summary>
    public void WriteLine(string message)
    {
        lock (_lock)
        {
            _output.AppendLine(message);
        }
    }

    /// <summary>
    /// Writes formatted text to the test output.
    /// </summary>
    public void WriteLine(string format, params object?[] args)
    {
        lock (_lock)
        {
            _output.AppendLine(string.Format(format, args));
        }
    }
}

/// <summary>
/// No-op implementation of ITestOutput used for class-level and assembly-level lifecycle instances.
/// Discards all output written to it.
/// </summary>
internal sealed class NullTestOutput : ITestOutput
{
    /// <summary>
    /// Singleton instance of the null test output.
    /// </summary>
    public static readonly NullTestOutput Instance = new();

    private NullTestOutput() { }

    /// <summary>
    /// Discards the message.
    /// </summary>
    public void WriteLine(string message)
    {
        // No-op
    }

    /// <summary>
    /// Discards the formatted message.
    /// </summary>
    public void WriteLine(string format, params object?[] args)
    {
        // No-op
    }
}
