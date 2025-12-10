using System.Text;
using SpeedComparison.Runner.Models;

namespace SpeedComparison.Runner;

/// <summary>
/// Generates markdown reports from benchmark results
/// </summary>
public class MarkdownGenerator
{
    public string Generate(BenchmarkResults results)
    {
        var sb = new StringBuilder();

        // Header
        sb.AppendLine("# Speed Comparison Results");
        sb.AppendLine();
        sb.AppendLine($"**Last Updated**: {results.Timestamp:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine($"**Environment**: {results.Environment.OperatingSystem} ({results.Environment.Architecture})");
        sb.AppendLine($"**.NET Version**: {results.Environment.DotNetVersion}");
        sb.AppendLine($"**Processor Count**: {results.Environment.ProcessorCount}");
        sb.AppendLine();

        // Summary table
        sb.AppendLine("## Summary");
        sb.AppendLine();
        sb.AppendLine("| Framework | Version | Total Time | Per-Test Time | Peak Memory | Tests/Sec | Relative Performance |");
        sb.AppendLine("|-----------|---------|------------|---------------|-------------|-----------|---------------------|");

        // Sort by average time (fastest first)
        var frameworks = results.Frameworks.Values.OrderBy(f => f.AverageTimeMs).ToList();
        
        foreach (var framework in frameworks)
        {
            sb.AppendLine($"| {framework.FrameworkName,-9} | {framework.Version,-7} | {framework.AverageTimeMs,7}ms | {framework.PerTestTimeMs,8:F2}ms | {framework.PeakMemoryMB,7:F1}MB | {framework.TestsPerSecond,9:F0} | {framework.PerformanceRating,-19} |");
        }
        
        sb.AppendLine();

        // Detailed results
        sb.AppendLine("## Detailed Results");
        sb.AppendLine();

        foreach (var framework in frameworks)
        {
            sb.AppendLine($"### {framework.FrameworkName} v{framework.Version}");
            sb.AppendLine();
            sb.AppendLine($"- **Test Count**: {framework.TestCount}");
            sb.AppendLine($"- **Passed**: {framework.PassedTests}");
            sb.AppendLine($"- **Failed**: {framework.FailedTests}");
            sb.AppendLine($"- **Skipped**: {framework.SkippedTests}");
            sb.AppendLine();
            sb.AppendLine("**Timing:**");
            sb.AppendLine($"- Median: {framework.MedianTimeMs}ms");
            sb.AppendLine($"- Average: {framework.AverageTimeMs}ms");
            sb.AppendLine($"- Per-test: {framework.PerTestTimeMs:F2}ms");
            sb.AppendLine($"- Throughput: {framework.TestsPerSecond:F0} tests/second");
            sb.AppendLine();
            sb.AppendLine("**Memory:**");
            sb.AppendLine($"- Median peak: {framework.MedianPeakMemoryBytes / 1024 / 1024:F1}MB");
            sb.AppendLine($"- Average peak: {framework.PeakMemoryMB:F1}MB");
            sb.AppendLine();
            sb.AppendLine($"**Raw Data (all iterations):**");
            sb.AppendLine($"- Execution times: {string.Join(", ", framework.ExecutionTimesMs.Select(t => $"{t}ms"))}");
            sb.AppendLine($"- Peak memory: {string.Join(", ", framework.PeakMemoryBytes.Select(m => $"{m / 1024 / 1024}MB"))}");
            sb.AppendLine();
        }

        // Methodology
        sb.AppendLine("## Methodology");
        sb.AppendLine();
        sb.AppendLine("### Test Suite");
        sb.AppendLine();
        sb.AppendLine("Each framework runs an identical test suite containing **200 tests**:");
        sb.AppendLine();
        sb.AppendLine("- **50 Simple Tests**: Basic assertions (Equal, True, False)");
        sb.AppendLine("- **50 Parameterized Tests**: Data-driven tests (5 methods × 10 parameters each)");
        sb.AppendLine("- **25 Lifecycle Tests**: Tests with setup/teardown hooks");
        sb.AppendLine("- **25 Async Tests**: Async/await test methods");
        sb.AppendLine("- **25 Complex Assertion Tests**: Collection, string, and numeric assertions");
        sb.AppendLine("- **25 Parallel Tests**: Tests designed to run concurrently");
        sb.AppendLine();
        sb.AppendLine("### Execution");
        sb.AppendLine();
        sb.AppendLine("- Each framework is run **5 times** in separate processes");
        sb.AppendLine("- Median and average times are calculated from all iterations");
        sb.AppendLine("- All projects built in **Release mode** with optimizations enabled");
        sb.AppendLine("- Tests run using each framework's native test runner:");
        sb.AppendLine("  - **NextUnit**: `dotnet run` (Microsoft.Testing.Platform)");
        sb.AppendLine("  - **xUnit, NUnit, MSTest**: `dotnet test` (VSTest Platform)");
        sb.AppendLine();
        sb.AppendLine("### Metrics");
        sb.AppendLine();
        sb.AppendLine("- **Total Time**: Wall-clock time from process start to completion");
        sb.AppendLine("- **Per-Test Time**: Total time ÷ test count");
        sb.AppendLine("- **Peak Memory**: Maximum working set size during execution");
        sb.AppendLine("- **Tests/Sec**: Test count ÷ (total time in seconds)");
        sb.AppendLine("- **Relative Performance**: Compared to NextUnit baseline (1.0 = same speed)");
        sb.AppendLine();
        sb.AppendLine("### Fairness");
        sb.AppendLine();
        sb.AppendLine("All test implementations:");
        sb.AppendLine("- Use identical test logic from `SpeedComparison.Shared`");
        sb.AppendLine("- Follow framework best practices (native attributes and assertions)");
        sb.AppendLine("- Include the same lifecycle hooks and async patterns");
        sb.AppendLine("- Run with parallel execution enabled (where supported)");
        sb.AppendLine();

        // Interpretation
        sb.AppendLine("## Interpretation");
        sb.AppendLine();
        sb.AppendLine("**What These Results Show:**");
        sb.AppendLine();
        sb.AppendLine("- **Execution Speed**: How quickly each framework discovers and runs tests");
        sb.AppendLine("- **Framework Overhead**: Per-test time indicates framework overhead beyond test logic");
        sb.AppendLine("- **Memory Efficiency**: Peak memory usage during test execution");
        sb.AppendLine("- **Scalability**: How each framework handles 200 tests");
        sb.AppendLine();
        sb.AppendLine("**What These Results Don't Show:**");
        sb.AppendLine();
        sb.AppendLine("- Test writing experience, API ergonomics, or developer productivity");
        sb.AppendLine("- Feature completeness or ecosystem maturity");
        sb.AppendLine("- Performance on test suites with different characteristics");
        sb.AppendLine("- Native AOT compilation support or startup time differences");
        sb.AppendLine();
        sb.AppendLine("**Performance is only one factor in choosing a test framework.** Consider features, ecosystem, team familiarity, and specific project requirements.");
        sb.AppendLine();

        // Footer
        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine("**Generated by**: NextUnit Speed Comparison Tool");
        sb.AppendLine($"**Report Date**: {results.Timestamp:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine();

        return sb.ToString();
    }

    public string GenerateConsoleSummary(BenchmarkResults results)
    {
        var sb = new StringBuilder();

        sb.AppendLine();
        sb.AppendLine("=== Benchmark Summary ===");
        sb.AppendLine();

        var frameworks = results.Frameworks.Values.OrderBy(f => f.AverageTimeMs).ToList();

        sb.AppendLine($"{"Framework",-12} {"Version",-8} {"Avg Time",-10} {"Per-Test",-12} {"Memory",-10} {"Tests/Sec",-12} {"Relative",-15}");
        sb.AppendLine(new string('-', 90));

        foreach (var framework in frameworks)
        {
            sb.AppendLine($"{framework.FrameworkName,-12} {framework.Version,-8} {framework.AverageTimeMs}ms {framework.PerTestTimeMs:F2}ms {framework.PeakMemoryMB:F1}MB {framework.TestsPerSecond:F0} {framework.PerformanceRating,-15}");
        }

        sb.AppendLine();

        return sb.ToString();
    }
}
