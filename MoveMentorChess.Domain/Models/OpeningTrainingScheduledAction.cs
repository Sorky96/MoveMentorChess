namespace MoveMentorChess.Domain;

public sealed record OpeningTrainingScheduledAction(
    string Id,
    string PlayerKey,
    string SessionId,
    TrainingNextActionKind Kind,
    OpeningLineKey? LineKey,
    OpeningBranchKey? BranchKey,
    OpeningPositionKey? PositionKey,
    DateTime CreatedUtc,
    DateTime DueUtc,
    OpeningTrainingScheduledActionStatus Status,
    DateTime? CompletedUtc = null,
    int Priority = 0,
    string? SourceActionId = null);

public enum OpeningTrainingScheduledActionStatus
{
    Pending,
    Completed,
    Dismissed,
    Superseded
}
