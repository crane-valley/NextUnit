using NextUnit;

namespace SpeedComparison.Analysis.Tests;

/// <summary>Covers the statistics the regression gate bases every decision on.</summary>
public class RobustStatisticsTests
{
    [Test]
    public void Median_OddCount_ReturnsMiddleValue()
    {
        Assert.Equal(2.0, RobustStatistics.Median([3.0, 1.0, 2.0]), 1e-12);
    }

    [Test]
    public void Median_EvenCount_AveragesTheTwoMiddleValues()
    {
        Assert.Equal(2.5, RobustStatistics.Median([4.0, 1.0, 3.0, 2.0]), 1e-12);
    }

    [Test]
    public void Median_DoesNotReorderTheInput()
    {
        var values = new[] { 3.0, 1.0, 2.0 };
        RobustStatistics.Median(values);
        Assert.Equal(3.0, values[0], 1e-12);
    }

    [Test]
    public void MedianAbsoluteDeviation_ReturnsMedianOfDeviations()
    {
        Assert.Equal(1.0, RobustStatistics.MedianAbsoluteDeviation([1.0, 2.0, 3.0, 4.0, 5.0]), 1e-12);
    }

    [Test]
    public void MedianAbsoluteDeviation_IgnoresASingleExtremeValue()
    {
        // The point of the estimator: replacing the largest sample with an absurd one must not move it.
        var calm = RobustStatistics.MedianAbsoluteDeviation([1.0, 2.0, 3.0, 4.0, 5.0]);
        var spiked = RobustStatistics.MedianAbsoluteDeviation([1.0, 2.0, 3.0, 4.0, 5000.0]);
        Assert.Equal(calm, spiked, 1e-12);
    }

    [Test]
    public void RobustStandardDeviation_ScalesTheDeviationForANormalDistribution()
    {
        Assert.Equal(1.4826, RobustStatistics.RobustStandardDeviation([1.0, 2.0, 3.0, 4.0, 5.0]), 1e-12);
    }

    [Test]
    public void GeometricMean_ReturnsTheMultiplicativeAverage()
    {
        Assert.Equal(2.0, RobustStatistics.GeometricMean([1.0, 4.0]), 1e-12);
        Assert.Equal(4.0, RobustStatistics.GeometricMean([2.0, 8.0]), 1e-12);
    }

    [Test]
    public void GeometricMean_RejectsNonPositiveValues()
    {
        Assert.Throws<ArgumentException>(() => RobustStatistics.GeometricMean([1.0, 0.0]));
    }

    [Test]
    [Arguments(0.0, 0.5)]
    [Arguments(1.96, 0.975002)]
    [Arguments(-1.96, 0.024998)]
    [Arguments(2.58, 0.995060)]
    public void NormalCumulativeDistribution_MatchesPublishedValues(double z, double expected)
    {
        Assert.Equal(expected, RobustStatistics.NormalCumulativeDistribution(z), 1e-5);
    }

    [Test]
    public void UpperTailProbability_CompletelySeparatedSamples_IsSignificant()
    {
        var probability = RobustStatistics.UpperTailProbability([6.0, 7.0, 8.0, 9.0, 10.0], [1.0, 2.0, 3.0, 4.0, 5.0]);
        Assert.InRange(probability, 0.001, 0.01);
    }

    [Test]
    public void UpperTailProbability_ReversedSeparation_IsNotSignificant()
    {
        var probability = RobustStatistics.UpperTailProbability([1.0, 2.0, 3.0, 4.0, 5.0], [6.0, 7.0, 8.0, 9.0, 10.0]);
        Assert.InRange(probability, 0.99, 1.0);
    }

    [Test]
    public void UpperTailProbability_IdenticalSamples_ReportsNoEvidence()
    {
        var probability = RobustStatistics.UpperTailProbability([1.0, 1.0, 1.0], [1.0, 1.0, 1.0]);
        Assert.Equal(1.0, probability, 1e-12);
    }

    [Test]
    public void UpperTailProbability_EmptySample_ReportsNoEvidence()
    {
        Assert.Equal(1.0, RobustStatistics.UpperTailProbability([], [1.0, 2.0]), 1e-12);
    }

    [Test]
    public void UpperTailProbability_InterleavedSamples_IsNotSignificant()
    {
        var probability = RobustStatistics.UpperTailProbability(
            [1.0, 3.0, 5.0, 7.0, 9.0],
            [2.0, 4.0, 6.0, 8.0, 10.0]);
        Assert.True(probability > 0.05, $"Interleaved samples must not look significant, but p was {probability}.");
    }
}
