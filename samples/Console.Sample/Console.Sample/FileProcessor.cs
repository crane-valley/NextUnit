namespace Console.Sample;

/// <summary>
/// Processes text files and generates reports.
/// </summary>
public class FileProcessor
{
    /// <summary>
    /// Analyzes a text file and returns statistics.
    /// </summary>
    public FileStatistics AnalyzeFile(string content)
    {
        if (string.IsNullOrEmpty(content))
        {
            return new FileStatistics();
        }

        var lines = content.Split('\n');
        var words = content.Split(new[] { ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries);

        return new FileStatistics
        {
            LineCount = lines.Length,
            WordCount = words.Length,
            CharacterCount = content.Length,
            NonWhitespaceCharacterCount = content.Count(c => !char.IsWhiteSpace(c)),
            AverageWordsPerLine = lines.Length > 0 ? (double)words.Length / lines.Length : 0
        };
    }

    /// <summary>
    /// Filters lines based on a predicate.
    /// </summary>
    public IEnumerable<string> FilterLines(string content, Func<string, bool> predicate)
    {
        if (string.IsNullOrEmpty(content))
        {
            return Enumerable.Empty<string>();
        }

        return content.Split('\n').Where(predicate);
    }

    /// <summary>
    /// Finds lines containing a specific search term.
    /// </summary>
    public IEnumerable<LineMatch> SearchLines(string content, string searchTerm, bool caseSensitive = false)
    {
        if (string.IsNullOrEmpty(content) || string.IsNullOrEmpty(searchTerm))
        {
            return Enumerable.Empty<LineMatch>();
        }

        var comparison = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        var lines = content.Split('\n');
        var matches = new List<LineMatch>();

        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i].Contains(searchTerm, comparison))
            {
                matches.Add(new LineMatch
                {
                    LineNumber = i + 1,
                    Content = lines[i],
                    MatchCount = CountOccurrences(lines[i], searchTerm, comparison)
                });
            }
        }

        return matches;
    }

    private static int CountOccurrences(string text, string searchTerm, StringComparison comparison)
    {
        int count = 0;
        int index = 0;

        while ((index = text.IndexOf(searchTerm, index, comparison)) != -1)
        {
            count++;
            index += searchTerm.Length;
        }

        return count;
    }
}

/// <summary>
/// Statistics about a file.
/// </summary>
public class FileStatistics
{
    public int LineCount { get; init; }
    public int WordCount { get; init; }
    public int CharacterCount { get; init; }
    public int NonWhitespaceCharacterCount { get; init; }
    public double AverageWordsPerLine { get; init; }
}

/// <summary>
/// Represents a line that matches a search term.
/// </summary>
public class LineMatch
{
    public int LineNumber { get; init; }
    public string Content { get; init; } = string.Empty;
    public int MatchCount { get; init; }
}
