using MoveMentorChess.App.ViewModels;
using MoveMentorChess.App.Views;
using MoveMentorChess.Persistence;

namespace MoveMentorChess.App.Composition;

internal static class AppCompositionRoot
{
    public static MainWindow CreateMainWindow()
    {
        IAnalysisWindowFactory analysisWindowFactory = new AnalysisWindowFactory(AnalysisStoreProvider.GetStore);
        IProfilesWindowFactory profilesWindowFactory = new ProfilesWindowFactory(AnalysisStoreProvider.GetStore);
        IStockfishPathResolver stockfishPathResolver = new DefaultStockfishPathResolver();
        IMainWindowAnalysisDataService mainWindowAnalysisDataService = new DefaultMainWindowAnalysisDataService(AnalysisStoreProvider.GetStore);
        IMainWindowDialogDataService mainWindowDialogDataService = new DefaultMainWindowDialogDataService(AnalysisStoreProvider.GetStore);

        return new MainWindow(analysisWindowFactory, profilesWindowFactory, mainWindowDialogDataService)
        {
            DataContext = new MainWindowViewModel(
                stockfishPathResolver,
                mainWindowAnalysisDataService)
        };
    }
}
