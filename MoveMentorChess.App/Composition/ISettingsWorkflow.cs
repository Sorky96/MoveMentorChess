namespace MoveMentorChess.App.Composition;

internal interface ISettingsWorkflow
{
    RuntimeSettingsSnapshot Load();

    void Save(RuntimeSettingsSnapshot settings);
}
