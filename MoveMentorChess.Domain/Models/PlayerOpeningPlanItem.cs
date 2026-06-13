namespace MoveMentorChess.Domain;

public sealed record PlayerOpeningPlanItem(
    string Title,
    string Detail,
    string Evidence,
    string? Eco,
    TrainingPlanTopicCategory Category,
    int Priority,
    int EstimatedMinutes);
