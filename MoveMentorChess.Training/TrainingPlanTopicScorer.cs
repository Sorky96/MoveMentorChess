namespace MoveMentorChess.Training;

public sealed record TrainingPlanTopicScoringInput(
    string Label,
    int OccurrenceCount,
    int TotalCentipawnLoss,
    int? AverageCentipawnLoss,
    ProfileProgressDirection TrendDirection,
    GamePhase? EmphasisPhase,
    IReadOnlyList<ProfilePhaseStat> MistakesByPhase,
    IReadOnlyList<string> RelatedOpenings,
    OpeningWeaknessReport? OpeningReport,
    OpeningTrainingOutcomeSummary TrainingSummary);

public sealed record TrainingPlanTopicScoringResult(
    TrainingPlanPriorityBreakdown PriorityBreakdown,
    TrainingPlanTopicStatus Status);

public sealed class TrainingPlanTopicScorer
{
    public TrainingPlanTopicScoringResult Score(TrainingPlanTopicScoringInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        int frequencyScore = input.OccurrenceCount * 100;
        int costlyScore = input.TotalCentipawnLoss <= 0
            ? 0
            : (input.TotalCentipawnLoss * 2) + ((input.AverageCentipawnLoss ?? 0) * 3);
        int trendScore = GetTrendScore(input.TrendDirection);
        int phaseScore = GetPhaseScore(input.MistakesByPhase, input.EmphasisPhase);
        int openingWeaknessScore = GetOpeningWeaknessScore(input.Label, input.OpeningReport);
        int trainingScore = GetTrainingScore(input.Label, input.RelatedOpenings, input.TrainingSummary);
        // TrainingPlanPriorityBreakdown has no opening-weakness component yet; keep this pressure visible in TotalScore.
        int totalScore = frequencyScore + costlyScore + trendScore + phaseScore + openingWeaknessScore + trainingScore;

        return new TrainingPlanTopicScoringResult(
            new TrainingPlanPriorityBreakdown(
                frequencyScore,
                costlyScore,
                trendScore,
                phaseScore,
                totalScore,
                trainingScore),
            DetermineTopicStatus(
                input.TrendDirection,
                input.TrainingSummary,
                input.Label,
                input.RelatedOpenings,
                input.OpeningReport));
    }

    public static bool IsOpeningTrainingRelevant(
        string label,
        IReadOnlyList<string> relatedOpenings,
        OpeningTrainingOutcomeSummary trainingSummary)
    {
        if (string.Equals(label, "opening_principles", StringComparison.Ordinal))
        {
            return true;
        }

        return relatedOpenings.Count > 0
            && trainingSummary.RelatedOpenings.Any(opening =>
                relatedOpenings.Contains(opening, StringComparer.OrdinalIgnoreCase));
    }

    private static int GetTrainingScore(
        string label,
        IReadOnlyList<string> relatedOpenings,
        OpeningTrainingOutcomeSummary trainingSummary)
    {
        if (!IsOpeningTrainingRelevant(label, relatedOpenings, trainingSummary) || trainingSummary.AttemptCount == 0)
        {
            return 0;
        }

        if (trainingSummary.WrongRate >= 0.45)
        {
            return 260;
        }

        if (trainingSummary.WrongRate >= 0.25)
        {
            return 130;
        }

        return trainingSummary.Accuracy >= 0.80 ? -80 : 40;
    }

    private static TrainingPlanTopicStatus DetermineTopicStatus(
        ProfileProgressDirection trendDirection,
        OpeningTrainingOutcomeSummary trainingSummary,
        string label,
        IReadOnlyList<string> relatedOpenings,
        OpeningWeaknessReport? openingReport)
    {
        if (IsOpeningTrainingRelevant(label, relatedOpenings, trainingSummary)
            && trainingSummary.AttemptCount > 0
            && trainingSummary.WrongRate >= 0.45)
        {
            return TrainingPlanTopicStatus.Urgent;
        }

        if (trendDirection == ProfileProgressDirection.Regressing
            || (string.Equals(label, "opening_principles", StringComparison.Ordinal)
                && TrainingPlanOpeningWeaknessSelector.HasFixNowOpening(openingReport)))
        {
            return TrainingPlanTopicStatus.Urgent;
        }

        if (IsOpeningTrainingRelevant(label, relatedOpenings, trainingSummary)
            && trainingSummary.AttemptCount >= 3
            && trainingSummary.Accuracy >= 0.75)
        {
            return TrainingPlanTopicStatus.Improving;
        }

        if (trendDirection == ProfileProgressDirection.Improving)
        {
            return TrainingPlanTopicStatus.Improving;
        }

        if (trainingSummary.AttemptCount == 0 && trendDirection == ProfileProgressDirection.InsufficientData)
        {
            return TrainingPlanTopicStatus.NewWeakness;
        }

        return TrainingPlanTopicStatus.Stable;
    }

    private static int GetOpeningWeaknessScore(string label, OpeningWeaknessReport? openingReport)
    {
        if (!string.Equals(label, "opening_principles", StringComparison.Ordinal)
            || !TrainingPlanOpeningWeaknessSelector.HasActionableOpeningWeakness(openingReport))
        {
            return 0;
        }

        return TrainingPlanOpeningWeaknessSelector.GetActionableOpenings(openingReport)
            .Take(3)
            .Sum(opening => opening.Category == OpeningWeaknessCategory.FixNow ? 220 : 110);
    }

    private static int GetTrendScore(ProfileProgressDirection direction)
    {
        return direction switch
        {
            ProfileProgressDirection.Regressing => 180,
            ProfileProgressDirection.Stable => 60,
            ProfileProgressDirection.Improving => -120,
            _ => 0
        };
    }

    private static int GetPhaseScore(IReadOnlyList<ProfilePhaseStat> mistakesByPhase, GamePhase? emphasisPhase)
    {
        if (!emphasisPhase.HasValue)
        {
            return 0;
        }

        int index = mistakesByPhase
            .Select((item, itemIndex) => new { item.Phase, itemIndex })
            .Where(item => item.Phase == emphasisPhase.Value)
            .Select(item => item.itemIndex)
            .DefaultIfEmpty(mistakesByPhase.Count)
            .First();

        return index switch
        {
            0 => 90,
            1 => 55,
            2 => 25,
            _ => 10
        };
    }
}
