namespace MoveMentorChess.Analysis;

public sealed class SystemLlamaRuntimeEnvironment : ILlamaRuntimeEnvironment
{
    public static SystemLlamaRuntimeEnvironment Instance { get; } = new();

    private SystemLlamaRuntimeEnvironment()
    {
    }

    public string BaseDirectory => AppContext.BaseDirectory;

    public string CurrentDirectory => Directory.GetCurrentDirectory();
}
