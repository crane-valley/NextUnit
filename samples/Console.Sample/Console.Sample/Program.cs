using Console.Sample;

// Simple console application to demonstrate testing
var parser = new ArgumentParser();
var processor = new FileProcessor();

var options = parser.Parse(args);

if (options.ShowHelp || args.Length == 0)
{
    ShowHelp();
    return 0;
}

if (!options.IsValid)
{
    foreach (var error in options.Errors)
    {
        System.Console.Error.WriteLine($"Error: {error}");
    }
    return 1;
}

if (options.InputFiles.Count == 0)
{
    System.Console.Error.WriteLine("Error: No input files specified");
    return 1;
}

// Process files
foreach (var filePath in options.InputFiles)
{
    if (!File.Exists(filePath))
    {
        System.Console.Error.WriteLine($"Error: File not found: {filePath}");
        continue;
    }

    var content = File.ReadAllText(filePath);
    var stats = processor.AnalyzeFile(content);

    System.Console.WriteLine($"\nFile: {filePath}");
    System.Console.WriteLine($"  Lines: {stats.LineCount}");
    System.Console.WriteLine($"  Words: {stats.WordCount}");
    System.Console.WriteLine($"  Characters: {stats.CharacterCount}");
    System.Console.WriteLine($"  Non-whitespace: {stats.NonWhitespaceCharacterCount}");
    System.Console.WriteLine($"  Avg words/line: {stats.AverageWordsPerLine:F2}");

    if (options.Verbose)
    {
        System.Console.WriteLine("  (Verbose mode enabled)");
    }
}

return 0;

static void ShowHelp()
{
    System.Console.WriteLine("Console.Sample - File analysis tool");
    System.Console.WriteLine();
    System.Console.WriteLine("Usage: Console.Sample [options] <files>");
    System.Console.WriteLine();
    System.Console.WriteLine("Options:");
    System.Console.WriteLine("  -h, --help        Show this help message");
    System.Console.WriteLine("  -v, --verbose     Enable verbose output");
    System.Console.WriteLine("  -o, --output      Specify output file");
    System.Console.WriteLine("  -f, --format      Specify output format");
}
