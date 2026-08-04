namespace NextUnit.Docs.Tests;

/// <summary>
/// A fenced code block taken from a Markdown document.
/// </summary>
/// <param name="DocumentName">The document the block came from.</param>
/// <param name="FenceLine">The one-based line number of the opening fence.</param>
/// <param name="InfoString">The text following the opening backticks, such as <c>csharp nunit</c>.</param>
/// <param name="Code">The block body, normalized to LF line endings.</param>
internal sealed record MarkdownCodeBlock(string DocumentName, int FenceLine, string InfoString, string Code)
{
    /// <summary>
    /// Gets the first info-string token, which Markdown renderers treat as the language.
    /// </summary>
    public string Language => Tokens.Length == 0 ? string.Empty : Tokens[0];

    /// <summary>
    /// Gets the info-string tokens after the language.
    /// </summary>
    public IReadOnlyList<string> Annotations => Tokens.Length == 0 ? [] : Tokens[1..];

    /// <summary>
    /// Gets a reference to the block that points at the document rather than at a generated file.
    /// </summary>
    public string Location => $"{DocumentName} line {FenceLine}";

    private string[] Tokens => InfoString.Split(' ', StringSplitOptions.RemoveEmptyEntries);
}

/// <summary>
/// Extracts fenced code blocks from Markdown.
/// </summary>
/// <remarks>
/// This handles the fenced-block subset the NextUnit documentation actually uses: backtick fences
/// that are never nested. A full Markdown parser would be a dependency and a maintenance surface
/// for no additional coverage.
/// </remarks>
internal static class MarkdownCodeBlocks
{
    private const string Fence = "```";

    public static IReadOnlyList<MarkdownCodeBlock> Parse(string documentName, string markdown)
    {
        var lines = markdown.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var blocks = new List<MarkdownCodeBlock>();

        for (var index = 0; index < lines.Length; index++)
        {
            if (!lines[index].TrimStart().StartsWith(Fence, StringComparison.Ordinal))
            {
                continue;
            }

            var fenceLine = index + 1;
            var infoString = lines[index].Trim().TrimStart('`').Trim();
            var body = new List<string>();

            index++;
            while (index < lines.Length && !string.Equals(lines[index].Trim(), Fence, StringComparison.Ordinal))
            {
                body.Add(lines[index]);
                index++;
            }

            if (index >= lines.Length)
            {
                throw new InvalidOperationException(
                    $"{documentName}: the code fence opened on line {fenceLine} is never closed.");
            }

            blocks.Add(new MarkdownCodeBlock(documentName, fenceLine, infoString, string.Join('\n', body)));
        }

        return blocks;
    }
}
