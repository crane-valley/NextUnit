using System.Text.Json;
using System.Text.Json.Serialization;

namespace SpeedComparison.Analysis;

/// <summary>The environment a run was measured in. Everything here is recorded; only part of it keys the baseline.</summary>
public sealed record HistoryEnvironment
{
    /// <summary>The hosted runner image family, for example <c>ubuntu24</c>.</summary>
    public required string RunnerImage { get; init; }

    /// <summary>The exact hosted runner image build.</summary>
    public required string RunnerImageVersion { get; init; }

    /// <summary>The operating system description reported by the runtime.</summary>
    public required string OperatingSystem { get; init; }

    /// <summary>The process architecture.</summary>
    public required string Architecture { get; init; }

    /// <summary>The processor identifier, where the platform exposes one.</summary>
    public required string Processor { get; init; }

    /// <summary>The number of logical processors.</summary>
    public required int ProcessorCount { get; init; }

    /// <summary>The .NET SDK version that built the participants.</summary>
    public required string SdkVersion { get; init; }

    /// <summary>The runtime description the participants executed on.</summary>
    public required string RuntimeVersion { get; init; }
}

/// <summary>One participant of a recorded run, with the raw samples needed to re-analyse it later.</summary>
public sealed record HistoryParticipant
{
    /// <summary>The participant name, for example <c>NextUnit (AOT)</c>.</summary>
    public required string Framework { get; init; }

    /// <summary>The participant's framework version.</summary>
    public required string Version { get; init; }

    /// <summary>The median wall-clock time of the participant in this run.</summary>
    public required double MedianMilliseconds { get; init; }

    /// <summary>The median of the per-round reference-normalised samples in this run.</summary>
    public required double NormalizedMedian { get; init; }

    /// <summary>The verdict this run reached for the participant, which later runs read to require a repeat.</summary>
    public required RegressionVerdict Verdict { get; init; }

    /// <summary>Wall-clock samples in round order, so index <c>i</c> is round <c>i + 1</c> for every participant.</summary>
    public required IReadOnlyList<double> SamplesMilliseconds { get; init; }
}

/// <summary>One appended line of the rolling performance history.</summary>
public sealed record HistoryRecord
{
    /// <summary>The schema readers accept; a reader skips any other version instead of failing the gate.</summary>
    public const int CurrentSchemaVersion = 1;

    /// <summary>The schema this record was written with.</summary>
    public required int SchemaVersion { get; init; }

    /// <summary>When the run finished.</summary>
    public required DateTimeOffset GeneratedAtUtc { get; init; }

    /// <summary>The measured workload identifier.</summary>
    public required string BenchmarkId { get; init; }

    /// <summary>The commit the measured executables were built from.</summary>
    public required string Commit { get; init; }

    /// <summary>The git reference the run was triggered on.</summary>
    public required string Reference { get; init; }

    /// <summary>The CI run identifier.</summary>
    public required string RunId { get; init; }

    /// <summary>What started the run.</summary>
    public required string Trigger { get; init; }

    /// <summary>Measured rounds per participant.</summary>
    public required int Rounds { get; init; }

    /// <summary>The number of tests every participant reported.</summary>
    public required int ExpectedTestCount { get; init; }

    /// <summary>The environment the run was measured in.</summary>
    public required HistoryEnvironment Environment { get; init; }

    /// <summary>Every participant of the run.</summary>
    public required IReadOnlyList<HistoryParticipant> Participants { get; init; }
}

/// <summary>
/// Reads and appends the rolling history. The store is a JSON Lines file so appending is a single line,
/// a corrupted line is isolated to one run, and a reviewer can diff two runs without a tool.
/// </summary>
public static class PerformanceHistoryStore
{
    /// <summary>
    /// The number of runs the file keeps. At the weekly cadence this is roughly two years, and it stays
    /// far above the baseline window even if a second runner image is added later.
    /// </summary>
    public const int MaximumRecords = 100;

    /// <summary>Serialisation is deliberately compact so one record occupies exactly one line.</summary>
    private static readonly JsonSerializerOptions _lineOptions = new()
    {
        WriteIndented = false,
        Converters = { new JsonStringEnumConverter() }
    };

    /// <summary>Reads every record the current schema understands, oldest first.</summary>
    /// <param name="path">The JSON Lines file. A missing file reads as an empty history.</param>
    /// <param name="skippedRecords">The number of lines written by another schema version.</param>
    public static IReadOnlyList<HistoryRecord> Read(string path, out int skippedRecords)
    {
        skippedRecords = 0;
        if (!File.Exists(path))
        {
            return [];
        }

        var records = new List<HistoryRecord>();
        var lineNumber = 0;
        foreach (var line in File.ReadLines(path))
        {
            lineNumber++;
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            int schemaVersion;
            try
            {
                using var document = JsonDocument.Parse(line);
                schemaVersion = document.RootElement.TryGetProperty(nameof(HistoryRecord.SchemaVersion), out var version)
                    ? version.GetInt32()
                    : 0;
            }
            catch (JsonException exception)
            {
                throw new InvalidDataException(
                    $"{path} line {lineNumber} is not valid JSON.",
                    exception);
            }

            if (schemaVersion != HistoryRecord.CurrentSchemaVersion)
            {
                skippedRecords++;
                continue;
            }

            var record = JsonSerializer.Deserialize<HistoryRecord>(line, _lineOptions)
                ?? throw new InvalidDataException($"{path} line {lineNumber} deserialised to null.");
            records.Add(record);
        }

        return records;
    }

    /// <summary>Serialises one record as the single line the store appends.</summary>
    public static string Serialize(HistoryRecord record) => JsonSerializer.Serialize(record, _lineOptions);

    /// <summary>Deserialises a record written by <see cref="Serialize"/>.</summary>
    public static HistoryRecord Deserialize(string json)
        => JsonSerializer.Deserialize<HistoryRecord>(json, _lineOptions)
            ?? throw new InvalidDataException("The history record deserialised to null.");

    /// <summary>
    /// Appends <paramref name="record"/> and trims the file to the most recent <see cref="MaximumRecords"/>
    /// lines. Trimming keeps the store bounded without needing a retention job.
    /// <para>
    /// The append replaces any record already stored for the same run and benchmark rather than adding a
    /// second copy. A publishing job that retries after an uncertain push would otherwise record the same
    /// run twice, and the baseline would count one measurement as two independent observations.
    /// </para>
    /// </summary>
    public static void Append(string path, HistoryRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        // Existing lines are carried across verbatim, including any the current schema skips, so an
        // append never destroys a record this build happens not to understand.
        var lines = File.Exists(path)
            ? File.ReadAllLines(path)
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .Where(line => !DescribesSameRun(line, record))
                .ToList()
            : [];
        lines.Add(Serialize(record));
        if (lines.Count > MaximumRecords)
        {
            lines.RemoveRange(0, lines.Count - MaximumRecords);
        }

        File.WriteAllLines(path, lines);
    }

    /// <summary>
    /// Whether a stored line describes the same run and benchmark as <paramref name="record"/>. The check
    /// reads the two identifying properties directly so it works on any schema version that carries them.
    /// </summary>
    private static bool DescribesSameRun(string line, HistoryRecord record)
    {
        try
        {
            using var document = JsonDocument.Parse(line);
            return document.RootElement.TryGetProperty(nameof(HistoryRecord.RunId), out var runId)
                && document.RootElement.TryGetProperty(nameof(HistoryRecord.BenchmarkId), out var benchmarkId)
                && runId.ValueKind == JsonValueKind.String
                && benchmarkId.ValueKind == JsonValueKind.String
                && string.Equals(runId.GetString(), record.RunId, StringComparison.Ordinal)
                && string.Equals(benchmarkId.GetString(), record.BenchmarkId, StringComparison.Ordinal);
        }
        catch (JsonException)
        {
            // Read reports a malformed line with its location; an append must not silently drop it.
            return false;
        }
    }
}

/// <summary>Computes the key that decides which recorded runs a candidate run may be compared against.</summary>
public static class BaselineKey
{
    /// <summary>Builds the key from a recorded run.</summary>
    public static string For(HistoryRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        return Compose(
            record.BenchmarkId,
            record.ExpectedTestCount,
            record.Rounds,
            record.Environment.RunnerImage,
            record.Environment.Architecture,
            record.Environment.SdkVersion,
            record.Environment.RuntimeVersion,
            record.Participants.Select(participant => participant.Framework));
    }

    /// <summary>Builds the key from a freshly measured run.</summary>
    public static string For(ComparisonResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return Compose(
            result.BenchmarkId,
            result.ExpectedTestCount,
            result.Rounds,
            result.RunnerImage,
            result.Architecture,
            result.DotNetSdkVersion,
            result.Runtime,
            result.Summaries.Select(summary => summary.Framework));
    }

    private static string Compose(
        string benchmarkId,
        int expectedTestCount,
        int rounds,
        string runnerImage,
        string architecture,
        string sdkVersion,
        string runtimeVersion,
        IEnumerable<string> frameworks)
    {
        // Only participants that are not gated enter the key. They form the reference the metric is
        // normalised against, so changing that set changes what the numbers mean; the gated participants
        // are expected to come and go with the product and must not split the baseline.
        var references = frameworks
            .Where(framework => !RegressionGate.IsGated(framework))
            .Order(StringComparer.Ordinal);
        return string.Join(
            " | ",
            benchmarkId,
            // Resizing the suite changes the mix of startup, scheduling, and execution the ratio measures,
            // so runs of a differently sized workload are not comparable however stable the machine was.
            FormattableString.Invariant($"{expectedTestCount} tests"),
            // A dispatch may legally ask for fewer rounds than the schedule uses. Those runs carry too few
            // samples for the rank test to decide anything, so they must form their own series rather than
            // land in the normal one as an unearned Stable and displace a suspected run from the chain.
            FormattableString.Invariant($"{rounds} rounds"),
            runnerImage,
            architecture,
            $"sdk {MajorMinor(sdkVersion)}",
            $"runtime {MajorMinor(runtimeVersion)}",
            $"references {string.Join(", ", references)}");
    }

    /// <summary>
    /// Reduces a version to major.minor. Patch-level churn on a hosted runner would otherwise reset the
    /// baseline almost every week, and the metric is already normalised against references measured on the
    /// same machine, so a patch bump does not change what a comparison means.
    /// </summary>
    private static string MajorMinor(string version)
    {
        var start = version.AsSpan().IndexOfAnyInRange('0', '9');
        if (start < 0)
        {
            return version;
        }

        var end = start;
        var separatorSeen = false;
        while (end < version.Length)
        {
            var character = version[end];
            if (char.IsAsciiDigit(character))
            {
                end++;
            }
            else if (character == '.' && !separatorSeen && end + 1 < version.Length && char.IsAsciiDigit(version[end + 1]))
            {
                separatorSeen = true;
                end++;
            }
            else
            {
                break;
            }
        }

        return version[start..end];
    }
}
