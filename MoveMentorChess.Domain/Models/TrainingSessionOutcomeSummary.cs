namespace MoveMentorChess.Domain;

public sealed record TrainingSessionOutcomeSummary(
    string Headline,
    int PositionCount,
    int CompletedCount,
    int CorrectCount,
    int PlayableCount,
    int WrongCount,
    int HintCount,
    double CompletionPercent,
    double AccuracyPercent);
