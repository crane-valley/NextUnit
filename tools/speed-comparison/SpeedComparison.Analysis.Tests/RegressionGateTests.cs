using NextUnit;

namespace SpeedComparison.Analysis.Tests;

/// <summary>Covers the decisions the gate is allowed to reach, and the ones it must refuse to reach.</summary>
public class RegressionGateTests
{
    [Test]
    public void EmptyHistory_LeavesTheGateDisarmed()
    {
        var result = RegressionGate.Evaluate(SyntheticRuns.Result(SyntheticRuns.Samples(seed: 100)), [], GateSeries.Baseline);

        Assert.Equal(RegressionVerdict.InsufficientBaseline, Subject(result).Verdict);
        Assert.False(result.HasConfirmedRegression);
    }

    [Test]
    public void FewerRunsThanTheMinimum_LeavesTheGateDisarmed()
    {
        var history = SyntheticRuns.Baseline(RegressionGate.MinimumBaselineRuns - 1);
        var regressed = SyntheticRuns.Samples(seed: 100, subjectMilliseconds: 400.0);

        var result = RegressionGate.Evaluate(SyntheticRuns.Result(regressed), history, GateSeries.Baseline);

        Assert.Equal(RegressionVerdict.InsufficientBaseline, Subject(result).Verdict);
    }

    [Test]
    public void UnchangedRun_IsStable()
    {
        var history = SyntheticRuns.Baseline(10);

        var result = RegressionGate.Evaluate(
            SyntheticRuns.Result(SyntheticRuns.Samples(seed: 100)),
            history,
            GateSeries.Baseline);

        Assert.Equal(RegressionVerdict.Stable, Subject(result).Verdict);
    }

    [Test]
    public void SlowerMachine_IsStable()
    {
        // Every participant runs in the same round on the same machine, so a runner that is 40 percent
        // slower must cancel out entirely. This is the property the whole gate rests on.
        var history = SyntheticRuns.Baseline(10);
        var slowMachine = SyntheticRuns.Samples(seed: 100, machineFactor: 1.4);

        var result = RegressionGate.Evaluate(SyntheticRuns.Result(slowMachine), history, GateSeries.Baseline);

        Assert.Equal(RegressionVerdict.Stable, Subject(result).Verdict);
    }

    [Test]
    public void ChangeBelowTheEffectThreshold_IsStable()
    {
        var history = SyntheticRuns.Baseline(10);
        var slightlySlower = SyntheticRuns.Samples(seed: 100, subjectMilliseconds: 300.0 * 1.02);

        var result = RegressionGate.Evaluate(SyntheticRuns.Result(slightlySlower), history, GateSeries.Baseline);

        Assert.Equal(RegressionVerdict.Stable, Subject(result).Verdict);
    }

    [Test]
    public void FirstRegressedRun_IsSuspectedRatherThanFailing()
    {
        var history = SyntheticRuns.Baseline(10);
        var regressed = SyntheticRuns.Samples(seed: 100, subjectMilliseconds: 300.0 * 1.25);

        var result = RegressionGate.Evaluate(SyntheticRuns.Result(regressed), history, GateSeries.Baseline);

        Assert.Equal(RegressionVerdict.Suspected, Subject(result).Verdict);
        Assert.False(result.HasConfirmedRegression);
    }

    [Test]
    public void RepeatedRegression_IsConfirmed()
    {
        var history = SyntheticRuns.Baseline(10);
        history[^1] = SyntheticRuns.Record(
            SyntheticRuns.Samples(seed: 50, subjectMilliseconds: 300.0 * 1.25),
            runId: "baseline-suspected",
            generatedAtUtc: new DateTimeOffset(2026, 3, 20, 0, 0, 0, TimeSpan.Zero),
            verdict: RegressionVerdict.Suspected);
        var regressed = SyntheticRuns.Samples(seed: 100, subjectMilliseconds: 300.0 * 1.25);

        var result = RegressionGate.Evaluate(SyntheticRuns.Result(regressed), history, GateSeries.Baseline);

        Assert.Equal(RegressionVerdict.Confirmed, Subject(result).Verdict);
        Assert.True(result.HasConfirmedRegression);
    }

    [Test]
    public void PullRequestSeries_NeverConfirms()
    {
        // A pull-request run does not enter the history, so the run before it belongs to the default
        // branch and cannot corroborate anything about the change under review.
        var history = SyntheticRuns.Baseline(10);
        history[^1] = SyntheticRuns.Record(
            SyntheticRuns.Samples(seed: 50, subjectMilliseconds: 300.0 * 1.25),
            runId: "baseline-suspected",
            generatedAtUtc: new DateTimeOffset(2026, 3, 20, 0, 0, 0, TimeSpan.Zero),
            verdict: RegressionVerdict.Suspected);
        var regressed = SyntheticRuns.Samples(seed: 100, subjectMilliseconds: 300.0 * 1.25);

        var result = RegressionGate.Evaluate(SyntheticRuns.Result(regressed), history, GateSeries.PullRequest);

        Assert.Equal(RegressionVerdict.Suspected, Subject(result).Verdict);
        Assert.False(result.HasConfirmedRegression);
    }

    [Test]
    public void FasterRun_IsReportedAsAnImprovement()
    {
        var history = SyntheticRuns.Baseline(10);
        var faster = SyntheticRuns.Samples(seed: 100, subjectMilliseconds: 300.0 * 0.8);

        var result = RegressionGate.Evaluate(SyntheticRuns.Result(faster), history, GateSeries.Baseline);

        Assert.Equal(RegressionVerdict.Improved, Subject(result).Verdict);
        Assert.False(result.HasConfirmedRegression);
    }

    [Test]
    public void RecordedRunsFromAnotherEnvironment_AreNotComparable()
    {
        var history = SyntheticRuns.Baseline(10)
            .Select(record => record with
            {
                Environment = record.Environment with { RunnerImage = "windows2025" }
            })
            .ToList();

        var result = RegressionGate.Evaluate(
            SyntheticRuns.Result(SyntheticRuns.Samples(seed: 100)),
            history,
            GateSeries.Baseline);

        Assert.Equal(0, result.BaselineRunCount);
        Assert.Equal(RegressionVerdict.InsufficientBaseline, Subject(result).Verdict);
    }

    [Test]
    public void BaselineIsLimitedToTheConfiguredWindow()
    {
        var history = SyntheticRuns.Baseline(RegressionGate.BaselineWindow + 15);

        var result = RegressionGate.Evaluate(
            SyntheticRuns.Result(SyntheticRuns.Samples(seed: 100)),
            history,
            GateSeries.Baseline);

        Assert.Equal(RegressionGate.BaselineWindow, result.BaselineRunCount);
    }

    [Test]
    public void TheRunItselfIsNeverPartOfItsOwnBaseline()
    {
        var history = SyntheticRuns.Baseline(10);
        history.Add(SyntheticRuns.Record(
            SyntheticRuns.Samples(seed: 100),
            runId: "current",
            generatedAtUtc: new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero)));

        var result = RegressionGate.Evaluate(
            SyntheticRuns.Result(SyntheticRuns.Samples(seed: 100), runId: "current"),
            history,
            GateSeries.Baseline);

        Assert.Equal(10, result.BaselineRunCount);
    }

    [Test]
    public void TheProducedRecordCarriesTheVerdictAndTheRawSamples()
    {
        var history = SyntheticRuns.Baseline(10);
        var samples = SyntheticRuns.Samples(seed: 100, subjectMilliseconds: 300.0 * 1.25);

        var result = RegressionGate.Evaluate(SyntheticRuns.Result(samples), history, GateSeries.Baseline);

        var subject = result.Record.Participants.Single(participant => participant.Framework == SyntheticRuns.Subject);
        Assert.Equal(RegressionVerdict.Suspected, subject.Verdict);
        Assert.Equal(samples[SyntheticRuns.Subject].Length, subject.SamplesMilliseconds.Count);
        var reference = result.Record.Participants.Single(participant => participant.Framework == "TUnit");
        Assert.Equal(RegressionVerdict.NotEvaluated, reference.Verdict);
    }

    [Test]
    public void ARunThatDidNotMeasureTheParticipantCannotCorroborate()
    {
        // Gated participants are deliberately not part of the baseline key, so one can be dropped and later
        // restored. A suspected verdict from before the gap must not confirm a repeat that never happened.
        var history = SyntheticRuns.Baseline(10);
        history[^2] = SyntheticRuns.Record(
            SyntheticRuns.Samples(seed: 50, subjectMilliseconds: 300.0 * 1.25),
            runId: "baseline-suspected",
            // Dated before the final record so the run without the participant stays the most recent one.
            generatedAtUtc: new DateTimeOffset(2026, 2, 26, 0, 0, 0, TimeSpan.Zero),
            verdict: RegressionVerdict.Suspected);
        history[^1] = WithoutSubject(history[^1]);
        var regressed = SyntheticRuns.Samples(seed: 100, subjectMilliseconds: 300.0 * 1.25);

        var result = RegressionGate.Evaluate(SyntheticRuns.Result(regressed), history, GateSeries.Baseline);

        Assert.Equal(RegressionVerdict.Suspected, Subject(result).Verdict);
        Assert.False(result.HasConfirmedRegression);
    }

    [Test]
    public void ARunThatDidNotMeasureTheParticipantStillFeedsTheStatistics()
    {
        var history = SyntheticRuns.Baseline(10);
        history[^1] = WithoutSubject(history[^1]);

        var result = RegressionGate.Evaluate(
            SyntheticRuns.Result(SyntheticRuns.Samples(seed: 100)),
            history,
            GateSeries.Baseline);

        Assert.Equal(10, result.BaselineRunCount);
        Assert.Equal(9, Subject(result).BaselineRunCount);
        Assert.Equal(RegressionVerdict.Stable, Subject(result).Verdict);
    }

    [Test]
    public void ARecordCarryingANonPositiveSampleIsDropped()
    {
        // The history is a git branch, so a hand-edited or truncated record can carry a sample no
        // measurement would produce. It must not reach the statistics, where it would poison the divisor.
        var history = SyntheticRuns.Baseline(10);
        history[^1] = WithSubjectSamples(history[^1], [0.0, 0.0, 0.0]);

        var result = RegressionGate.Evaluate(
            SyntheticRuns.Result(SyntheticRuns.Samples(seed: 100)),
            history,
            GateSeries.Baseline);

        Assert.Equal(9, Subject(result).BaselineRunCount);
        Assert.Equal(RegressionVerdict.Stable, Subject(result).Verdict);
    }

    [Test]
    public void ABaselineOfOnlyCorruptRecordsLeavesTheGateDisarmed()
    {
        var history = SyntheticRuns.Baseline(10)
            .Select(record => WithSubjectSamples(record, [0.0, 0.0, 0.0]))
            .ToList();

        var result = RegressionGate.Evaluate(
            SyntheticRuns.Result(SyntheticRuns.Samples(seed: 100, subjectMilliseconds: 300.0 * 1.25)),
            history,
            GateSeries.Baseline);

        Assert.Equal(RegressionVerdict.InsufficientBaseline, Subject(result).Verdict);
        Assert.Equal(0, Subject(result).RelativeChange, 1e-12);
        Assert.False(result.HasConfirmedRegression);
    }

    [Test]
    public void AReferenceFrameworkUpgradeIsNamedInTheResult()
    {
        var history = SyntheticRuns.Baseline(10);
        var upgraded = SyntheticRuns.Result(SyntheticRuns.Samples(seed: 100));
        upgraded = upgraded with
        {
            Summaries = [.. upgraded.Summaries.Select(summary =>
                summary.Framework == "TUnit" ? summary with { Version = "2.0.0" } : summary)]
        };

        var result = RegressionGate.Evaluate(upgraded, history, GateSeries.Baseline);

        Assert.Equal(1, result.ReferenceVersionChanges.Count);
        Assert.Contains("TUnit 1.0.0 -> 2.0.0", result.ReferenceVersionChanges[0]);
    }

    [Test]
    public void UnchangedReferenceFrameworksAreNotReported()
    {
        var history = SyntheticRuns.Baseline(10);

        var result = RegressionGate.Evaluate(
            SyntheticRuns.Result(SyntheticRuns.Samples(seed: 100)),
            history,
            GateSeries.Baseline);

        Assert.Equal(0, result.ReferenceVersionChanges.Count);
    }

    [Test]
    public void OnlyThisProjectsParticipantsAreGated()
    {
        Assert.True(RegressionGate.IsGated("NextUnit"));
        Assert.True(RegressionGate.IsGated("NextUnit (AOT)"));
        Assert.False(RegressionGate.IsGated("TUnit"));
        Assert.False(RegressionGate.IsGated("xUnit"));
    }

    private static ParticipantAssessment Subject(GateResult result)
        => result.Assessments.Single(assessment => assessment.Framework == SyntheticRuns.Subject);

    private static HistoryRecord WithSubjectSamples(HistoryRecord record, double[] samples)
        => record with
        {
            Participants = [.. record.Participants.Select(participant =>
                participant.Framework == SyntheticRuns.Subject
                    ? participant with { SamplesMilliseconds = samples }
                    : participant)]
        };

    private static HistoryRecord WithoutSubject(HistoryRecord record)
        => record with
        {
            Participants = [.. record.Participants.Where(participant => participant.Framework != SyntheticRuns.Subject)]
        };
}
