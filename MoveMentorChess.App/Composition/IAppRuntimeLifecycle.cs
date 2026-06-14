namespace MoveMentorChess.App.Composition;

internal interface IAppRuntimeLifecycle
{
    void CleanupStartupProcesses();

    void ShutdownLlamaServer();
}
