namespace SpeedComparison.Analysis;

/// <summary>
/// Statistics chosen to survive benchmark noise: order statistics rather than the mean, and a
/// distribution-free rank test rather than one that assumes normally distributed timings.
/// </summary>
public static class RobustStatistics
{
    /// <summary>Scales a median absolute deviation to a standard deviation for a normal distribution.</summary>
    private const double NormalConsistencyConstant = 1.4826;

    /// <summary>Returns the median of <paramref name="values"/>. The input is not modified.</summary>
    public static double Median(IReadOnlyList<double> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        if (values.Count == 0)
        {
            throw new ArgumentException("The median of an empty sample is undefined.", nameof(values));
        }

        var sorted = values.ToArray();
        Array.Sort(sorted);
        return sorted.Length % 2 == 0
            ? (sorted[sorted.Length / 2 - 1] + sorted[sorted.Length / 2]) / 2
            : sorted[sorted.Length / 2];
    }

    /// <summary>Returns the median of the absolute deviations from the median.</summary>
    public static double MedianAbsoluteDeviation(IReadOnlyList<double> values)
    {
        var median = Median(values);
        var deviations = new double[values.Count];
        for (var index = 0; index < values.Count; index++)
        {
            deviations[index] = Math.Abs(values[index] - median);
        }

        return Median(deviations);
    }

    /// <summary>
    /// Returns a standard deviation estimated from the median absolute deviation, so a single extreme
    /// sample cannot inflate the spread the gate measures a candidate against.
    /// </summary>
    public static double RobustStandardDeviation(IReadOnlyList<double> values)
        => NormalConsistencyConstant * MedianAbsoluteDeviation(values);

    /// <summary>
    /// Returns the geometric mean, which is the correct average for the reference timings because the
    /// metric built on top of it is a ratio; an arithmetic mean would let the slowest reference dominate.
    /// </summary>
    public static double GeometricMean(IReadOnlyList<double> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        if (values.Count == 0)
        {
            throw new ArgumentException("The geometric mean of an empty sample is undefined.", nameof(values));
        }

        var logarithmSum = 0.0;
        foreach (var value in values)
        {
            if (value <= 0)
            {
                throw new ArgumentException("The geometric mean requires strictly positive values.", nameof(values));
            }

            logarithmSum += Math.Log(value);
        }

        return Math.Exp(logarithmSum / values.Count);
    }

    /// <summary>
    /// Returns the one-sided Mann-Whitney U probability that <paramref name="candidate"/> is drawn from a
    /// distribution no larger than <paramref name="baseline"/>. A small value means the candidate is
    /// larger than chance explains. Ties receive average ranks and the variance is tie-corrected.
    /// </summary>
    public static double UpperTailProbability(IReadOnlyList<double> candidate, IReadOnlyList<double> baseline)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(baseline);
        if (candidate.Count == 0 || baseline.Count == 0)
        {
            return 1.0;
        }

        var combined = new (double Value, bool IsCandidate)[candidate.Count + baseline.Count];
        for (var index = 0; index < candidate.Count; index++)
        {
            combined[index] = (candidate[index], true);
        }

        for (var index = 0; index < baseline.Count; index++)
        {
            combined[candidate.Count + index] = (baseline[index], false);
        }

        Array.Sort(combined, static (left, right) => left.Value.CompareTo(right.Value));

        var candidateRankSum = 0.0;
        var tieCorrection = 0.0;
        var position = 0;
        while (position < combined.Length)
        {
            var last = position;
            while (last + 1 < combined.Length && combined[last + 1].Value.Equals(combined[position].Value))
            {
                last++;
            }

            // Ranks are one-based, so the average rank of the tied block spans position + 1 to last + 1.
            var averageRank = (position + last) / 2.0 + 1.0;
            for (var index = position; index <= last; index++)
            {
                if (combined[index].IsCandidate)
                {
                    candidateRankSum += averageRank;
                }
            }

            double tiedCount = last - position + 1;
            if (tiedCount > 1)
            {
                tieCorrection += (tiedCount * tiedCount * tiedCount) - tiedCount;
            }

            position = last + 1;
        }

        double candidateCount = candidate.Count;
        double baselineCount = baseline.Count;
        var total = candidateCount + baselineCount;
        var u = candidateRankSum - (candidateCount * (candidateCount + 1) / 2.0);
        var mean = candidateCount * baselineCount / 2.0;
        var variance = candidateCount * baselineCount / 12.0
            * (total + 1.0 - (tieCorrection / (total * (total - 1.0))));
        if (variance <= 0)
        {
            // Every observation is identical, so no ordering evidence exists either way.
            return 1.0;
        }

        // The continuity correction keeps the discrete U statistic from overstating significance.
        var z = (u - mean - 0.5) / Math.Sqrt(variance);
        return 1.0 - NormalCumulativeDistribution(z);
    }

    /// <summary>Returns the standard normal cumulative distribution function at <paramref name="z"/>.</summary>
    public static double NormalCumulativeDistribution(double z)
        => 0.5 * ComplementaryError(-z / Math.Sqrt(2));

    /// <summary>
    /// Chebyshev approximation of erfc with a fractional error below 1.2e-7, which is far tighter than the
    /// significance levels the gate compares against and avoids a numerics dependency in CI tooling.
    /// </summary>
    private static double ComplementaryError(double x)
    {
        var t = 1.0 / (1.0 + (0.5 * Math.Abs(x)));

        // Horner's method, written one step per line so the coefficient order stays checkable by eye.
        var polynomial = -0.82215223 + (t * 0.17087277);
        polynomial = 1.48851587 + (t * polynomial);
        polynomial = -1.13520398 + (t * polynomial);
        polynomial = 0.27886807 + (t * polynomial);
        polynomial = -0.18628806 + (t * polynomial);
        polynomial = 0.09678418 + (t * polynomial);
        polynomial = 0.37409196 + (t * polynomial);
        polynomial = 1.00002368 + (t * polynomial);
        polynomial = -1.26551223 + (t * polynomial);

        var series = t * Math.Exp((-x * x) + polynomial);
        return x >= 0 ? series : 2.0 - series;
    }
}
