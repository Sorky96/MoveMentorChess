namespace MoveMentorChess.Domain;

public sealed record PlayerOpeningPlan(
    string PlayerKey,
    string DisplayName,
    string Summary,
    IReadOnlyList<PlayerOpeningPlanItem> Today,
    IReadOnlyList<PlayerOpeningPlanItem> ThisWeek,
    IReadOnlyList<PlayerOpeningPlanItem> LongTermGaps,
    TrainingProgressSnapshot Progress);
