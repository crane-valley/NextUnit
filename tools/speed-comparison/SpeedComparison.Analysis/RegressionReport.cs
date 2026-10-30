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
            Invariant($"- Primary comparable runs: {result.BaselineRunCount} of a {RegressionGate.BaselineWindow}-run window ")
            + Invariant($"(the gate arms at {RegressionGate.MinimumBaselineRuns})"));
        builder.AppendLine(
            "- Series: "
            + (result.Series == GateSeries.Baseline
                ? "default branch, appended to the history"
                // Every run that is not appended reads this way, including a manual dispatch on a side
                // branch, so the label describes what the run does rather than guessing what triggered it.
                : "read-only, not appended to the history"));
        if (result.SkippedRecordCount > 0)
        {
            builder.AppendLine(Invariant(
                $"- Skipped {result.SkippedRecordCount} recorded run(s) written by a different history schema"));
        }

        if (result.ReferenceVersionChanges.Count > 0)
        {
            // An upgrade can move same-mode ratios; naming it supplies context, not proof of causality.
            builder.AppendLine(
                "- Reference frameworks changed since the last comparable run: "
                + string.Join(", ", result.ReferenceVersionChanges));
        }

        builder.AppendLine();
        builder.AppendLine("| Participant | Normalized median | Baseline | Change | Robust spread | p | Verdict |");
        builder.AppendLine("| ----------- | ----------------: | -------: | -----: | ------------: | -: | ------- |");
        foreach (var row in result.Assessments.Select(FormatRow))
        {
            builder.AppendLine(row);
        }

        builder.AppendLine();
        builder.AppendLine("### Cross-mode diagnostic (not a gate)");
        builder.AppendLine();
        builder.AppendLine("The original all-reference ratio is retained below. A shared AOT slowdown can cancel "
            + "against the AOT reference, so investigate this signal even when the primary gate is stable. "
            + "A shift does not establish an SDK regression or a NextUnit code regression. "
            + "This diagnostic can reuse revision-1 raw samples during revision-2 warm-up; it never reuses their verdicts.");
        builder.AppendLine();
        builder.AppendLine("- Diagnostic usable runs: " + string.Join(", ", result.CrossModeAssessments.Select(
            assessment => Invariant($"{assessment.Framework}: {assessment.BaselineRunCount}"))));
        builder.AppendLine();
        builder.AppendLine("| Participant | Normalized median | Baseline | Change | Robust spread | p | Diagnostic |");
        builder.AppendLine("| ----------- | ----------------: | -------: | -----: | ------------: | -: | ---------- |");
        foreach (var row in result.CrossModeAssessments.Select(FormatRow))
        {
            builder.AppendLine(row);
        }

        builder.AppendLine();
        var aotReferences = result.Record.Participants
            .Where(participant => !RegressionGate.IsGated(participant.Framework) && RegressionGate.IsAot(participant.Framework))
            .Select(participant => participant.Framework).ToArray();
        if (result.Record.Participants.Any(participant => RegressionGate.IsGated(participant.Framework)
            && RegressionGate.IsAot(participant.Framework)))
        {
            builder.AppendLine("- AOT references: " + (aotReferences.Length == 0 ? "none; no same-mode decision" : string.Join(", ", aotReferences)));
            if (aotReferences.Length == 1)
            {
                builder.AppendLine("- Only one AOT reference is available: its changes are not diluted by averaging. "
                    + "A shared regression can be hidden in the primary ratio; review the cross-mode diagnostic, "
                    + "raw timings, and reference version changes before accepting a result.");
            }
        }

        builder.AppendLine();
        builder.AppendLine("Method:");
        builder.AppendLine();
        builder.AppendLine(
            "- Each sample is divided by the geometric mean of the same-execution-mode reference frameworks "
            + "measured in the same round. Only common multiplicative movement cancels; environment effects "
            + "need not affect AOT and managed startup equally.");
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
            // The two series stay suspected for different reasons, and saying the wrong one misleads.
            // A read-only run is capped at suspected whatever the recorded runs show, because it never
            // enters the history; only a baseline run is suspected because its predecessor was clean.
            var reason = result.Series == GateSeries.Baseline
                ? "Not failing: the run recorded before it did not regress."
                : "Not failing: this run is not appended to the history, so it can never confirm a repeat.";
            return $"**Suspected regression** in {frameworks}. {reason}";
        }

        if (result.Assessments.All(assessment => assessment.Verdict == RegressionVerdict.InsufficientBaseline))
        {
            return "Baseline is still warming up, so no decision was made."
                + (result.CrossModeAssessments.Any(assessment => assessment.Verdict == RegressionVerdict.Suspected)
                    ? " **Cross-mode movement warrants investigation**; see the diagnostic below."
                    : "");
        }

        if (result.CrossModeAssessments.Any(assessment => assessment.Verdict == RegressionVerdict.Suspected))
        {
            return "No same-mode regression. **Cross-mode movement warrants investigation**; see the diagnostic below.";
        }

        if (result.Assessments.Any(assessment => assessment.Verdict == RegressionVerdict.InsufficientBaseline))
        {
            return "No same-mode regression detected among evaluated participants; some participants have insufficient evidence.";
        }

        if (result.Assessments.Any(assessment => assessment.Verdict == RegressionVerdict.Improved))
        {
            var frameworks = Join(result.Assessments, RegressionVerdict.Improved);
            return $"No same-mode regression. Measurable relative improvement in {frameworks}.";
        }

        return "No same-mode regression.";
    }

    private static string FormatRow(ParticipantAssessment assessment)
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
        return $"| {string.Join(" | ", cells)} |";
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
