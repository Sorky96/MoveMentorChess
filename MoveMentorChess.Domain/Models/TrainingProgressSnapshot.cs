namespace MoveMentorChess.Domain;

public sealed record TrainingProgressSnapshot(
    int SessionCount,
    int AttemptCount,
    int CorrectCount,
    int PlayableCount,
    int WrongCount,
    double AccuracyPercent,
    DateTime? LastCompletedUtc);
