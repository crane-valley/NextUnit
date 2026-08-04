namespace SpeedComparison.Analysis.Tests;

/// <summary>
/// Builds comparison runs and recorded runs from explicit per-round samples, so a test states the timing
/// behaviour it is describing and nothing else.
/// </summary>
internal static class SyntheticRuns
{
    /// <summary>The reference participants every synthetic run carries, with their undisturbed level.</summary>
    private static readonly (string Framework, double Milliseconds)[] _references =
    [
        ("TUnit", 380.0),
        ("NUnit", 510.0),
        ("MSTest", 440.0),
        ("xUnit", 470.0)
    ];

    /// <summary>The participant the gate judges.</summary>
    public const string Subject = "NextUnit";

    /// <summary>
    /// Produces per-round samples for one run. <paramref name="subjectMilliseconds"/> sets the subject's
    /// level and <paramref name="machineFactor"/> scales every participant, which is how a slow runner
    /// looks: everything moves together.
    /// </summary>
    public static Dictionary<string, double[]> Samples(
        int seed,
        int rounds = 21,
        double subjectMilliseconds = 300.0,
        double machineFactor = 1.0,
        double noiseFraction = 0.02)
    {
        var random = new Random(seed);
        var samples = new Dictionary<string, double[]>(StringComparer.Ordinal)
        {
            [Subject] = Series(random, subjectMilliseconds * machineFactor, rounds, noiseFraction)
        };
        foreach (var (framework, milliseconds) in _references)
        {
            samples[framework] = Series(random, milliseconds * machineFactor, rounds, noiseFraction);
        }

        return samples;
    }

    /// <summary>Wraps samples in the comparison result the gate consumes.</summary>
    public static ComparisonResult Result(
        IReadOnlyDictionary<string, double[]> samples,
        string runId = "current",
        DateTimeOffset? generatedAtUtc = null,
        string sdkVersion = "10.0.302",
        string runtime = ".NET 10.0.10",
        string runnerImage = "ubuntu24")
    {
        var measurements = new List<Measurement>();
        foreach (var (framework, values) in samples)
        {
            for (var round = 0; round < values.Length; round++)
            {
                measurements.Add(new Measurement(round + 1, 1, framework, values[round]));
            }
        }

        return new ComparisonResult
        {
            GeneratedAtUtc = generatedAtUtc ?? new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero),
            BenchmarkId = ComparisonResult.RoundRobinBenchmarkId,
            MetricRevision = ComparisonResult.CurrentMetricRevision,
            RunnerImage = runnerImage,
            RunnerImageVersion = "20260728.1.0",
            OperatingSystem = "Ubuntu 24.04.4 LTS",
            Architecture = "X64",
            Runtime = runtime,
            DotNetSdkVersion = sdkVersion,
            Processor = "X64",
            ProcessorCount = 4,
            Commit = "0123456789abcdef0123456789abcdef01234567",
            Reference = "refs/heads/main",
            RunId = runId,
            Trigger = "schedule",
            Rounds = samples.Values.Max(values => values.Length),
            ExpectedTestCount = 127,
            Methodology = "synthetic",
            Summaries = samples.Select(entry => new FrameworkSummary(
                entry.Key,
                "1.0.0",
                entry.Value.Length,
                entry.Value.Average(),
                RobustStatistics.Median(entry.Value),
                0,
                entry.Value.Min(),
                entry.Value.Max(),
                1)).ToArray(),
            Measurements = measurements
        };
    }

    /// <summary>Builds a baseline of recorded runs that differ only by ordinary noise.</summary>
    public static List<HistoryRecord> Baseline(
        int count,
        double subjectMilliseconds = 300.0,
        RegressionVerdict verdict = RegressionVerdict.Stable,
        int firstSeed = 1)
    {
        var records = new List<HistoryRecord>(count);
        for (var index = 0; index < count; index++)
        {
            var samples = Samples(firstSeed + index, subjectMilliseconds: subjectMilliseconds);
            records.Add(Record(
                samples,
                runId: $"baseline-{index}",
                generatedAtUtc: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero).AddDays(7.0 * index),
                verdict: verdict));
        }

        return records;
    }

    /// <summary>Wraps samples in the history record the store would have written for them.</summary>
    public static HistoryRecord Record(
        IReadOnlyDictionary<string, double[]> samples,
        string runId,
        DateTimeOffset generatedAtUtc,
        RegressionVerdict verdict = RegressionVerdict.Stable,
        string sdkVersion = "10.0.302",
        string runtime = ".NET 10.0.10",
        string runnerImage = "ubuntu24")
        => new()
        {
            SchemaVersion = HistoryRecord.CurrentSchemaVersion,
            GeneratedAtUtc = generatedAtUtc,
            BenchmarkId = ComparisonResult.RoundRobinBenchmarkId,
            MetricRevision = ComparisonResult.CurrentMetricRevision,
            Commit = "0123456789abcdef0123456789abcdef01234567",
            Reference = "refs/heads/main",
            RunId = runId,
            Trigger = "schedule",
            Rounds = samples.Values.Max(values => values.Length),
            ExpectedTestCount = 127,
            Environment = new HistoryEnvironment
            {
                RunnerImage = runnerImage,
                RunnerImageVersion = "20260728.1.0",
                OperatingSystem = "Ubuntu 24.04.4 LTS",
                Architecture = "X64",
                Processor = "X64",
                ProcessorCount = 4,
                SdkVersion = sdkVersion,
                RuntimeVersion = runtime
            },
            Participants = samples.Select(entry => new HistoryParticipant
            {
                Framework = entry.Key,
                Version = "1.0.0",
                MedianMilliseconds = RobustStatistics.Median(entry.Value),
                NormalizedMedian = 0,
                Verdict = RegressionGate.IsGated(entry.Key) ? verdict : RegressionVerdict.NotEvaluated,
                SamplesMilliseconds = entry.Value
            }).ToArray()
        };

    private static double[] Series(Random random, double level, int rounds, double noiseFraction)
    {
        var values = new double[rounds];
        for (var round = 0; round < rounds; round++)
        {
            values[round] = level * (1.0 + (noiseFraction * ((random.NextDouble() * 2) - 1)));
        }

        return values;
    }
}
