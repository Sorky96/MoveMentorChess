namespace MoveMentorChess.Training;

public static class OpeningTrainingOutcomeSummarizer
{
    public static OpeningTrainingOutcomeSummary Build(IReadOnlyList<OpeningTrainingSessionResult>? history)
    {
        List<OpeningTrainingSessionResult> completed = (history ?? [])
            .Where(result => result.Outcome == OpeningTrainingSessionOutcome.Completed)
            .ToList();
        int attemptCount = completed.Sum(result => result.AttemptCount);
        int correctCount = completed.Sum(result => result.CorrectCount);
        int playableCount = completed.Sum(result => result.PlayableCount);
        int wrongCount = completed.Sum(result => result.WrongCount);
        double accuracy = attemptCount == 0 ? 0 : (double)(correctCount + playableCount) / attemptCount;
        double wrongRate = attemptCount == 0 ? 0 : (double)wrongCount / attemptCount;

        return new OpeningTrainingOutcomeSummary(
            completed.Count,
            attemptCount,
            correctCount,
            playableCount,
            wrongCount,
            accuracy,
            wrongRate,
            completed.Count == 0 ? null : completed.Max(result => result.CompletedUtc),
            completed
                .SelectMany(result => result.RelatedOpenings)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList(),
            completed
                .SelectMany(result => result.ThemeLabels)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList());
    }
}
