using NextUnit;

namespace Console.Sample.Tests;

/// <summary>
/// Tests for the FileProcessor class.
/// </summary>
public class FileProcessorTests
{
    private readonly FileProcessor _processor = new();

    [Test]
    public void AnalyzeFile_EmptyContent_ReturnsZeroStatistics()
    {
        var result = _processor.AnalyzeFile("");

        Assert.Equal(0, result.LineCount);
        Assert.Equal(0, result.WordCount);
        Assert.Equal(0, result.CharacterCount);
    }

    [Test]
    public void AnalyzeFile_SingleLine_ReturnsCorrectStatistics()
    {
        var content = "Hello world";

        var result = _processor.AnalyzeFile(content);

        Assert.Equal(1, result.LineCount);
        Assert.Equal(2, result.WordCount);
        Assert.Equal(11, result.CharacterCount);
        Assert.Equal(10, result.NonWhitespaceCharacterCount);
    }

    [Test]
    public void AnalyzeFile_MultipleLines_ReturnsCorrectStatistics()
    {
        var content = "Line one\nLine two\nLine three";

        var result = _processor.AnalyzeFile(content);

        Assert.Equal(3, result.LineCount);
        Assert.Equal(6, result.WordCount);
        Assert.Equal(2.0, result.AverageWordsPerLine);
    }

    [Test]
    public void FilterLines_EmptyContent_ReturnsEmptySequence()
    {
        var result = _processor.FilterLines("", line => true);

        Assert.Empty(result);
    }

    [Test]
    public void FilterLines_WithPredicate_ReturnsFilteredLines()
    {
        var content = "keep\nremove\nkeep\nremove";

        var result = _processor.FilterLines(content, line => line.StartsWith("keep"));

        Assert.Equal(2, result.Count());
        Assert.All(result, line => Assert.True(line.StartsWith("keep")));
    }

    [Test]
    public void SearchLines_EmptyContent_ReturnsEmptyResults()
    {
        var result = _processor.SearchLines("", "search");

        Assert.Empty(result);
    }

    [Test]
    public void SearchLines_CaseSensitive_FindsMatches()
    {
        var content = "Hello World\nhello world\nHELLO WORLD";

        var result = _processor.SearchLines(content, "Hello", caseSensitive: true);

        Assert.Equal(1, result.Count());
        Assert.Equal(1, result.First().LineNumber);
    }

    [Test]
    public void SearchLines_CaseInsensitive_FindsAllMatches()
    {
        var content = "Hello World\nhello world\nHELLO WORLD";

        var result = _processor.SearchLines(content, "Hello", caseSensitive: false);

        Assert.Equal(3, result.Count());
    }

    [Test]
    public void SearchLines_MultipleMatches_CountsCorrectly()
    {
        var content = "test test test";

        var result = _processor.SearchLines(content, "test");
        var match = result.First();

        Assert.Equal(1, match.LineNumber);
        Assert.Equal(3, match.MatchCount);
    }
}
