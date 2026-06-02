namespace MoveMentorChess.Analysis;

public interface ILlamaRuntimeEnvironment
{
    string BaseDirectory { get; }

    string CurrentDirectory { get; }

    string? GetEnvironmentVariable(string variable);

    LlamaGpuSettings LoadLlamaGpuSettings();

    bool FileExists(string path);

    bool DirectoryExists(string path);

    IEnumerable<string> EnumerateFiles(string path, string searchPattern);
}
