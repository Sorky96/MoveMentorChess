namespace MoveMentorChess.Training;

public sealed class OpeningTrainingPriorityService
{
    public IReadOnlyList<TrainingPriorityItem> BuildPriorities(
        OpeningTrainerOverview overview,
        IReadOnlyList<OpeningReviewItem> reviewItems,
        IReadOnlyList<OpeningTrainingSessionResult> sessionResults)
    {
        ArgumentNullException.ThrowIfNull(overview);
        ArgumentNullException.ThrowIfNull(reviewItems);
        ArgumentNullException.ThrowIfNull(sessionResults);

        HashSet<string> reviewedBranches = reviewItems
            .Select(item => item.BranchKey.Value)
            .ToHashSet(StringComparer.Ordinal);

        Dictionary<string, int> wrongByBranch = sessionResults
            .SelectMany(result => result.Attempts)
            .Where(attempt => attempt.Score == OpeningTrainingScore.Wrong && attempt.BranchKey.HasValue)
            .GroupBy(attempt => attempt.BranchKey!.Value.Value, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);

        Dictionary<string, int> wrongByPosition = sessionResults
            .SelectMany(result => result.Attempts)
            .Where(attempt => attempt.Score == OpeningTrainingScore.Wrong && attempt.PositionKey.HasValue)
            .GroupBy(attempt => attempt.PositionKey!.Value.Value, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);

        List<TrainingPriorityItem> priorities = [];
        priorities.AddRange(BuildBranchPriorities(overview, reviewedBranches, wrongByBranch));
        priorities.AddRange(BuildWeakPositionPriorities(overview, wrongByPosition));

        return priorities
            .OrderByDescending(item => item.Score)
            .ThenBy(item => item.Title, StringComparer.OrdinalIgnoreCase)
            .Take(8)
            .ToList();
    }

    private static IEnumerable<TrainingPriorityItem> BuildBranchPriorities(
        OpeningTrainerOverview overview,
        HashSet<string> reviewedBranches,
        IReadOnlyDictionary<string, int> wrongByBranch)
    {
        foreach (OpeningTrainingBranch branch in overview.CommonBranches)
        {
            bool isReviewed = reviewedBranches.Contains(branch.BranchKey.Value);
            int wrongCount = wrongByBranch.TryGetValue(branch.BranchKey.Value, out int value) ? value : 0;
            OpponentMoveFrequency? opponentFrequency = FindOpponentFrequency(overview.OpponentReplyProfile, branch);
            int mistakeCount = opponentFrequency?.MistakeCount ?? 0;
            double score = branch.Frequency * 4.0
                + (isReviewed ? 0 : 30)
                + wrongCount * 24
                + mistakeCount * 18;

            TrainingPriorityAction action = mistakeCount > 0
                ? TrainingPriorityAction.ReviewOpponentReply
                : TrainingPriorityAction.TrainThisBranch;
            TrainingPriorityReasonCode reason = wrongCount > 0
                ? TrainingPriorityReasonCode.RecentMistake
                : mistakeCount > 0
                    ? TrainingPriorityReasonCode.DangerousOpponentReply
                    : isReviewed
                        ? TrainingPriorityReasonCode.NeglectedBranch
                        : TrainingPriorityReasonCode.CoverageGap;

            string response = branch.RecommendedResponse is null
                ? "No prepared response is attached yet."
                : $"Recommended response: {branch.RecommendedResponse.DisplayText}.";
            string evidence = wrongCount > 0
                ? $"{wrongCount} wrong attempt(s) recorded here. {response}"
                : mistakeCount > 0
                    ? $"{mistakeCount} mistake(s) against this reply in history. {response}"
                    : $"Book frequency {branch.Frequency}. {(isReviewed ? "Already seen, but still practical." : "Not covered in review history yet.")} {response}";

            yield return new TrainingPriorityItem(
                $"branch:{branch.BranchKey.Value}",
                overview.LineKey,
                action,
                reason,
                $"Answer {branch.OpponentMove}",
                $"Prioritize this opponent reply in {overview.OpeningName}.",
                evidence,
                Math.Round(score, 2),
                Math.Clamp(4 + branch.Continuation.Count, 5, 12),
                branch.BranchKey,
                branch.ResultingPositionKey,
                branch.OpponentMove,
                branch.OpponentMoveUci);
        }
    }

    private static IEnumerable<TrainingPriorityItem> BuildWeakPositionPriorities(
        OpeningTrainerOverview overview,
        IReadOnlyDictionary<string, int> wrongByPosition)
    {
        foreach (OpeningTrainingPosition position in overview.WeakPositions)
        {
            int wrongCount = wrongByPosition.TryGetValue(position.OpeningPositionKey.Value, out int value) ? value : 0;
            double score = 55 + position.Priority * 8.0 + wrongCount * 24;
            string moveText = string.IsNullOrWhiteSpace(position.BetterMove)
                ? "Find the book repair move."
                : $"Repair move: {position.BetterMove}.";

            yield return new TrainingPriorityItem(
                $"position:{position.PositionId}",
                overview.LineKey,
                TrainingPriorityAction.RepairThisPosition,
                TrainingPriorityReasonCode.RecentMistake,
                "Repair a missed position",
                position.Instruction,
                wrongCount > 0
                    ? $"{wrongCount} wrong attempt(s) point back to this position. {moveText}"
                    : $"Saved as a weak position from previous work. {moveText}",
                Math.Round(score, 2),
                6,
                position.OpeningBranchKey,
                position.OpeningPositionKey,
                position.BetterMove,
                null);
        }
    }

    private static OpponentMoveFrequency? FindOpponentFrequency(OpponentReplyProfile profile, OpeningTrainingBranch branch)
    {
        return profile.Frequencies.FirstOrDefault(item =>
                !string.IsNullOrWhiteSpace(branch.OpponentMoveUci)
                && string.Equals(item.MoveUci, branch.OpponentMoveUci, StringComparison.OrdinalIgnoreCase))
            ?? profile.Frequencies.FirstOrDefault(item =>
                string.Equals(item.MoveSan, branch.OpponentMove, StringComparison.OrdinalIgnoreCase));
    }
}
