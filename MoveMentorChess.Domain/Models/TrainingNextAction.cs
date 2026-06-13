namespace MoveMentorChess.Domain;

public sealed record TrainingNextAction(
    string Id,
    TrainingNextActionKind Kind,
    string Title,
    string Description,
    string CommandLabel,
    int Priority,
    int DelayMinutes = 0);
