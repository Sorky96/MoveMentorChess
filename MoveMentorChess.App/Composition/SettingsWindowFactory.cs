using MoveMentorChess.App.Views;

namespace MoveMentorChess.App.Composition;

internal sealed class SettingsWindowFactory : ISettingsWindowFactory
{
    private readonly ISettingsWorkflow settingsWorkflow;

    public SettingsWindowFactory(ISettingsWorkflow settingsWorkflow)
    {
        this.settingsWorkflow = settingsWorkflow ?? throw new ArgumentNullException(nameof(settingsWorkflow));
    }

    public SettingsWindow Create()
    {
        return new SettingsWindow(settingsWorkflow);
    }
}
