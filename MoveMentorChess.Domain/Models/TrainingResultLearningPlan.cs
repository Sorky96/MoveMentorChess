namespace MoveMentorChess.Domain;

public sealed record TrainingResultLearningPlan(
    string MasteredText,
    string RepeatText,
    string NextReviewText,
    string ReasonText,
    IReadOnlyList<TrainingResultReviewItem> ReviewItems);
