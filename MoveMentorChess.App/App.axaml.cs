using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using MoveMentorChess.Analysis;
using MoveMentorChess.App.Composition;

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
            LlamaCppProcessCleaner.CleanupOrphanedProcesses();
            desktop.Exit += (_, _) => LlamaCppServerManager.Instance.Shutdown();
            desktop.MainWindow = AppCompositionRoot.CreateMainWindow();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
