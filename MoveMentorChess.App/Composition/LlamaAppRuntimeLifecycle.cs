using MoveMentorChess.Analysis;

namespace MoveMentorChess.App.Composition;

internal sealed class LlamaAppRuntimeLifecycle : IAppRuntimeLifecycle
{
    private readonly Action cleanupStartupProcesses;
    private readonly Action shutdownLlamaServer;

    public LlamaAppRuntimeLifecycle()
        : this(LlamaCppProcessCleaner.CleanupOrphanedProcesses, () => LlamaCppServerManager.Instance.Shutdown())
    {
    }

    internal LlamaAppRuntimeLifecycle(
        Action cleanupStartupProcesses,
        Action shutdownLlamaServer)
    {
        this.cleanupStartupProcesses = cleanupStartupProcesses ?? throw new ArgumentNullException(nameof(cleanupStartupProcesses));
        this.shutdownLlamaServer = shutdownLlamaServer ?? throw new ArgumentNullException(nameof(shutdownLlamaServer));
    }

    public void CleanupStartupProcesses()
    {
        cleanupStartupProcesses();
    }

    public void ShutdownLlamaServer()
    {
        shutdownLlamaServer();
    }
}
