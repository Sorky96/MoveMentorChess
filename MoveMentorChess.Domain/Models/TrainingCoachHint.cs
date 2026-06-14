namespace MoveMentorChess.Domain;

public sealed record TrainingCoachHint(
    TrainingCoachHintLevel Level,
    string Title,
    string Text);
