namespace MoveMentorChess.Training;

public sealed class OpeningTrainingResultPlanService
{
    private const string DontKnowSubmittedMove = "I do not know";

    public TrainingResultLearningPlan BuildPlan(
        TrainingSessionOutcomeSummary summary,
        IReadOnlyList<OpeningTrainingAttemptResult> attempts,
        IReadOnlyList<TrainingNextAction> nextActions)
    {
        ArgumentNullException.ThrowIfNull(summary);
        ArgumentNullException.ThrowIfNull(attempts);
        ArgumentNullException.ThrowIfNull(nextActions);

        int mastered = summary.CorrectCount + summary.PlayableCount;
        IReadOnlyList<TrainingResultReviewItem> reviewItems = BuildReviewItems(attempts);
        TrainingNextAction? primaryAction = nextActions
            .OrderByDescending(action => action.Priority)
            .FirstOrDefault();

        return new TrainingResultLearningPlan(
            $"Mastered: {mastered}/{summary.PositionCount} positions",
            BuildRepeatText(reviewItems),
            BuildNextReviewText(primaryAction),
            BuildReasonText(summary, attempts),
            reviewItems);
    }

    private static IReadOnlyList<TrainingResultReviewItem> BuildReviewItems(IReadOnlyList<OpeningTrainingAttemptResult> attempts)
    {
        return attempts
            .Where(attempt => attempt.Score == OpeningTrainingScore.Wrong || attempt.ShouldRepeatImmediately)
            .GroupBy(attempt => attempt.PositionId, StringComparer.Ordinal)
            .Select(group =>
            {
                OpeningTrainingAttemptResult strongest = group
                    .OrderByDescending(GetReviewPriority)
                    .ThenByDescending(attempt => attempt.ShouldRepeatImmediately)
                    .First();

                return new TrainingResultReviewItem(
                    strongest.PositionId,
                    BuildMoveText(strongest),
                    BuildReviewReason(strongest),
                    GetReviewPriority(strongest));
            })
            .OrderByDescending(item => item.Priority)
            .ThenBy(item => item.MoveText, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string BuildRepeatText(IReadOnlyList<TrainingResultReviewItem> reviewItems)
    {
        if (reviewItems.Count == 0)
        {
            return "To review: no urgent positions from this run.";
        }

        TrainingResultReviewItem first = reviewItems[0];
        return reviewItems.Count == 1
            ? $"To review: {first.MoveText}"
            : $"To review: {first.MoveText} and {reviewItems.Count - 1} more position(s)";
    }

    private static string BuildNextReviewText(TrainingNextAction? action)
    {
        if (action is null)
        {
            return "Next review: no scheduled action yet.";
        }

        if (action.DelayMinutes <= 0)
        {
            return $"Next review: now - {action.Title}.";
        }

        if (action.DelayMinutes >= 1440)
        {
            return $"Next review: tomorrow - {action.Title}.";
        }

        return $"Next review: in {action.DelayMinutes} minutes - {action.Title}.";
    }

    private static string BuildReasonText(
        TrainingSessionOutcomeSummary summary,
        IReadOnlyList<OpeningTrainingAttemptResult> attempts)
    {
        int dontKnowCount = attempts.Count(IsDontKnowAttempt);
        if (dontKnowCount > 0)
        {
            return $"Reason: {dontKnowCount} position(s) were marked I don't know.";
        }

        if (summary.WrongCount > 0)
        {
            return $"Reason: {summary.WrongCount} wrong attempt(s) need reinforcement.";
        }

        if (summary.HintCount > 0)
        {
            return $"Reason: {summary.HintCount} hint(s) were used, so recall is close but not automatic.";
        }

        if (summary.PlayableCount > 0)
        {
            return "Reason: playable alternatives were accepted, but the main line should still become automatic.";
        }

        return "Reason: clean line, so spacing is enough for the next review.";
    }

    private static string BuildMoveText(OpeningTrainingAttemptResult attempt)
    {
        if (IsDontKnowAttempt(attempt))
        {
            return "position marked I don't know";
        }

        if (!string.IsNullOrWhiteSpace(attempt.ResolvedSan))
        {
            return attempt.ResolvedSan!;
        }

        if (!string.IsNullOrWhiteSpace(attempt.SubmittedMoveText))
        {
            return attempt.SubmittedMoveText;
        }

        return attempt.PositionId;
    }

    private static string BuildReviewReason(OpeningTrainingAttemptResult attempt)
    {
        if (IsDontKnowAttempt(attempt))
        {
            return "not_known";
        }

        if (attempt.ShouldRepeatImmediately)
        {
            return "wrong_attempt";
        }

        return attempt.Score == OpeningTrainingScore.Wrong
            ? "wrong_attempt"
            : "needs_review";
    }

    private static int GetReviewPriority(OpeningTrainingAttemptResult attempt)
    {
        if (IsDontKnowAttempt(attempt))
        {
            return 120;
        }

        if (attempt.ShouldRepeatImmediately)
        {
            return 100;
        }

        return attempt.Score == OpeningTrainingScore.Wrong ? 90 : 50;
    }

    private static bool IsDontKnowAttempt(OpeningTrainingAttemptResult attempt)
        => string.Equals(attempt.SubmittedMoveText, DontKnowSubmittedMove, StringComparison.OrdinalIgnoreCase);
}
