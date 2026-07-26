using System.Text;

namespace NextUnit.Generator.Builders;

/// <summary>
/// A text buffer that tracks the current indentation level so emitters describe nesting instead of
/// hardcoding column counts.
/// </summary>
/// <remarks>
/// The emitted text is a public contract pinned by the generator snapshot tests, so the writer
/// reproduces the previous hand-indented layout exactly: four spaces per level, indentation applied
/// once per line at the first write, and blank separator lines emitted with no indentation so no
/// line ever carries trailing whitespace.
/// <para>
/// Lines end with a literal LF rather than <see cref="StringBuilder.AppendLine()"/>, which would
/// use <see cref="Environment.NewLine"/>. Host-dependent newlines would make the generated text -
/// and therefore the incremental generator's cached output and the snapshot baselines - differ
/// between Windows and Linux for reasons unrelated to the compilation being processed.
/// </para>
/// </remarks>
internal sealed class CodeWriter
{
    private const string IndentUnit = "    ";
    private const char LineFeed = '\n';

    private readonly StringBuilder _builder = new();
    private int _level;
    private bool _atLineStart = true;

    /// <summary>
    /// Increases the indentation applied to subsequent lines.
    /// </summary>
    public void Indent() => _level++;

    /// <summary>
    /// Decreases the indentation applied to subsequent lines.
    /// </summary>
    public void Unindent() => _level--;

    /// <summary>
    /// Writes text without ending the line, so the next write continues it.
    /// </summary>
    public void Write(string text)
    {
        WriteIndentIfAtLineStart();
        _builder.Append(text);
    }

    /// <summary>
    /// Writes text and ends the line.
    /// </summary>
    public void WriteLine(string text)
    {
        WriteIndentIfAtLineStart();
        _builder.Append(text).Append(LineFeed);
        _atLineStart = true;
    }

    /// <summary>
    /// Ends the current line, emitting a blank separator line when one is not already open.
    /// </summary>
    public void WriteLine()
    {
        _builder.Append(LineFeed);
        _atLineStart = true;
    }

    /// <summary>
    /// Returns the emitted source text.
    /// </summary>
    public override string ToString() => _builder.ToString();

    private void WriteIndentIfAtLineStart()
    {
        if (!_atLineStart)
        {
            return;
        }

        for (var i = 0; i < _level; i++)
        {
            _builder.Append(IndentUnit);
        }

        _atLineStart = false;
    }
}
