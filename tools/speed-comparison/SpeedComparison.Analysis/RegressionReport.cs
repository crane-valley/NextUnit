using System.Text;

namespace SpeedComparison.Analysis;

/// <summary>Renders a gate result as the Markdown published to the job summary and the pull-request comment.</summary>
public static class RegressionReport
{
    /// <summary>Renders <paramref name="result"/>.</summary>
    public static string Render(GateResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        var builder = new StringBuilder();
        builder.AppendLine("## Performance regression check");
        builder.AppendLine();
        builder.AppendLine(Headline(result));
        builder.AppendLine();
        builder.AppendLine($"- Baseline key: `{result.BaselineKey}`");
        builder.AppendLine(
            Invariant($"- Comparable runs: {result.BaselineRunCount} of a {RegressionGate.BaselineWindow}-run window ")
            + Invariant($"(the gate arms at {RegressionGate.MinimumBaselineRuns})"));
        builder.AppendLine(
            "- Series: "
            + (result.Series == GateSeries.Baseline
                ? "default branch, appended to the history"
                : "pull request, read-only"));
        if (result.SkippedRecordCount > 0)
        {
            builder.AppendLine(Invariant(
                $"- Skipped {result.SkippedRecordCount} recorded run(s) written by a different history schema"));
        }

        builder.AppendLine();
        builder.AppendLine("| Participant | Normalized median | Baseline | Change | Robust spread | p | Verdict |");
        builder.AppendLine("| ----------- | ----------------: | -------: | -----: | ------------: | -: | ------- |");
        foreach (var assessment in result.Assessments)
        {
            string[] cells =
            [
                assessment.Framework,
                Invariant($"{assessment.CurrentNormalizedMedian:F4}"),
                FormatBaseline(assessment),
                FormatChange(assessment),
                FormatSpread(assessment),
                FormatProbability(assessment),
                Describe(assessment.Verdict)
            ];
            builder.AppendLine($"| {string.Join(" | ", cells)} |");
        }

        builder.AppendLine();
        builder.AppendLine("Method:");
        builder.AppendLine();
        builder.AppendLine(
            "- Each sample is divided by the geometric mean of the reference frameworks measured in the same "
            + "round, so machine speed, hosted image, and background load cancel out of the comparison.");
        builder.AppendLine(
            Invariant($"- A regression must be at least {RegressionGate.MinimumRelativeChange * 100:F0}% slower, exceed ")
            + Invariant($"{RegressionGate.RobustDeviationMultiple:F0} robust standard deviations of the observed ")
            + Invariant($"run-to-run spread, and clear a one-sided Mann-Whitney U test at p < {RegressionGate.SignificanceLevel:F2}."));
        builder.AppendLine(
            "- A run that clears all three is suspected, not failed. The build fails only when the recorded run "
            + "before it also regressed, which is why a single noisy median cannot gate anything.");
        return builder.ToString();
    }

    private static string Headline(GateResult result)
    {
        if (result.Assessments.Count == 0)
        {
            return "No gated participant was measured.";
        }

        if (result.HasConfirmedRegression)
        {
            var frameworks = Join(result.Assessments, RegressionVerdict.Confirmed);
            return $"**Confirmed regression** in {frameworks}. This run and the run recorded before it both regressed.";
        }

        if (result.Assessments.Any(assessment => assessment.Verdict == RegressionVerdict.Suspected))
        {
            var frameworks = Join(result.Assessments, RegressionVerdict.Suspected);
            return $"**Suspected regression** in {frameworks}. Not failing: no earlier recorded run regressed.";
        }

        if (result.Assessments.All(assessment => assessment.Verdict == RegressionVerdict.InsufficientBaseline))
        {
            return "Baseline is still warming up, so no decision was made.";
        }

        if (result.Assessments.Any(assessment => assessment.Verdict == RegressionVerdict.Improved))
        {
            var frameworks = Join(result.Assessments, RegressionVerdict.Improved);
            return $"No regression. Measurable improvement in {frameworks}.";
        }

        return "No regression.";
    }

    private static string Join(IEnumerable<ParticipantAssessment> assessments, RegressionVerdict verdict)
        => string.Join(
            ", ",
            assessments.Where(assessment => assessment.Verdict == verdict).Select(assessment => assessment.Framework));

    private static string Describe(RegressionVerdict verdict) => verdict switch
    {
        RegressionVerdict.Confirmed => "Confirmed regression",
        RegressionVerdict.Suspected => "Suspected regression",
        RegressionVerdict.Improved => "Improved",
        RegressionVerdict.Stable => "Stable",
        RegressionVerdict.InsufficientBaseline => "Baseline warming up",
        _ => "Not evaluated"
    };

    private static string FormatBaseline(ParticipantAssessment assessment)
        => assessment.BaselineRunCount < RegressionGate.MinimumBaselineRuns
            ? "-"
            : Invariant($"{assessment.BaselineNormalizedMedian:F4}");

    private static string FormatChange(ParticipantAssessment assessment)
        => assessment.BaselineRunCount < RegressionGate.MinimumBaselineRuns
            ? "-"
            : Invariant($"{assessment.RelativeChange:+0.0%;-0.0%;0.0%}");

    private static string FormatSpread(ParticipantAssessment assessment)
        => assessment.BaselineRunCount < RegressionGate.MinimumBaselineRuns
            ? "-"
            : Invariant($"{assessment.BaselineRobustDeviation:F4}");

    private static string FormatProbability(ParticipantAssessment assessment)
    {
        if (assessment.BaselineRunCount < RegressionGate.MinimumBaselineRuns)
        {
            return "-";
        }

        var probability = Math.Min(assessment.RegressionProbability, assessment.ImprovementProbability);
        return probability < 0.0001 ? "<0.0001" : Invariant($"{probability:F4}");
    }

    private static string Invariant(FormattableString text) => FormattableString.Invariant(text);
}
