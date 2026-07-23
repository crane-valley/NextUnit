using System.Text;
using NextUnit.Core;

namespace NextUnit.Internal;

/// <summary>
/// Implementation of ITestOutput that captures output for a single test case.
/// Thread-safe implementation using lock for concurrent access.
/// </summary>
internal sealed class TestOutputCapture : ITestOutput
{
    private OutputBuffer? _buffer;

    /// <summary>
    /// Gets the captured output as a string.
    /// </summary>
    public string GetOutput()
    {
        var buffer = Volatile.Read(ref _buffer);
        if (buffer is null)
        {
            return string.Empty;
        }

        lock (buffer)
        {
            return buffer.Output.ToString();
        }
    }

    /// <summary>
    /// Writes a line of text to the test output.
    /// </summary>
    public void WriteLine(string message)
    {
        var buffer = GetOrCreateBuffer();
        lock (buffer)
        {
            buffer.Output.AppendLine(message);
        }
    }

    /// <summary>
    /// Writes formatted text to the test output.
    /// </summary>
    public void WriteLine(string format, params object?[] args)
    {
        var buffer = GetOrCreateBuffer();
        lock (buffer)
        {
            buffer.Output.AppendFormat(format, args);
            buffer.Output.AppendLine();
        }
    }

    private OutputBuffer GetOrCreateBuffer()
    {
        var buffer = Volatile.Read(ref _buffer);
        if (buffer is not null)
        {
            return buffer;
        }

        var created = new OutputBuffer();
        return Interlocked.CompareExchange(ref _buffer, created, null) ?? created;
    }

    private sealed class OutputBuffer
    {
        public StringBuilder Output { get; } = new();
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
