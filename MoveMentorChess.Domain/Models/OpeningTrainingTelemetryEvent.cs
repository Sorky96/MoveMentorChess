namespace MoveMentorChess.Domain;

public sealed record OpeningTrainingTelemetryEvent(
    string EventName,
    DateTime CreatedUtc,
    string? PlayerKey = null,
    OpeningLineKey? LineKey = null,
    OpeningKey? OpeningKey = null,
    string? SessionId = null,
    string? RecommendationId = null,
    SpecialTrainingModeKind? SpecialMode = null,
    IReadOnlyDictionary<string, string>? Properties = null);

public static class OpeningTrainingTelemetryEvents
{
    public const string OpeningTrainerOpened = "opening_trainer_opened";
    public const string OpeningRecommendationShown = "opening_recommendation_shown";
    public const string OpeningTrainingStarted = "opening_training_started";
    public const string OverviewRecommendationSelected = "overview_recommendation_selected";
    public const string GuidedHintUsed = "guided_hint_used";
    public const string GuidedSessionAbandoned = "guided_session_abandoned";
    public const string GuidedSessionCompleted = "guided_session_completed";
    public const string ResultsNextActionClicked = "results_next_action_clicked";
    public const string SpecialModeStarted = "special_mode_started";
}
