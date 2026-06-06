using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using MoveMentorChess.Analysis;
using MoveMentorChess.App.Composition;
using MoveMentorChess.Localization;

namespace MoveMentorChess.App;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            Localizer.UseApplicationCulture(ApplicationSettingsStore.Load().CultureName);
            LlamaCppProcessCleaner.CleanupOrphanedProcesses();
            desktop.Exit += (_, _) => LlamaCppServerManager.Instance.Shutdown();
            desktop.MainWindow = AppCompositionRoot.CreateMainWindow();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
