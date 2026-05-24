using MoveMentorChess.App.ViewModels;
using MoveMentorChess.Persistence;

namespace MoveMentorChess.App.Composition;

internal sealed class AppDataServiceFactory(Func<IAnalysisStore?> analysisStoreProvider)
{
    public IAnalysisWindowFactory CreateAnalysisWindowFactory()
        => new AnalysisWindowFactory(analysisStoreProvider);

    public IProfilesWindowFactory CreateProfilesWindowFactory()
        => new ProfilesWindowFactory(analysisStoreProvider);

    public IMainWindowAnalysisDataService CreateMainWindowAnalysisDataService()
        => new DefaultMainWindowAnalysisDataService(analysisStoreProvider);

    public IMainWindowDialogDataService CreateMainWindowDialogDataService()
        => new DefaultMainWindowDialogDataService(analysisStoreProvider);
}
