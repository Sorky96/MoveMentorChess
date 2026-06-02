namespace MoveMentorChess.App.ViewModels;

internal static class OpeningTrainerResultPresentation
{
    public static TrainingResultTone DetermineTone(
        bool hasSession,
        int completedSteps,
        int playableAnswers,
        int wrongAttempts,
        int hintUseCount)
    {
        if (!hasSession)
        {
            return TrainingResultTone.NotStarted;
        }

        if (wrongAttempts == 0)
        {
            return hintUseCount == 0 && playableAnswers == 0
                ? TrainingResultTone.Clean
                : TrainingResultTone.Assisted;
        }

        double wrongRatio = completedSteps == 0 ? 0d : (double)wrongAttempts / completedSteps;
        if (wrongAttempts >= 3 || wrongRatio >= 0.35)
        {
            return TrainingResultTone.HeavyRepair;
        }

        return wrongAttempts == 1
            ? TrainingResultTone.SingleRepair
            : TrainingResultTone.SeveralRepairs;
    }

    public static string BuildCompletionHeadline(int? positionCount, string openingName)
    {
        return positionCount.HasValue
            ? $"Finished {positionCount.Value} practice positions for {openingName}."
            : "Practice finished.";
    }

    public static string BuildCompletionRecommendation(
        int wrongAttempts,
        int playableAnswers,
        int transposedAnswers)
    {
        if (wrongAttempts > 0)
        {
            return "Repeat this line soon. One or more positions still need reinforcement.";
        }

        return playableAnswers > 0 || transposedAnswers > 0
            ? "The line is mostly stable. Review again after a short break to make the moves automatic."
            : "This line looks stable. You can move on to another branch or opening.";
    }

    public static string BuildBiggestWeaknessText(TrainingResultTone tone, int wrongAttempts)
    {
        return wrongAttempts > 0
            ? tone switch
            {
                TrainingResultTone.SingleRepair => "1 position is worth a calmer repeat.",
                TrainingResultTone.SeveralRepairs => $"{wrongAttempts} positions are worth a calmer repeat.",
                TrainingResultTone.HeavyRepair => $"{wrongAttempts} positions need a shorter repair pass.",
                _ => OpeningTrainerPresentationText.FormatPositionCount(wrongAttempts, "is worth", "are worth") + " a calmer repeat."
            }
            : tone == TrainingResultTone.Assisted
                ? "Recall is close. Repeat once after a short break to make it automatic."
                : "No urgent repair point from this run.";
    }

    public static string BuildCelebrationTitle(TrainingResultTone tone, int wrongAttempts)
    {
        return tone switch
        {
            TrainingResultTone.NotStarted => "Review ready",
            TrainingResultTone.Clean => "Great run. This line is stable.",
            TrainingResultTone.Assisted => "Good session. The line is almost automatic.",
            TrainingResultTone.SingleRepair => "Good session. One move needs a calmer repeat.",
            TrainingResultTone.SeveralRepairs => $"Good session. {wrongAttempts} moves need a calmer repeat.",
            TrainingResultTone.HeavyRepair => "Useful diagnostic session. Slow this line down.",
            _ => "Review ready"
        };
    }

    public static string BuildCelebrationText(TrainingResultTone tone, int completedSteps)
    {
        return tone switch
        {
            TrainingResultTone.NotStarted => "Finish practice to see what improved and what comes next.",
            TrainingResultTone.Clean => $"You completed all {completedSteps} positions without misses or hints. Let spacing do its work.",
            TrainingResultTone.Assisted => $"You completed all {completedSteps} positions. Hints or alternatives helped, so a short-break repeat will make recall cleaner.",
            TrainingResultTone.SingleRepair => "You can stop here, but one quick repeat will lock in the weak move while it is fresh.",
            TrainingResultTone.SeveralRepairs => "You can stop here, but one quick repeat will lock in the weak moves while they are fresh.",
            TrainingResultTone.HeavyRepair => "This was useful signal, not failure. Repeat fewer positions and focus on the first repair target.",
            _ => "Finish practice to see what improved and what comes next."
        };
    }

    public static string BuildOutcomeBadge(TrainingResultTone tone, int wrongAttempts)
    {
        return tone switch
        {
            TrainingResultTone.Clean => "Stable line",
            TrainingResultTone.Assisted => "Almost automatic",
            TrainingResultTone.SingleRepair => "1 repair target",
            TrainingResultTone.SeveralRepairs => $"{wrongAttempts} repair targets",
            TrainingResultTone.HeavyRepair => "Focused repair",
            _ => "Review ready"
        };
    }

    public static string BuildNextStepSummary(TrainingResultTone tone, TrainingNextActionCardViewModel? primaryNextAction)
    {
        return primaryNextAction is null
            ? "Next: finish practice to unlock a recommendation."
            : tone switch
            {
                TrainingResultTone.Clean => "Next: stop here or return tomorrow.",
                TrainingResultTone.Assisted => "Next: train another opening while this repeat waits.",
                TrainingResultTone.SingleRepair => "Next: repeat this line now.",
                TrainingResultTone.SeveralRepairs => "Next: repeat this line now.",
                TrainingResultTone.HeavyRepair => "Next: repeat a smaller repair pass.",
                _ => $"Next: {primaryNextAction.ButtonText.ToLowerInvariant()}"
            };
    }
}

public enum TrainingResultTone
{
    NotStarted,
    Clean,
    Assisted,
    SingleRepair,
    SeveralRepairs,
    HeavyRepair
}
