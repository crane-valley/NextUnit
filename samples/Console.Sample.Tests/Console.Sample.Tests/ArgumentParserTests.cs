using NextUnit;

namespace Console.Sample.Tests;

/// <summary>
/// Tests for the ArgumentParser class.
/// </summary>
public class ArgumentParserTests
{
    private readonly ArgumentParser _parser = new();

    [Test]
    public void Parse_EmptyArgs_ReturnsValidArguments()
    {
        var result = _parser.Parse(Array.Empty<string>());

        Assert.True(result.IsValid);
        Assert.Empty(result.InputFiles);
    }

    [Test]
    public void Parse_HelpFlag_SetsShowHelp()
    {
        var result = _parser.Parse(new[] { "--help" });

        Assert.True(result.ShowHelp);
    }

    [Test]
    public void Parse_ShortHelpFlag_SetsShowHelp()
    {
        var result = _parser.Parse(new[] { "-h" });

        Assert.True(result.ShowHelp);
    }

    [Test]
    public void Parse_VerboseFlag_SetsVerbose()
    {
        var result = _parser.Parse(new[] { "--verbose" });

        Assert.True(result.Verbose);
    }

    [Test]
    public void Parse_OutputOption_SetsOutputPath()
    {
        var result = _parser.Parse(new[] { "--output", "output.txt" });

        Assert.Equal("output.txt", result.OutputPath);
    }

    [Test]
    public void Parse_OutputOptionMissingValue_AddsError()
    {
        var result = _parser.Parse(new[] { "--output" });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("Missing value for --output"));
    }

    [Test]
    public void Parse_FormatOption_SetsFormat()
    {
        var result = _parser.Parse(new[] { "--format", "json" });

        Assert.Equal("json", result.Format);
    }

    [Test]
    public void Parse_InputFiles_AddsToInputFilesList()
    {
        var result = _parser.Parse(new[] { "file1.txt", "file2.txt" });

        Assert.Equal(2, result.InputFiles.Count);
        Assert.Contains("file1.txt", result.InputFiles);
        Assert.Contains("file2.txt", result.InputFiles);
    }

    [Test]
    public void Parse_UnknownOption_AddsError()
    {
        var result = _parser.Parse(new[] { "--unknown" });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("Unknown option: --unknown"));
    }

    [Test]
    public void Parse_MixedOptions_ParsesCorrectly()
    {
        var result = _parser.Parse(new[] { "-v", "--output", "out.txt", "input.txt" });

        Assert.True(result.Verbose);
        Assert.Equal("out.txt", result.OutputPath);
        Assert.Contains("input.txt", result.InputFiles);
        Assert.True(result.IsValid);
    }

    /// <summary>
    /// Demonstrates parameterized tests for different flag variations.
    /// </summary>
    [Test]
    [TestData(nameof(FlagTestCases))]
    public void Parse_VariousFlags_SetsCorrectProperty(string[] args, string propertyName, bool expectedValue)
    {
        var result = _parser.Parse(args);

        var property = typeof(ParsedArguments).GetProperty(propertyName);
        var actualValue = (bool)property!.GetValue(result)!;

        Assert.Equal(expectedValue, actualValue);
    }

    public static IEnumerable<object[]> FlagTestCases()
    {
        yield return new object[] { new[] { "-h" }, "ShowHelp", true };
        yield return new object[] { new[] { "--help" }, "ShowHelp", true };
        yield return new object[] { new[] { "-v" }, "Verbose", true };
        yield return new object[] { new[] { "--verbose" }, "Verbose", true };
        yield return new object[] { Array.Empty<string>(), "ShowHelp", false };
        yield return new object[] { Array.Empty<string>(), "Verbose", false };
    }
}
