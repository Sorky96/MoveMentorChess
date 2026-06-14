namespace MoveMentorChess.Domain;

public sealed record TrainingResultReviewItem(
    string PositionId,
    string MoveText,
    string ReasonText,
    int Priority,
    string AttemptedMoveText = "",
    string PreparedMoveText = "",
    string PriorityText = "");
