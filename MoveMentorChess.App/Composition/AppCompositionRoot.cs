using Avalonia.Controls.ApplicationLifetimes;
using MoveMentorChess.App.ViewModels;
using MoveMentorChess.App.Views;
using MoveMentorChess.Localization;
using MoveMentorChess.Persistence;

namespace MoveMentorChess.App.Composition;

internal static class AppCompositionRoot
{
    private static readonly Lazy<IAppRuntimeLifecycle> RuntimeLifecycle = new(() => new LlamaAppRuntimeLifecycle());
    private static readonly Lazy<ISettingsWorkflow> SettingsWorkflow = new(() => new DefaultSettingsWorkflow(RuntimeLifecycle.Value));

    public static void ConfigureDesktopApplication(IClassicDesktopStyleApplicationLifetime desktop)
    {
        ArgumentNullException.ThrowIfNull(desktop);

        Localizer.UseApplicationCulture(SettingsWorkflow.Value.Load().ApplicationSettings.CultureName);
        RuntimeLifecycle.Value.CleanupStartupProcesses();
        desktop.Exit += (_, _) => RuntimeLifecycle.Value.ShutdownLlamaServer();
        desktop.MainWindow = CreateMainWindow();
    }

    public static MainWindow CreateMainWindow()
    {
        AppDataServiceFactory dataServiceFactory = new(AnalysisStoreProvider.GetStore);
        IAnalysisWindowFactory analysisWindowFactory = dataServiceFactory.CreateAnalysisWindowFactory();
        IProfilesWindowFactory profilesWindowFactory = dataServiceFactory.CreateProfilesWindowFactory();
        IStockfishPathResolver stockfishPathResolver = new DefaultStockfishPathResolver();
        IMainWindowAnalysisDataService mainWindowAnalysisDataService = dataServiceFactory.CreateMainWindowAnalysisDataService();
        IMainWindowEngineSession mainWindowEngineSession = new DefaultMainWindowEngineSession(stockfishPathResolver);
        IMainWindowAnalysisWorkflow mainWindowAnalysisWorkflow = new DefaultMainWindowAnalysisWorkflow(mainWindowAnalysisDataService);
        IMainWindowDialogDataService mainWindowDialogDataService = dataServiceFactory.CreateMainWindowDialogDataService();
        ISettingsWindowFactory settingsWindowFactory = new SettingsWindowFactory(SettingsWorkflow.Value);

        return new MainWindow(
            analysisWindowFactory,
            profilesWindowFactory,
            mainWindowDialogDataService,
            settingsWindowFactory)
        {
            DataContext = new MainWindowViewModel(
                mainWindowEngineSession,
                mainWindowAnalysisDataService,
                mainWindowAnalysisWorkflow)
        };
    }
}
