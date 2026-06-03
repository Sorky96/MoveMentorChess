namespace MoveMentorChess.Training;

public sealed record TrainingPlanTopicNarrativeInput(
    string Label,
    int OccurrenceCount,
    int TotalCentipawnLoss,
    int? AverageCentipawnLoss,
    ProfileProgressDirection TrendDirection,
    GamePhase? WeakestPhase,
    GamePhase? EmphasisPhase,
    IReadOnlyList<string> RelatedOpenings,
    OpeningWeaknessReport? OpeningReport,
    OpeningTrainingOutcomeSummary TrainingSummary);

public sealed record TrainingPlanTopicNarrative(
    string WhyThisTopicNow,
    string Rationale);

public static class TrainingPlanTopicNarrativeBuilder
{
    public static TrainingPlanTopicNarrative Build(TrainingPlanTopicNarrativeInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        return new TrainingPlanTopicNarrative(
            BuildWhyThisTopicNow(input),
            BuildChessRationale(
                input.OccurrenceCount,
                input.TotalCentipawnLoss,
                input.AverageCentipawnLoss,
                input.TrendDirection,
                input.EmphasisPhase));
    }

    private static string BuildChessRationale(
        int occurrences,
        int totalCentipawnLoss,
        int? averageCentipawnLoss,
        ProfileProgressDirection trendDirection,
        GamePhase? emphasisPhase)
    {
        string frequencyText = occurrences <= 0
            ? "This theme has not appeared often yet, but it still deserves monitoring."
            : occurrences == 1
                ? "This theme already showed up in one analyzed mistake."
                : $"This theme keeps coming back: {occurrences} analyzed mistakes point to the same habit.";

        string costText = totalCentipawnLoss <= 0
            ? "The current sample does not show a large material or evaluation cost yet."
            : averageCentipawnLoss.HasValue
                ? $"When it appears, it is expensive: it has cost about {totalCentipawnLoss} centipawns in total, around {averageCentipawnLoss.Value} on average."
                : $"When it appears, it is expensive: it has cost about {totalCentipawnLoss} centipawns in total.";

        string trendText = trendDirection switch
        {
            ProfileProgressDirection.Regressing => "Recent games suggest this problem is becoming more urgent, not less.",
            ProfileProgressDirection.Improving => "Recent games look cleaner, so this can be trained without panic.",
            ProfileProgressDirection.Stable => "Recent games show that this habit is still stable enough to justify focused work.",
            _ => "There is not enough recent data yet, so the plan leans more on repeated mistakes than on form."
        };

        string phaseText = emphasisPhase.HasValue
            ? $"It shows up most often in the {TrainingTextFormatter.FormatPhase(emphasisPhase.Value).ToLowerInvariant()}, so that phase gets extra training time."
            : "It is not tied strongly to one phase yet, so the plan keeps the work general.";

        return $"{frequencyText} {costText} {trendText} {phaseText}";
    }

    private static string BuildWhyThisTopicNow(TrainingPlanTopicNarrativeInput input)
    {
        List<string> parts = [];

        parts.Add(input.OccurrenceCount switch
        {
            <= 0 => "Frequency: the sample is still small, so this topic is tracked conservatively.",
            1 => "Frequency: this theme already appeared in one analyzed mistake.",
            _ => $"Frequency: this theme appeared {input.OccurrenceCount} times in analyzed mistakes."
        });

        parts.Add(input.TotalCentipawnLoss <= 0
            ? "CPL cost: the current sample does not show a large centipawn penalty yet."
            : input.AverageCentipawnLoss.HasValue
                ? $"CPL cost: it has already cost {input.TotalCentipawnLoss} centipawns in total, about {input.AverageCentipawnLoss.Value} on average."
                : $"CPL cost: it has already cost {input.TotalCentipawnLoss} centipawns in total.");

        parts.Add($"Trend: {DescribeTrend(input.TrendDirection)}");

        if (input.EmphasisPhase.HasValue)
        {
            string phaseText = TrainingTextFormatter.FormatPhase(input.EmphasisPhase.Value);
            if (input.WeakestPhase.HasValue && input.WeakestPhase.Value == input.EmphasisPhase.Value)
            {
                parts.Add($"Weakest phase: it lines up with the current weakest phase, {phaseText}.");
            }
            else
            {
                parts.Add($"Weakest phase: it shows up most in {phaseText}, so the plan leans there now.");
            }
        }

        if (input.RelatedOpenings.Count > 0)
        {
            parts.Add($"Openings: it clusters around {string.Join(" / ", input.RelatedOpenings.Select(TrainingTextFormatter.FormatOpening))}.");
        }

        if (string.Equals(input.Label, "opening_principles", StringComparison.Ordinal)
            && TrainingPlanOpeningWeaknessSelector.HasActionableOpeningWeakness(input.OpeningReport))
        {
            IReadOnlyList<OpeningWeaknessEntry> urgentOpenings = TrainingPlanOpeningWeaknessSelector
                .GetActionableOpenings(input.OpeningReport)
                .Take(2)
                .ToList();
            parts.Add(
                $"Opening trainer: add focused sessions for {string.Join(" / ", urgentOpenings.Select(item => TrainingTextFormatter.FormatOpening(item.Eco)))} because the opening report marks them as unstable or costly.");
        }

        if (TrainingPlanTopicScorer.IsOpeningTrainingRelevant(input.Label, input.RelatedOpenings, input.TrainingSummary))
        {
            parts.Add(input.TrainingSummary.AttemptCount == 0
                ? "Training results: no completed opening-trainer attempts are recorded yet."
                : $"Training results: {input.TrainingSummary.CorrectCount} correct, {input.TrainingSummary.PlayableCount} playable and {input.TrainingSummary.WrongCount} wrong across {input.TrainingSummary.AttemptCount} recorded opening-trainer attempts.");
        }

        return string.Join(" ", parts);
    }

    private static string DescribeTrend(ProfileProgressDirection direction)
    {
        return direction switch
        {
            ProfileProgressDirection.Regressing => "regressing, so the topic gets extra urgency.",
            ProfileProgressDirection.Improving => "improving, so the topic is shifted toward maintenance/review.",
            ProfileProgressDirection.Stable => "stable, so it remains a consistent training target.",
            _ => "insufficient data, so priority stays anchored to frequency and CPL cost."
        };
    }

}
