using MoveMentorChess.App.ViewModels;
using MoveMentorChess.App.Views;
using MoveMentorChess.Persistence;

namespace MoveMentorChess.App.Composition;

internal static class AppCompositionRoot
{
    public static MainWindow CreateMainWindow()
    {
        IAnalysisWindowFactory analysisWindowFactory = new AnalysisWindowFactory();
        IProfilesWindowFactory profilesWindowFactory = new ProfilesWindowFactory(AnalysisStoreProvider.GetStore);
        IStockfishPathResolver stockfishPathResolver = new DefaultStockfishPathResolver();

        return new MainWindow(analysisWindowFactory, profilesWindowFactory)
        {
            DataContext = new MainWindowViewModel(
                stockfishPathResolver,
                AnalysisStoreProvider.GetStore)
        };
    }
}
