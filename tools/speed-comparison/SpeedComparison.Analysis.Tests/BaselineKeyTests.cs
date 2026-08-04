using NextUnit;

namespace SpeedComparison.Analysis.Tests;

/// <summary>Covers which runs the gate is willing to treat as comparable.</summary>
public class BaselineKeyTests
{
    [Test]
    public void AMeasuredRunAndItsRecordProduceTheSameKey()
    {
        var samples = SyntheticRuns.Samples(seed: 1);
        var measured = BaselineKey.For(SyntheticRuns.Result(samples));
        var recorded = BaselineKey.For(SyntheticRuns.Record(samples, "run-1", DateTimeOffset.UnixEpoch));

        Assert.Equal(measured, recorded);
    }

    [Test]
    public void PatchLevelVersionsStayComparable()
    {
        // Hosted images move the SDK and runtime patch almost every week. Splitting the baseline on that
        // would leave the gate permanently disarmed, and the metric already cancels machine differences.
        var samples = SyntheticRuns.Samples(seed: 1);
        var first = BaselineKey.For(SyntheticRuns.Result(samples, sdkVersion: "10.0.302", runtime: ".NET 10.0.10"));
        var second = BaselineKey.For(SyntheticRuns.Result(samples, sdkVersion: "10.0.415", runtime: ".NET 10.0.14"));

        Assert.Equal(first, second);
    }

    [Test]
    public void AMinorVersionChangeStartsANewBaseline()
    {
        var samples = SyntheticRuns.Samples(seed: 1);
        var first = BaselineKey.For(SyntheticRuns.Result(samples, runtime: ".NET 10.0.10"));
        var second = BaselineKey.For(SyntheticRuns.Result(samples, runtime: ".NET 11.0.1"));

        Assert.NotEqual(first, second);
    }

    [Test]
    public void ADifferentRunnerImageStartsANewBaseline()
    {
        var samples = SyntheticRuns.Samples(seed: 1);
        var first = BaselineKey.For(SyntheticRuns.Result(samples, runnerImage: "ubuntu24"));
        var second = BaselineKey.For(SyntheticRuns.Result(samples, runnerImage: "windows2025"));

        Assert.NotEqual(first, second);
    }

    [Test]
    public void ChangingTheReferenceSetStartsANewBaseline()
    {
        // The references are the denominator of the metric, so dropping one changes what every number
        // means and the old runs must stop being treated as comparable.
        var samples = SyntheticRuns.Samples(seed: 1);
        var reduced = samples.Where(entry => entry.Key != "xUnit").ToDictionary(StringComparer.Ordinal);

        Assert.NotEqual(BaselineKey.For(SyntheticRuns.Result(samples)), BaselineKey.For(SyntheticRuns.Result(reduced)));
    }

    [Test]
    public void AddingAGatedParticipantKeepsTheBaseline()
    {
        // A Native AOT participant appearing must not throw away the history of the ones already measured.
        var samples = SyntheticRuns.Samples(seed: 1);
        var extended = new Dictionary<string, double[]>(samples, StringComparer.Ordinal)
        {
            ["NextUnit (AOT)"] = [.. samples[SyntheticRuns.Subject].Select(value => value / 10)]
        };

        Assert.Equal(BaselineKey.For(SyntheticRuns.Result(samples)), BaselineKey.For(SyntheticRuns.Result(extended)));
    }

    [Test]
    public void ResizingTheWorkloadStartsANewBaseline()
    {
        // A different number of tests changes the mix of startup, scheduling, and execution that the ratio
        // measures, so the older runs stop being comparable however stable the machine was.
        var samples = SyntheticRuns.Samples(seed: 1);
        var first = BaselineKey.For(SyntheticRuns.Result(samples));
        var second = BaselineKey.For(SyntheticRuns.Result(samples) with { ExpectedTestCount = 200 });

        Assert.NotEqual(first, second);
    }

    [Test]
    public void ADifferentRoundCountStartsANewBaseline()
    {
        // A dispatch may legally ask for fewer rounds. Such a run carries too few samples for the rank
        // test to decide anything, so it must not land in the normal series as an unearned Stable and
        // displace a suspected run from the confirmation chain.
        var samples = SyntheticRuns.Samples(seed: 1);
        var first = BaselineKey.For(SyntheticRuns.Result(samples));
        var second = BaselineKey.For(SyntheticRuns.Result(samples) with { Rounds = 7 });

        Assert.NotEqual(first, second);
    }

    [Test]
    public void AShortDispatchCannotDisplaceTheRecordedChain()
    {
        var history = SyntheticRuns.Baseline(10);
        var shortRun = SyntheticRuns.Result(SyntheticRuns.Samples(seed: 100)) with { Rounds = 7 };

        var result = RegressionGate.Evaluate(shortRun, history, GateSeries.Baseline);

        Assert.Equal(0, result.BaselineRunCount);
        Assert.Equal(
            RegressionVerdict.InsufficientBaseline,
            result.Assessments.Single(assessment => assessment.Framework == SyntheticRuns.Subject).Verdict);
    }

    [Test]
    public void AReferenceFrameworkUpgradeKeepsTheBaseline()
    {
        // Reference versions are dependency-managed and move often. Keying on them would retire the
        // baseline before it could arm; the report names the change instead.
        var samples = SyntheticRuns.Samples(seed: 1);
        var upgraded = SyntheticRuns.Result(samples);
        upgraded = upgraded with
        {
            Summaries = [.. upgraded.Summaries.Select(summary =>
                summary.Framework == "TUnit" ? summary with { Version = "2.0.0" } : summary)]
        };

        Assert.Equal(BaselineKey.For(SyntheticRuns.Result(samples)), BaselineKey.For(upgraded));
    }

    [Test]
    public void TheKeyNamesTheDimensionsItSplitsOn()
    {
        var key = BaselineKey.For(SyntheticRuns.Result(SyntheticRuns.Samples(seed: 1)));

        Assert.Contains(ComparisonResult.RoundRobinBenchmarkId, key);
        Assert.Contains("127 tests", key);
        Assert.Contains("ubuntu24", key);
        Assert.Contains("sdk 10.0", key);
        Assert.Contains("runtime 10.0", key);
        Assert.Contains("references MSTest, NUnit, TUnit, xUnit", key);
    }
}
