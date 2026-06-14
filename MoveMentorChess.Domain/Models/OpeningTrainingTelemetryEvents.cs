namespace MoveMentorChess.Domain;

public static class OpeningTrainingTelemetryEvents
{
    public const string OpeningTrainerOpened = "opening_trainer_opened";
    public const string OpeningDailyLessonShown = "opening_daily_lesson_shown";
    public const string OpeningDailyLessonStarted = "opening_daily_lesson_started";
    public const string OpeningAdvancedOpened = "opening_advanced_opened";
    public const string OpeningReferenceRevealed = "opening_reference_revealed";
    public const string OpeningDontKnowUsed = "opening_dont_know_used";
    public const string OpeningLearningPlanShown = "opening_learning_plan_shown";
    public const string OpeningRecommendationShown = "opening_recommendation_shown";
    public const string OpeningTrainingStarted = "opening_training_started";
    public const string OverviewRecommendationSelected = "overview_recommendation_selected";
    public const string GuidedHintUsed = "guided_hint_used";
    public const string GuidedReferenceRevealed = OpeningReferenceRevealed;
    public const string GuidedDontKnowUsed = OpeningDontKnowUsed;
    public const string GuidedSessionAbandoned = "guided_session_abandoned";
    public const string GuidedSessionCompleted = "guided_session_completed";
    public const string ResultsNextActionClicked = "results_next_action_clicked";
    public const string SpecialModeStarted = "special_mode_started";
}
