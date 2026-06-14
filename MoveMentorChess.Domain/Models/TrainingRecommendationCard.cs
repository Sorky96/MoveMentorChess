namespace MoveMentorChess.Domain;

public sealed record TrainingRecommendationCard(
    OpeningLineCatalogItem OpeningLine,
    int EstimatedDurationMinutes,
    TrainingRecommendationDifficulty Difficulty,
    TrainingRecommendationReasonCode ReasonCode,
    TrainingRecommendationType RecommendationType,
    string Reason,
    string RecommendedAction,
    string FallbackAction,
    double Priority);
