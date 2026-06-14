namespace MoveMentorChess.Domain;

public sealed record OpeningTrainingAnswerOption(
    string Id,
    string Text,
    bool IsCorrect,
    string? Explanation = null);
