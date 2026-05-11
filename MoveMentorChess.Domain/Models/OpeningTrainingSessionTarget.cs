namespace MoveMentorChess.Domain;

public sealed record OpeningTrainingSessionTarget(
    string SourceId,
    TrainingPriorityAction Action,
    OpeningLineKey LineKey,
    OpeningBranchKey? BranchKey = null,
    OpeningPositionKey? PositionKey = null,
    string? OpponentMove = null,
    string? OpponentMoveUci = null);
