using NextUnit;

namespace SpeedComparison.Analysis.Tests;

/// <summary>Covers what the published report claims, because the report is the whole visible product.</summary>
public class RegressionReportTests
{
    [Test]
    public void ABaselineSuspectedRunSaysItsPredecessorWasClean()
    {
        var report = RegressionReport.Render(Suspected(GateSeries.Baseline));

        Assert.Contains("**Suspected regression** in NextUnit", report);
        Assert.Contains("the run recorded before it did not regress", report);
    }

    [Test]
    public void AReadOnlySuspectedRunSaysItCannotConfirm()
    {
        // A pull request or a side-branch dispatch stays suspected even when the recorded run before it
        // regressed, so explaining it away as a clean predecessor would be false.
        var report = RegressionReport.Render(Suspected(GateSeries.PullRequest));

        Assert.Contains("**Suspected regression** in NextUnit", report);
        Assert.Contains("not appended to the history", report);
    }

    [Test]
    public void AReadOnlyRunIsNotDescribedAsAPullRequest()
    {
        var report = RegressionReport.Render(Suspected(GateSeries.PullRequest));

        Assert.Contains("read-only, not appended to the history", report);
    }

    [Test]
    public void AConfirmedRunNamesTheRepeat()
    {
        var history = SyntheticRuns.Baseline(10);
        history[^1] = SyntheticRuns.Record(
            SyntheticRuns.Samples(seed: 50, subjectMilliseconds: 300.0 * 1.25),
            runId: "baseline-suspected",
            generatedAtUtc: new DateTimeOffset(2026, 3, 20, 0, 0, 0, TimeSpan.Zero),
            verdict: RegressionVerdict.Suspected);
        var regressed = SyntheticRuns.Samples(seed: 100, subjectMilliseconds: 300.0 * 1.25);

        var report = RegressionReport.Render(
            RegressionGate.Evaluate(SyntheticRuns.Result(regressed), history, GateSeries.Baseline));

        Assert.Contains("**Confirmed regression** in NextUnit", report);
    }

    [Test]
    public void AWarmingBaselineSaysSoRatherThanClaimingStability()
    {
        var report = RegressionReport.Render(
            RegressionGate.Evaluate(SyntheticRuns.Result(SyntheticRuns.Samples(seed: 100)), [], GateSeries.Baseline));

        Assert.Contains("Baseline is still warming up", report);
    }

    [Test]
    public void TheReportStatesTheThresholdsItApplied()
    {
        var report = RegressionReport.Render(Suspected(GateSeries.Baseline));

        Assert.Contains("5% slower", report);
        Assert.Contains("Mann-Whitney U", report);
        Assert.Contains("p < 0.01", report);
    }

    [Test]
    public void AReferenceVersionChangeIsSurfacedInTheReport()
    {
        var history = SyntheticRuns.Baseline(10);
        var upgraded = SyntheticRuns.Result(SyntheticRuns.Samples(seed: 100));
        upgraded = upgraded with
        {
            Summaries = [.. upgraded.Summaries.Select(summary =>
                summary.Framework == "TUnit" ? summary with { Version = "2.0.0" } : summary)]
        };

        var report = RegressionReport.Render(RegressionGate.Evaluate(upgraded, history, GateSeries.Baseline));

        Assert.Contains("Reference frameworks changed", report);
        Assert.Contains("TUnit 1.0.0 -> 2.0.0", report);
    }

    private static GateResult Suspected(GateSeries series)
    {
        var history = SyntheticRuns.Baseline(10);
        if (series == GateSeries.PullRequest)
        {
            // The predecessor regressed, which is exactly the case a read-only run must not misdescribe.
            history[^1] = SyntheticRuns.Record(
                SyntheticRuns.Samples(seed: 50, subjectMilliseconds: 300.0 * 1.25),
                runId: "baseline-suspected",
                generatedAtUtc: new DateTimeOffset(2026, 3, 20, 0, 0, 0, TimeSpan.Zero),
                verdict: RegressionVerdict.Suspected);
        }

        var regressed = SyntheticRuns.Samples(seed: 100, subjectMilliseconds: 300.0 * 1.25);
        return RegressionGate.Evaluate(SyntheticRuns.Result(regressed), history, series);
    }
}
