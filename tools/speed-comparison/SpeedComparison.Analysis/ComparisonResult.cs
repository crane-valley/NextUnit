namespace SpeedComparison.Analysis;

/// <summary>One measured process start, identified by the round and the execution position it occupied.</summary>
public sealed record Measurement(int Round, int Position, string Framework, double ElapsedMilliseconds);

/// <summary>Aggregate statistics for one participant of a round-robin comparison.</summary>
public sealed record FrameworkSummary(
    string Framework,
    string Version,
    int Runs,
    double MeanMilliseconds,
    double MedianMilliseconds,
    double StandardDeviationMilliseconds,
    double MinimumMilliseconds,
    double MaximumMilliseconds,
    double RelativeToNextUnit);

/// <summary>
/// The full result of one round-robin comparison, including the environment metadata the regression
/// gate needs to compare like for like and the raw per-round measurements it needs to re-analyse a run.
/// </summary>
public sealed record ComparisonResult
{
    /// <summary>Identifies the measured workload so unrelated benchmarks never share a baseline.</summary>
    public const string RoundRobinBenchmarkId = "round-robin-runtime";

    /// <summary>When the comparison finished.</summary>
    public required DateTimeOffset GeneratedAtUtc { get; init; }

    /// <summary>The workload identifier; see <see cref="RoundRobinBenchmarkId"/>.</summary>
    public required string BenchmarkId { get; init; }

    /// <summary>The hosted runner image family, for example <c>ubuntu24</c>, or <c>local</c> off CI.</summary>
    public required string RunnerImage { get; init; }

    /// <summary>The exact hosted runner image build. Recorded for diagnosis, deliberately not part of the baseline key.</summary>
    public required string RunnerImageVersion { get; init; }

    /// <summary>The operating system description reported by the runtime.</summary>
    public required string OperatingSystem { get; init; }

    /// <summary>The process architecture the executables were measured on.</summary>
    public required string Architecture { get; init; }

    /// <summary>The runtime description, for example <c>.NET 10.0.10</c>.</summary>
    public required string Runtime { get; init; }

    /// <summary>The .NET SDK version that built the participants.</summary>
    public required string DotNetSdkVersion { get; init; }

    /// <summary>The processor identifier, where the platform exposes one.</summary>
    public required string Processor { get; init; }

    /// <summary>The number of logical processors visible to the measuring process.</summary>
    public required int ProcessorCount { get; init; }

    /// <summary>The commit the measured executables were built from.</summary>
    public required string Commit { get; init; }

    /// <summary>The git reference the run was triggered on, for example <c>refs/heads/main</c>.</summary>
    public required string Reference { get; init; }

    /// <summary>The CI run identifier, or <c>local</c> off CI.</summary>
    public required string RunId { get; init; }

    /// <summary>What started the run, for example <c>schedule</c> or <c>pull_request</c>.</summary>
    public required string Trigger { get; init; }

    /// <summary>Measured rounds per participant.</summary>
    public required int Rounds { get; init; }

    /// <summary>The number of tests every participant is required to report.</summary>
    public required int ExpectedTestCount { get; init; }

    /// <summary>A prose description of the controls the measurement applied.</summary>
    public required string Methodology { get; init; }

    /// <summary>Per-participant aggregates, ordered fastest first.</summary>
    public required IReadOnlyList<FrameworkSummary> Summaries { get; init; }

    /// <summary>Every accepted measurement, in the order it was taken.</summary>
    public required IReadOnlyList<Measurement> Measurements { get; init; }
}
