namespace MoveMentorChess.Analysis;

public sealed class SystemLlamaRuntimeEnvironment : ILlamaRuntimeEnvironment
{
    public static SystemLlamaRuntimeEnvironment Instance { get; } = new();

    private SystemLlamaRuntimeEnvironment()
    {
    }

    public string BaseDirectory => AppContext.BaseDirectory;

    public string CurrentDirectory => Directory.GetCurrentDirectory();

    public string? GetEnvironmentVariable(string variable) => Environment.GetEnvironmentVariable(variable);

    public LlamaGpuSettings LoadLlamaGpuSettings() => LlamaGpuSettingsStore.Load();

    public bool FileExists(string path) => File.Exists(path);

    public bool DirectoryExists(string path) => Directory.Exists(path);

    public IEnumerable<string> EnumerateFiles(string path, string searchPattern)
        => Directory.EnumerateFiles(path, searchPattern, SearchOption.TopDirectoryOnly);
}
