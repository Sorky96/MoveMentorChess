namespace MoveMentorChess.Analysis;

public interface ILlamaRuntimeEnvironment
{
    string BaseDirectory { get; }

    string CurrentDirectory { get; }

    bool FileExists(string path);

    bool DirectoryExists(string path);

    IEnumerable<string> EnumerateFiles(string path, string searchPattern);
}
