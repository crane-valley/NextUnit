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
/// </remarks>
internal sealed class CodeWriter
{
    private const string IndentUnit = "    ";

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
        _builder.AppendLine(text);
        _atLineStart = true;
    }

    /// <summary>
    /// Ends the current line, emitting a blank separator line when one is not already open.
    /// </summary>
    public void WriteLine()
    {
        _builder.AppendLine();
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
