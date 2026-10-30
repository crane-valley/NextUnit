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
    /// can shift participants sharing its mode and is important context when a finding is surprising.
    /// </summary>
    public required IReadOnlyList<string> ReferenceVersionChanges { get; init; }

    /// <summary>The finding for every gated participant.</summary>
    public required IReadOnlyList<ParticipantAssessment> Assessments { get; init; }

    /// <summary>
    /// All-reference comparisons retained as diagnostic context, never a confirmation chain. A common
    /// AOT slowdown can cancel in a same-mode ratio; this view keeps that signal visible for investigation.
    /// </summary>
    public required IReadOnlyList<ParticipantAssessment> CrossModeAssessments { get; init; }

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
/// seconds. Common multiplicative movement cancels within each execution mode; AOT and managed startup
/// need not respond equally to an environment change. Ratios cannot identify the cause of a shift.
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
        if (result.MetricRevision != ComparisonResult.CurrentMetricRevision)
        {
            throw new ArgumentException(
                $"Result metric revision {result.MetricRevision} is not supported by this analyzer "
                + $"(revision {ComparisonResult.CurrentMetricRevision}). Rerun the comparison before analyzing it.",
                nameof(result));
        }

        var key = BaselineKey.For(result);
        var currentSamples = SamplesByFramework(result);
        var references = currentSamples.Keys.Where(framework => !IsGated(framework)).Order(StringComparer.Ordinal).ToArray();
        var normalized = Normalize(currentSamples, references);
        var crossModeNormalized = Normalize(currentSamples, references, false);

        var baseline = history
            .Where(record => record.RunId != result.RunId)
            .Where(record => string.Equals(BaselineKey.For(record), key, StringComparison.Ordinal))
            .OrderBy(record => record.GeneratedAtUtc)
            .TakeLast(BaselineWindow)
            .ToArray();

        // Revision 2 changes only normalization, not the raw measurement protocol. The all-reference
        // diagnostic can therefore retain r1 raw samples while the primary r2 baseline warms up.
        // This is an explicit r1 -> r2 exception, not permission to compare future metric revisions.
        // No historical verdict is used to confirm a diagnostic.
        var diagnosticBaseline = history
            .Where(record => record.RunId != result.RunId)
            .Where(record => record.MetricRevision == result.MetricRevision
                || (result.MetricRevision == 2 && record.MetricRevision == 1))
            .Where(record => string.Equals(BaselineKey.For(record with { MetricRevision = result.MetricRevision }), key, StringComparison.Ordinal))
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
            ReferenceVersionChanges = ReferenceVersionChanges(result, baseline.Length == 0 ? diagnosticBaseline : baseline),
            Assessments = assessments,
            // These verdicts are deliberately never confirmed: history stores the primary same-mode
            // verdict, not a cross-mode predecessor. Do not reuse that chain for another metric.
            CrossModeAssessments = result.Summaries.Where(summary => IsGated(summary.Framework))
                .Select(summary => Assess(summary.Framework, crossModeNormalized,
                    diagnosticBaseline, GateSeries.PullRequest, false)).ToArray(),
            Record = BuildRecord(result, currentSamples, normalized, assessments)
        };
    }

    /// <summary>
    /// Names the reference frameworks whose version moved since the most recent comparable run.
    /// <para>
    /// Reference versions are deliberately not part of the baseline key. They are dependency-managed and
    /// move often, so keying on them would retire the baseline before it could arm. The metric is also
    /// resistant to an individual change when multiple references share a mode. Native AOT currently
    /// has only one reference, so there is no dilution there. Always surface version changes, and do
    /// not infer a participant's code regressed merely because its reference-relative metric moved.
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
        GateSeries series,
        bool matchExecutionMode = true)
    {
        var current = normalized.TryGetValue(framework, out var samples) ? samples : [];
        var currentMedian = current.Length > 0 ? RobustStatistics.Median(current) : 0;

        // The floor is measured against run medians, which are independent observations, while the rank
        // test pools the samples inside those runs. Mixing the two on purpose: the floor answers "is this
        // beyond normal run-to-run movement" and the rank test answers "is the shift consistent".
        // The history is a git branch, so a hand-edited or truncated record can carry a zero or negative
        // sample that no measurement would produce. Such a record is dropped here rather than defended
        // against later: it is what keeps every value below strictly positive, and therefore keeps the
        // baseline median a safe divisor.
        var usable = baseline
            .Select(record => NormalizeRecord(record, matchExecutionMode).GetValueOrDefault(framework, []))
            .Where(values => values.Length > 0 && Array.TrueForAll(values, value => value > 0))
            .ToArray();
        var runMedians = Array.ConvertAll(usable, RobustStatistics.Median);
        var pooled = usable.SelectMany(values => values).ToArray();

        // Confirmation reads the immediately preceding run and no further back. Gated participants are
        // deliberately not part of the baseline key, so one can be removed and later restored; carrying a
        // verdict across the runs that did not measure it would confirm a repeat that never happened.
        // Statistics still use every comparable record above.
        var previousVerdict = !matchExecutionMode || baseline.Count == 0
            ? RegressionVerdict.NotEvaluated
            : baseline[^1].Participants
                .FirstOrDefault(participant => participant.Framework == framework)?.Verdict
                ?? RegressionVerdict.NotEvaluated;

        if (runMedians.Length < MinimumBaselineRuns || current.Length == 0)
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
                BaselineRunCount = runMedians.Length,
                PreviousVerdict = previousVerdict
            };
        }

        var baselineMedian = RobustStatistics.Median(runMedians);
        var robustDeviation = RobustStatistics.RobustStandardDeviation(runMedians);
        // Safe divisor: every sample behind runMedians was filtered to be strictly positive above, so the
        // median of them is strictly positive too.
        var relativeChange = (currentMedian - baselineMedian) / baselineMedian;
        var comparable = current.Length >= MinimumComparableSamples && pooled.Length >= MinimumComparableSamples;
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
            BaselineRunCount = runMedians.Length,
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

    private static Dictionary<string, double[]> NormalizeRecord(HistoryRecord record, bool matchExecutionMode)
    {
        var samples = record.Participants.ToDictionary(
            participant => participant.Framework,
            participant => participant.SamplesMilliseconds.ToArray(),
            StringComparer.Ordinal);
        var references = samples.Keys.Where(framework => !IsGated(framework)).Order(StringComparer.Ordinal).ToArray();
        return Normalize(samples, references, matchExecutionMode);
    }

    /// <summary>Whether a participant uses the benchmark's explicit Native AOT naming convention.</summary>
    public static bool IsAot(string framework) => framework.EndsWith(" (AOT)", StringComparison.Ordinal);

    /// <summary>
    /// Divides a sample by references in the same round and execution mode. Missing same-mode references
    /// leave that participant unevaluated rather than silently falling back to a different startup model.
    /// </summary>
    private static Dictionary<string, double[]> Normalize(
        IReadOnlyDictionary<string, double[]> samples,
        IReadOnlyList<string> references,
        bool matchExecutionMode = true)
    {
        var normalized = new Dictionary<string, double[]>(StringComparer.Ordinal);
        foreach (var (framework, values) in samples)
        {
            var peers = references.Where(reference =>
                !matchExecutionMode || IsAot(reference) == IsAot(framework)).ToArray();
            if (peers.Length == 0)
            {
                continue;
            }

            var count = Math.Min(values.Length, peers.Min(reference => samples[reference].Length));
            var series = new double[count];
            var buffer = new double[peers.Length];
            var valid = true;
            for (var round = 0; round < count; round++)
            {
                for (var index = 0; index < peers.Length; index++)
                {
                    buffer[index] = samples[peers[index]][round];
                }

                if (Array.Exists(buffer, value => !double.IsFinite(value) || value <= 0)
                    || !double.IsFinite(values[round]) || values[round] <= 0)
                {
                    valid = false;
                    break;
                }

                series[round] = values[round] / RobustStatistics.GeometricMean(buffer);
            }

            if (valid)
            {
                normalized[framework] = series;
            }
        }

        return normalized;
    }
}
