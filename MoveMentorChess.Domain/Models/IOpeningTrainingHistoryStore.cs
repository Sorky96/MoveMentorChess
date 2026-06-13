namespace MoveMentorChess.Domain;

public interface IOpeningTrainingHistoryStore
{
    void SaveOpeningTrainingSessionResult(OpeningTrainingSessionResult result);
    IReadOnlyList<OpeningTrainingSessionResult> ListOpeningTrainingSessionResults(string? playerKey = null, int limit = 200);
    void SaveOpeningReviewItems(string playerKey, IReadOnlyList<OpeningReviewItem> items);
    IReadOnlyList<OpeningReviewItem> ListOpeningReviewItems(string? playerKey = null, int limit = 1000);
    void SaveOpeningTrainingScheduledActions(string playerKey, IReadOnlyList<OpeningTrainingScheduledAction> actions);
    IReadOnlyList<OpeningTrainingScheduledAction> ListDueOpeningTrainingScheduledActions(string? playerKey, DateTime nowUtc, int limit = 50);
    void MarkOpeningTrainingScheduledActionCompleted(string playerKey, string actionId, DateTime completedUtc);
}
