namespace SpeedComparison.Analysis;

/// <summary>What the gate concluded about one participant of one run.</summary>
public enum RegressionVerdict
{
    /// <summary>The participant is a reference rather than a gate subject.</summary>
    NotEvaluated,

    /// <summary>Too few comparable runs have been recorded to decide anything.</summary>
    InsufficientBaseline,

    /// <summary>The run matches its baseline.</summary>
    Stable,

    /// <summary>The run is faster than its baseline by more than noise explains.</summary>
    Improved,

    /// <summary>This run regressed, but no earlier recorded run did, so the regression is unconfirmed.</summary>
    Suspected,

    /// <summary>This run and the recorded run before it both regressed. This is the only failing verdict.</summary>
    Confirmed
}

/// <summary>Which line of runs a comparison belongs to.</summary>
public enum GateSeries
{
    /// <summary>A default-branch run. It appends to the history and can therefore confirm a repeat.</summary>
    Baseline,

    /// <summary>
    /// A run that reads the history without appending, and therefore never reaches a failing verdict.
    /// Pull requests are the usual case; a manual dispatch outside the default branch behaves the same way.
    /// </summary>
    PullRequest
}

/// <summary>The gate's finding for one participant.</summary>
public sealed record ParticipantAssessment
{
    /// <summary>The participant name.</summary>
    public required string Framework { get; init; }

    /// <summary>The conclusion for this participant.</summary>
    public required RegressionVerdict Verdict { get; init; }

    /// <summary>The median of this run's per-round normalised samples.</summary>
    public required double CurrentNormalizedMedian { get; init; }

    /// <summary>The median of the baseline runs' normalised medians, or zero without a baseline.</summary>
    public required double BaselineNormalizedMedian { get; init; }

    /// <summary>The change relative to the baseline, where a positive value means slower.</summary>
    public required double RelativeChange { get; init; }

    /// <summary>The run-to-run spread of the baseline, estimated robustly.</summary>
    public required double BaselineRobustDeviation { get; init; }

    /// <summary>The one-sided probability that this run is slower only by chance.</summary>
    public required double RegressionProbability { get; init; }

    /// <summary>The one-sided probability that this run is faster only by chance.</summary>
    public required double ImprovementProbability { get; init; }

    /// <summary>How many recorded runs the baseline was built from.</summary>
    public required int BaselineRunCount { get; init; }

    /// <summary>The verdict the most recent recorded run reached for this participant.</summary>
    public required RegressionVerdict PreviousVerdict { get; init; }
}

/// <summary>Everything the gate produced for one run.</summary>
public sealed record GateResult
{
    /// <summary>The key that selected the comparable runs.</summary>
    public required string BaselineKey { get; init; }

    /// <summary>The series the run belongs to.</summary>
    public required GateSeries Series { get; init; }

    /// <summary>How many recorded runs matched the key.</summary>
    public required int BaselineRunCount { get; init; }

    /// <summary>How many recorded lines were written by a different schema version and therefore skipped.</summary>
    public required int SkippedRecordCount { get; init; }

    /// <summary>
    /// Reference frameworks whose version differs from the most recent comparable run, as
    /// <c>name old -&gt; new</c>. The references are the denominator of the metric, so an upgrade among them
    /// shifts every gated participant at once and is the first thing to check when a finding is surprising.
    /// </summary>
    public required IReadOnlyList<string> ReferenceVersionChanges { get; init; }

    /// <summary>The finding for every gated participant.</summary>
    public required IReadOnlyList<ParticipantAssessment> Assessments { get; init; }

    /// <summary>The record this run would append to the history.</summary>
    public required HistoryRecord Record { get; init; }

    /// <summary>Whether any gated participant reached <see cref="RegressionVerdict.Confirmed"/>.</summary>
    public bool HasConfirmedRegression
        => Assessments.Any(assessment => assessment.Verdict == RegressionVerdict.Confirmed);
}

/// <summary>
/// Decides whether a round-robin run regressed against the runs recorded before it.
/// <para>
/// The gate measures each participant against the other participants of the same round rather than
/// against wall-clock time. Every participant runs once per round on the same machine within a couple of
/// seconds, so a slow runner, a noisy neighbour, or a different hosted image moves all of them together
/// and cancels out of the ratio. What survives is a change specific to the participant under test.
/// </para>
/// <para>
/// A finding has to clear three independent bars and then repeat. It must be large enough to matter, it
/// must exceed the run-to-run spread the baseline actually exhibits, and it must be improbable under a
/// distribution-free rank test. Only when the recorded run before it also regressed does the gate fail,
/// which is what keeps one unlucky median from turning the build red.
/// </para>
/// </summary>
public static class RegressionGate
{
    /// <summary>
    /// How many recorded runs form the baseline. At the weekly cadence this is about five months, long
    /// enough to characterise run-to-run spread and short enough to follow deliberate performance work.
    /// </summary>
    public const int BaselineWindow = 20;

    /// <summary>
    /// The baseline stays disarmed below this many runs. Two runs cannot show a spread, so the gate would
    /// be guessing at what counts as noise.
    /// </summary>
    public const int MinimumBaselineRuns = 3;

    /// <summary>
    /// The one-sided significance level. It is deliberately stricter than the customary 0.05 because the
    /// pooled samples within a run are correlated, which flatters the rank test.
    /// </summary>
    public const double SignificanceLevel = 0.01;

    /// <summary>
    /// The smallest change worth acting on. The workload is startup-heavy and measured end to end, so a
    /// change below this is not distinguishable from ordinary variation in process start cost.
    /// </summary>
    public const double MinimumRelativeChange = 0.05;

    /// <summary>
    /// How many robust standard deviations of the observed run-to-run spread a change must exceed. Three
    /// keeps a baseline that happens to be noisy from arming a gate it cannot support.
    /// </summary>
    public const double RobustDeviationMultiple = 3.0;

    /// <summary>The smallest sample count the normal approximation of the rank test is used at.</summary>
    public const int MinimumComparableSamples = 8;

    /// <summary>
    /// Whether a participant is a gate subject rather than a reference. Only this project's own
    /// participants are gated; the competing frameworks are pinned dependencies that supply the reference.
    /// </summary>
    public static bool IsGated(string framework)
        => framework.StartsWith("NextUnit", StringComparison.Ordinal);

    /// <summary>Evaluates <paramref name="result"/> against <paramref name="history"/>.</summary>
    /// <param name="result">The run that has just been measured.</param>
    /// <param name="history">Previously recorded runs, in any order.</param>
    /// <param name="series">Which line of runs this run belongs to.</param>
    /// <param name="skippedRecordCount">Lines the history reader could not interpret.</param>
    public static GateResult Evaluate(
        ComparisonResult result,
        IReadOnlyList<HistoryRecord> history,
        GateSeries series,
        int skippedRecordCount = 0)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(history);

        var key = BaselineKey.For(result);
        var currentSamples = SamplesByFramework(result);
        var references = currentSamples.Keys.Where(framework => !IsGated(framework)).Order(StringComparer.Ordinal).ToArray();
        var normalized = Normalize(currentSamples, references);

        var baseline = history
            .Where(record => record.RunId != result.RunId)
            .Where(record => string.Equals(BaselineKey.For(record), key, StringComparison.Ordinal))
            .OrderBy(record => record.GeneratedAtUtc)
            .TakeLast(BaselineWindow)
            .ToArray();

        var assessments = new List<ParticipantAssessment>();
        foreach (var summary in result.Summaries.Where(summary => IsGated(summary.Framework)))
        {
            assessments.Add(Assess(summary.Framework, normalized, baseline, series));
        }

        return new GateResult
        {
            BaselineKey = key,
            Series = series,
            BaselineRunCount = baseline.Length,
            SkippedRecordCount = skippedRecordCount,
            ReferenceVersionChanges = ReferenceVersionChanges(result, baseline),
            Assessments = assessments,
            Record = BuildRecord(result, currentSamples, normalized, assessments)
        };
    }

    /// <summary>
    /// Names the reference frameworks whose version moved since the most recent comparable run.
    /// <para>
    /// Reference versions are deliberately not part of the baseline key. They are dependency-managed and
    /// move often, so keying on them would retire the baseline before it could arm. The metric is also
    /// resistant to them: a single reference is one of several inside a geometric mean, so even a large
    /// upgrade to one of them moves the denominator by a fraction of its own change. What is left is a
    /// residual risk worth naming in the report rather than one worth disarming the gate over.
    /// </para>
    /// </summary>
    private static IReadOnlyList<string> ReferenceVersionChanges(
        ComparisonResult result,
        IReadOnlyList<HistoryRecord> baseline)
    {
        if (baseline.Count == 0)
        {
            return [];
        }

        var previous = baseline[^1].Participants.ToDictionary(
            participant => participant.Framework,
            participant => participant.Version,
            StringComparer.Ordinal);

        return result.Summaries
            .Where(summary => !IsGated(summary.Framework))
            .Where(summary => previous.TryGetValue(summary.Framework, out var version)
                && !string.Equals(version, summary.Version, StringComparison.Ordinal))
            .Select(summary => $"{summary.Framework} {previous[summary.Framework]} -> {summary.Version}")
            .ToArray();
    }

    private static ParticipantAssessment Assess(
        string framework,
        IReadOnlyDictionary<string, double[]> normalized,
        IReadOnlyList<HistoryRecord> baseline,
        GateSeries series)
    {
        var current = normalized.TryGetValue(framework, out var samples) ? samples : [];
        var currentMedian = current.Length > 0 ? RobustStatistics.Median(current) : 0;

        // The floor is measured against run medians, which are independent observations, while the rank
        // test pools the samples inside those runs. Mixing the two on purpose: the floor answers "is this
        // beyond normal run-to-run movement" and the rank test answers "is the shift consistent".
        var runMedians = new List<double>(baseline.Count);
        var pooled = new List<double>(baseline.Count * Math.Max(current.Length, 1));
        foreach (var record in baseline)
        {
            var recorded = NormalizeRecord(record);
            if (!recorded.TryGetValue(framework, out var values) || values.Length == 0)
            {
                continue;
            }

            runMedians.Add(RobustStatistics.Median(values));
            pooled.AddRange(values);
        }

        // Confirmation reads the immediately preceding run and no further back. Gated participants are
        // deliberately not part of the baseline key, so one can be removed and later restored; carrying a
        // verdict across the runs that did not measure it would confirm a repeat that never happened.
        // Statistics still use every comparable record above.
        var previousVerdict = baseline.Count == 0
            ? RegressionVerdict.NotEvaluated
            : baseline[^1].Participants
                .FirstOrDefault(participant => participant.Framework == framework)?.Verdict
                ?? RegressionVerdict.NotEvaluated;

        if (runMedians.Count < MinimumBaselineRuns || current.Length == 0)
        {
            return new ParticipantAssessment
            {
                Framework = framework,
                Verdict = RegressionVerdict.InsufficientBaseline,
                CurrentNormalizedMedian = currentMedian,
                BaselineNormalizedMedian = 0,
                RelativeChange = 0,
                BaselineRobustDeviation = 0,
                RegressionProbability = 1,
                ImprovementProbability = 1,
                BaselineRunCount = runMedians.Count,
                PreviousVerdict = previousVerdict
            };
        }

        var baselineMedian = RobustStatistics.Median(runMedians);
        var robustDeviation = RobustStatistics.RobustStandardDeviation(runMedians);
        var relativeChange = (currentMedian - baselineMedian) / baselineMedian;
        var comparable = current.Length >= MinimumComparableSamples && pooled.Count >= MinimumComparableSamples;
        var regressionProbability = comparable ? RobustStatistics.UpperTailProbability(current, pooled) : 1;
        var improvementProbability = comparable ? RobustStatistics.UpperTailProbability(pooled, current) : 1;
        var floor = RobustDeviationMultiple * robustDeviation;

        var verdict = RegressionVerdict.Stable;
        if (relativeChange >= MinimumRelativeChange
            && currentMedian - baselineMedian >= floor
            && regressionProbability < SignificanceLevel)
        {
            // A pull-request run cannot repeat: it never enters the history, so the run before it belongs
            // to the default branch and says nothing about the change under review.
            verdict = series == GateSeries.Baseline
                && previousVerdict is RegressionVerdict.Suspected or RegressionVerdict.Confirmed
                    ? RegressionVerdict.Confirmed
                    : RegressionVerdict.Suspected;
        }
        else if (relativeChange <= -MinimumRelativeChange
            && baselineMedian - currentMedian >= floor
            && improvementProbability < SignificanceLevel)
        {
            verdict = RegressionVerdict.Improved;
        }

        return new ParticipantAssessment
        {
            Framework = framework,
            Verdict = verdict,
            CurrentNormalizedMedian = currentMedian,
            BaselineNormalizedMedian = baselineMedian,
            RelativeChange = relativeChange,
            BaselineRobustDeviation = robustDeviation,
            RegressionProbability = regressionProbability,
            ImprovementProbability = improvementProbability,
            BaselineRunCount = runMedians.Count,
            PreviousVerdict = previousVerdict
        };
    }

    private static HistoryRecord BuildRecord(
        ComparisonResult result,
        IReadOnlyDictionary<string, double[]> samples,
        IReadOnlyDictionary<string, double[]> normalized,
        IReadOnlyList<ParticipantAssessment> assessments)
    {
        var participants = result.Summaries.Select(summary => new HistoryParticipant
        {
            Framework = summary.Framework,
            Version = summary.Version,
            MedianMilliseconds = summary.MedianMilliseconds,
            NormalizedMedian = normalized.TryGetValue(summary.Framework, out var values) && values.Length > 0
                ? RobustStatistics.Median(values)
                : 0,
            Verdict = assessments.FirstOrDefault(assessment => assessment.Framework == summary.Framework)?.Verdict
                ?? RegressionVerdict.NotEvaluated,
            SamplesMilliseconds = samples.TryGetValue(summary.Framework, out var raw) ? raw : []
        }).ToArray();

        return new HistoryRecord
        {
            SchemaVersion = HistoryRecord.CurrentSchemaVersion,
            GeneratedAtUtc = result.GeneratedAtUtc,
            BenchmarkId = result.BenchmarkId,
            MetricRevision = result.MetricRevision,
            Commit = result.Commit,
            Reference = result.Reference,
            RunId = result.RunId,
            Trigger = result.Trigger,
            Rounds = result.Rounds,
            ExpectedTestCount = result.ExpectedTestCount,
            Environment = new HistoryEnvironment
            {
                RunnerImage = result.RunnerImage,
                RunnerImageVersion = result.RunnerImageVersion,
                OperatingSystem = result.OperatingSystem,
                Architecture = result.Architecture,
                Processor = result.Processor,
                ProcessorCount = result.ProcessorCount,
                SdkVersion = result.DotNetSdkVersion,
                RuntimeVersion = result.Runtime
            },
            Participants = participants
        };
    }

    private static Dictionary<string, double[]> SamplesByFramework(ComparisonResult result)
        => result.Measurements
            .GroupBy(measurement => measurement.Framework, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.OrderBy(measurement => measurement.Round).Select(measurement => measurement.ElapsedMilliseconds).ToArray(),
                StringComparer.Ordinal);

    private static Dictionary<string, double[]> NormalizeRecord(HistoryRecord record)
    {
        var samples = record.Participants.ToDictionary(
            participant => participant.Framework,
            participant => participant.SamplesMilliseconds.ToArray(),
            StringComparer.Ordinal);
        var references = samples.Keys.Where(framework => !IsGated(framework)).Order(StringComparer.Ordinal).ToArray();
        return Normalize(samples, references);
    }

    /// <summary>
    /// Divides each round's sample by the geometric mean of the references measured in the same round.
    /// Rounds where any series is missing a sample are dropped rather than compared across rounds.
    /// </summary>
    private static Dictionary<string, double[]> Normalize(
        IReadOnlyDictionary<string, double[]> samples,
        IReadOnlyList<string> references)
    {
        var normalized = new Dictionary<string, double[]>(StringComparer.Ordinal);
        if (references.Count == 0)
        {
            return normalized;
        }

        var rounds = references.Min(framework => samples[framework].Length);
        if (rounds == 0)
        {
            return normalized;
        }

        var referenceMeans = new double[rounds];
        var buffer = new double[references.Count];
        for (var round = 0; round < rounds; round++)
        {
            for (var index = 0; index < references.Count; index++)
            {
                buffer[index] = samples[references[index]][round];
            }

            if (Array.Exists(buffer, value => value <= 0))
            {
                // A non-positive sample cannot be a wall-clock time, so the record is unusable rather
                // than merely noisy; dropping it is safer than normalising against a bad reference.
                return new Dictionary<string, double[]>(StringComparer.Ordinal);
            }

            referenceMeans[round] = RobustStatistics.GeometricMean(buffer);
        }

        foreach (var (framework, values) in samples)
        {
            var count = Math.Min(rounds, values.Length);
            var series = new double[count];
            for (var round = 0; round < count; round++)
            {
                series[round] = values[round] / referenceMeans[round];
            }

            normalized[framework] = series;
        }

        return normalized;
    }
}
