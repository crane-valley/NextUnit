using NextUnit;

namespace SpeedComparison.Analysis.Tests;

/// <summary>Separates execution-mode movement from participant-specific regressions.</summary>
public class ExecutionModeTests
{
    [Test]
    public void DifferentCommonScalingPerMode_DoesNotRegress()
    {
        var result = RegressionGate.Evaluate(Current(managedFactor: 0.75, aotFactor: 0.85), History(), GateSeries.Baseline);

        Assert.Equal(RegressionVerdict.Stable, Aot(result).Verdict);
        Assert.Equal(RegressionVerdict.Stable, result.Assessments.Single(x => x.Framework == "NextUnit").Verdict);
        Assert.Equal(RegressionVerdict.Suspected, result.CrossModeAssessments.Single(x => x.Framework == "NextUnit (AOT)").Verdict);
        Assert.False(result.HasConfirmedRegression);
    }

    [Test]
    public void IsolatedAotRegression_StillRequiresAndConfirmsARepeat()
    {
        var history = History();
        var first = RegressionGate.Evaluate(Current(subjectFactor: 1.25), history, GateSeries.Baseline);
        Assert.Equal(RegressionVerdict.Suspected, Aot(first).Verdict);
        history.Add(first.Record);

        var repeated = Current(subjectFactor: 1.25) with { RunId = "repeat", GeneratedAtUtc = DateTimeOffset.UtcNow };
        var second = RegressionGate.Evaluate(repeated, history, GateSeries.Baseline);
        Assert.Equal(RegressionVerdict.Confirmed, Aot(second).Verdict);
        Assert.True(second.HasConfirmedRegression);
        Assert.Equal(RegressionVerdict.Suspected, second.CrossModeAssessments.Single(x => x.Framework == "NextUnit (AOT)").Verdict);
        Assert.Equal(RegressionVerdict.Suspected, Aot(RegressionGate.Evaluate(repeated, history, GateSeries.PullRequest)).Verdict);
    }

    [Test]
    public void SharedAotSlowdown_RemainsVisibleAsADiagnostic()
    {
        var result = RegressionGate.Evaluate(Current(aotFactor: 1.25), History(), GateSeries.Baseline);
        Assert.Equal(RegressionVerdict.Stable, Aot(result).Verdict);
        Assert.Equal(RegressionVerdict.Suspected, result.CrossModeAssessments.Single(x => x.Framework == "NextUnit (AOT)").Verdict);
        var report = RegressionReport.Render(result);
        Assert.Contains("Cross-mode movement warrants investigation", report);
        Assert.Contains("Only one AOT reference", report);
        Assert.Contains("Cross-mode diagnostic (not a gate)", report);
        Assert.Contains("shared regression can be hidden", report);
    }

    [Test]
    public void MissingAotReference_DoesNotFallBackToManagedReferences()
    {
        var samples = Samples();
        samples.Remove("TUnit (AOT)");
        var history = Enumerable.Range(0, 5).Select(i => SyntheticRuns.Record(samples, $"past-{i}", DateTimeOffset.UnixEpoch.AddDays(i))).ToArray();
        var result = RegressionGate.Evaluate(SyntheticRuns.Result(samples), history, GateSeries.Baseline);
        Assert.Equal(RegressionVerdict.InsufficientBaseline, Aot(result).Verdict);
        Assert.Equal(RegressionVerdict.Stable, result.Assessments.Single(x => x.Framework == "NextUnit").Verdict);
        Assert.Contains("none; no same-mode decision", RegressionReport.Render(result));
    }

    [Test]
    public void InvalidAotReference_DoesNotPoisonManagedAssessment()
    {
        foreach (var invalid in new[] { 0.0, double.NaN, double.PositiveInfinity })
        {
            var samples = Samples();
            samples["TUnit (AOT)"][0] = invalid;
            var result = RegressionGate.Evaluate(SyntheticRuns.Result(samples), History(), GateSeries.Baseline);
            Assert.Equal(RegressionVerdict.InsufficientBaseline, Aot(result).Verdict);
            Assert.Equal(RegressionVerdict.Stable, result.Assessments.Single(x => x.Framework == "NextUnit").Verdict);
            Assert.Contains("some participants have insufficient evidence", RegressionReport.Render(result));
        }
    }

    [Test]
    public void OldMetricVerdicts_CannotConfirmTheNewMetric()
    {
        var history = History().Select(x => x with { MetricRevision = 1, Participants = [.. x.Participants.Select(p => p with { Verdict = RegressionVerdict.Confirmed })] }).ToArray();
        var result = RegressionGate.Evaluate(Current(subjectFactor: 1.25), history, GateSeries.Baseline);
        Assert.Equal(0, result.BaselineRunCount);
        Assert.Equal(RegressionVerdict.InsufficientBaseline, Aot(result).Verdict);
        Assert.False(result.HasConfirmedRegression);
        Assert.Equal(RegressionVerdict.Suspected, result.CrossModeAssessments.Single(x => x.Framework == "NextUnit (AOT)").Verdict);
        Assert.Contains("Cross-mode movement warrants investigation", RegressionReport.Render(result));
        Assert.True(result.CrossModeAssessments.All(x => x.PreviousVerdict == RegressionVerdict.NotEvaluated));
        Assert.Contains("NextUnit (AOT): 10", RegressionReport.Render(result));
    }

    [Test]
    public void ReferenceVersionChanges_AreVisibleDuringPrimaryWarmup()
    {
        var history = History().Select(x => x with { MetricRevision = 1 }).ToArray();
        var current = Current();
        current = current with { Summaries = [.. current.Summaries.Select(s => s.Framework == "TUnit (AOT)" ? s with { Version = "2.0.0" } : s)] };
        var result = RegressionGate.Evaluate(current, history, GateSeries.Baseline);
        Assert.Equal(0, result.BaselineRunCount);
        Assert.Contains("TUnit (AOT) 1.0.0 -> 2.0.0", RegressionReport.Render(result));
    }

    [Test]
    public void UnrelatedMetricRevisions_DoNotFeedTheDiagnostic()
    {
        var history = History().Select(x => x with { MetricRevision = 99 }).ToArray();
        var result = RegressionGate.Evaluate(Current(subjectFactor: 1.25), history, GateSeries.Baseline);
        Assert.Equal(RegressionVerdict.InsufficientBaseline, result.CrossModeAssessments.Single(x => x.Framework == "NextUnit (AOT)").Verdict);
    }

    [Test]
    public void AnOldResult_CannotBeReinterpretedWithTheNewMetric()
    {
        var old = Current(subjectFactor: 1.25) with { MetricRevision = 1 };
        var error = Assert.Throws<ArgumentException>(() => RegressionGate.Evaluate(old, History(), GateSeries.Baseline));
        Assert.Contains("Rerun the comparison", error.Message);
    }

    [Test]
    public void AnAotReferenceVersionChange_IsStillReported()
    {
        var current = Current();
        current = current with { Summaries = [.. current.Summaries.Select(s => s.Framework == "TUnit (AOT)" ? s with { Version = "2.0.0" } : s)] };
        var result = RegressionGate.Evaluate(current, History(), GateSeries.Baseline);
        Assert.Contains("TUnit (AOT) 1.0.0 -> 2.0.0", RegressionReport.Render(result));
    }

    private static ParticipantAssessment Aot(GateResult result) => result.Assessments.Single(x => x.Framework == "NextUnit (AOT)");

    private static List<HistoryRecord> History() => Enumerable.Range(0, 10)
        .Select(i => SyntheticRuns.Record(Samples(), $"past-{i}", DateTimeOffset.UnixEpoch.AddDays(i))).ToList();

    private static ComparisonResult Current(double managedFactor = 1, double aotFactor = 1, double subjectFactor = 1)
        => SyntheticRuns.Result(Samples(managedFactor, aotFactor, subjectFactor));

    private static Dictionary<string, double[]> Samples(double managedFactor = 1, double aotFactor = 1, double subjectFactor = 1)
    {
        var samples = SyntheticRuns.Samples(42, machineFactor: managedFactor, noiseFraction: 0);
        samples["TUnit (AOT)"] = Enumerable.Repeat(25.0 * aotFactor, 21).ToArray();
        samples["NextUnit (AOT)"] = Enumerable.Repeat(20.0 * aotFactor * subjectFactor, 21).ToArray();
        return samples;
    }
}
