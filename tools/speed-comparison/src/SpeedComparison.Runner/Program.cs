using System.Text.Json;
using SpeedComparison.Runner;

// Get solution root directory
var solutionRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

Console.WriteLine("NextUnit Speed Comparison Tool");
Console.WriteLine("==============================");
Console.WriteLine();

// Run benchmarks
var runner = new BenchmarkRunner(solutionRoot);
var results = await runner.RunBenchmarksAsync();

// Display console summary
var markdownGen = new MarkdownGenerator();
Console.WriteLine(markdownGen.GenerateConsoleSummary(results));

// Generate markdown report
var markdown = markdownGen.Generate(results);
var resultsDir = Path.Combine(solutionRoot, "results");
Directory.CreateDirectory(resultsDir);

var markdownPath = Path.Combine(resultsDir, "BENCHMARK_RESULTS.md");
await File.WriteAllTextAsync(markdownPath, markdown);
Console.WriteLine($"Markdown report written to: {markdownPath}");

// Save JSON results
var jsonPath = Path.Combine(resultsDir, "history", $"{results.Timestamp:yyyy-MM-dd_HHmmss}_results.json");
Directory.CreateDirectory(Path.GetDirectoryName(jsonPath)!);

var jsonOptions = new JsonSerializerOptions
{
    WriteIndented = true,
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
};

var json = JsonSerializer.Serialize(results, jsonOptions);
await File.WriteAllTextAsync(jsonPath, json);
Console.WriteLine($"JSON results written to: {jsonPath}");

Console.WriteLine();
Console.WriteLine("Benchmark completed successfully!");
return 0;
