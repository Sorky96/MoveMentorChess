using MoveMentorChess.Profiles;

namespace MoveMentorChess.App.ViewModels;

internal static class AnalysisStoreServiceFactory
{
    public static PlayerProfileService CreatePlayerProfileService(IAnalysisStore store)
    {
        ArgumentNullException.ThrowIfNull(store);

        return new(
            store,
            store,
            store,
            store as IOpeningTheoryStore,
            store as IOpeningTreeStore,
            store as IOpeningTrainingHistoryStore);
    }

    public static OpeningTrainerWorkspaceService CreateOpeningTrainerWorkspaceService(IAnalysisStore store)
    {
        ArgumentNullException.ThrowIfNull(store);

        return new(
            store,
            store,
            store,
            store as IOpeningTheoryStore,
            store as IOpeningTrainingHistoryStore,
            store as IOpeningTrainingTelemetryStore);
    }
}
