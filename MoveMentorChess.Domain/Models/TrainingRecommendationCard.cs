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

public enum TrainingRecommendationDifficulty
{
    Easy,
    Medium,
    Hard
}

public enum TrainingRecommendationReasonCode
{
    StartHere,
    CoverageGap,
    WeakRecentHistory,
    HighValueTheory,
    RevisitDue
}

public enum TrainingRecommendationType
{
    General,
    Personalized,
    Recovery,
    Exploration
}
