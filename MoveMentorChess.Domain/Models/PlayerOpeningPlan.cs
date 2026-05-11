namespace MoveMentorChess.Domain;

public sealed record PlayerOpeningPlan(
    string PlayerKey,
    string DisplayName,
    string Summary,
    IReadOnlyList<PlayerOpeningPlanItem> Today,
    IReadOnlyList<PlayerOpeningPlanItem> ThisWeek,
    IReadOnlyList<PlayerOpeningPlanItem> LongTermGaps,
    TrainingProgressSnapshot Progress);

public sealed record PlayerOpeningPlanItem(
    string Title,
    string Detail,
    string Evidence,
    string? Eco,
    TrainingPlanTopicCategory Category,
    int Priority,
    int EstimatedMinutes);

public sealed record TrainingProgressSnapshot(
    int SessionCount,
    int AttemptCount,
    int CorrectCount,
    int PlayableCount,
    int WrongCount,
    double AccuracyPercent,
    DateTime? LastCompletedUtc);
