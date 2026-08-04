using System.Text.Json;
using SpeedComparison.Analysis;

namespace Tests.Benchmark;

/// <summary>The command-line surface of the performance regression gate.</summary>
internal static class RegressionCommand
{
    private const string HistoryOption = "--history";
    private const string ResultOption = "--result";
    private const string RecordOption = "--record";
    private const string ReportOption = "--report";
    private const string SeriesOption = "--series";

    /// <summary>
    /// Compares the run in the result file against the rolling history and writes the report, the verdict,
    /// and the record the history would receive. Returns a non-zero exit code only for a confirmed
    /// regression, so a single flagged run reports without failing anything.
    /// </summary>
    public static async Task<int> AnalyzeAsync(string[] args)
    {
        var repositoryRoot = RoundRobinComparison.FindRepositoryRoot();
        var resultsDirectory = Path.Join(repositoryRoot, "tools", "speed-comparison", "results");
        var resultPath = OptionValue(args, ResultOption) ?? Path.Join(resultsDirectory, "runtime-comparison.json");
        var historyPath = OptionValue(args, HistoryOption);
        var recordPath = OptionValue(args, RecordOption) ?? Path.Join(resultsDirectory, "history-record.json");
        var reportPath = OptionValue(args, ReportOption) ?? Path.Join(resultsDirectory, "REGRESSION_REPORT.md");
        var series = ParseSeries(OptionValue(args, SeriesOption));

        if (!File.Exists(resultPath))
        {
            Console.Error.WriteLine($"Comparison result {resultPath} was not found; run --round-robin first.");
            return 1;
        }

        ComparisonResult result;
        try
        {
            result = JsonSerializer.Deserialize<ComparisonResult>(await File.ReadAllTextAsync(resultPath))
                ?? throw new InvalidDataException($"{resultPath} deserialised to null.");
        }
        catch (JsonException exception)
        {
            // The checked-in result predates the environment metadata the gate keys on, so a stale file
            // has to say so rather than surface as a bare deserialisation failure.
            Console.Error.WriteLine(
                $"{resultPath} is missing fields the regression gate requires. "
                + $"Re-run --round-robin to produce a current result. ({exception.Message})");
            return 1;
        }

        var skippedRecords = 0;
        IReadOnlyList<HistoryRecord> history = historyPath is null
            ? []
            : PerformanceHistoryStore.Read(historyPath, out skippedRecords);

        var gate = RegressionGate.Evaluate(result, history, series, skippedRecords);
        var report = RegressionReport.Render(gate);

        EnsureDirectory(reportPath);
        await File.WriteAllTextAsync(reportPath, report);
        EnsureDirectory(recordPath);
        await File.WriteAllTextAsync(recordPath, PerformanceHistoryStore.Serialize(gate.Record) + Environment.NewLine);

        Console.WriteLine(report);
        Console.WriteLine($"Regression report: {reportPath}");
        Console.WriteLine($"History record: {recordPath}");

        if (!gate.HasConfirmedRegression)
        {
            return 0;
        }

        Console.Error.WriteLine(
            "A performance regression was confirmed by two consecutive recorded runs. "
            + "See the regression report for the participants and thresholds involved.");
        return 1;
    }

    /// <summary>Appends a record produced by <see cref="AnalyzeAsync"/> to the rolling history file.</summary>
    public static async Task<int> AppendAsync(string[] args)
    {
        var historyPath = OptionValue(args, HistoryOption);
        var recordPath = OptionValue(args, RecordOption);
        if (historyPath is null || recordPath is null)
        {
            Console.Error.WriteLine($"--append-history requires {HistoryOption} and {RecordOption}.");
            return 1;
        }

        if (!File.Exists(recordPath))
        {
            Console.Error.WriteLine($"History record {recordPath} was not found.");
            return 1;
        }

        var record = PerformanceHistoryStore.Deserialize(await File.ReadAllTextAsync(recordPath));
        PerformanceHistoryStore.Append(historyPath, record);
        Console.WriteLine(
            $"Appended run {record.RunId} ({record.Commit}) to {historyPath}, "
            + $"keeping at most {PerformanceHistoryStore.MaximumRecords} runs.");
        return 0;
    }

    private static GateSeries ParseSeries(string? value) => value switch
    {
        null or "baseline" => GateSeries.Baseline,
        "pull-request" => GateSeries.PullRequest,
        _ => throw new ArgumentException($"Unknown series '{value}'; expected 'baseline' or 'pull-request'.", nameof(value))
    };

    private static void EnsureDirectory(string filePath)
    {
        if (Path.GetDirectoryName(filePath) is { Length: > 0 } directory)
        {
            Directory.CreateDirectory(directory);
        }
    }

    private static string? OptionValue(string[] args, string option)
    {
        var index = Array.IndexOf(args, option);
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }
}
