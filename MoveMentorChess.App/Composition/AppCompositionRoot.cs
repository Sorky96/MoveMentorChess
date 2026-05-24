using MoveMentorChess.App.ViewModels;
using MoveMentorChess.App.Views;
using MoveMentorChess.Persistence;

namespace MoveMentorChess.App.Composition;

internal static class AppCompositionRoot
{
    public static MainWindow CreateMainWindow()
    {
        AppDataServiceFactory dataServiceFactory = new(AnalysisStoreProvider.GetStore);
        IAnalysisWindowFactory analysisWindowFactory = dataServiceFactory.CreateAnalysisWindowFactory();
        IProfilesWindowFactory profilesWindowFactory = dataServiceFactory.CreateProfilesWindowFactory();
        IStockfishPathResolver stockfishPathResolver = new DefaultStockfishPathResolver();
        IMainWindowAnalysisDataService mainWindowAnalysisDataService = dataServiceFactory.CreateMainWindowAnalysisDataService();
        IMainWindowDialogDataService mainWindowDialogDataService = dataServiceFactory.CreateMainWindowDialogDataService();

        return new MainWindow(analysisWindowFactory, profilesWindowFactory, mainWindowDialogDataService)
        {
            DataContext = new MainWindowViewModel(
                stockfishPathResolver,
                mainWindowAnalysisDataService)
        };
    }
}
