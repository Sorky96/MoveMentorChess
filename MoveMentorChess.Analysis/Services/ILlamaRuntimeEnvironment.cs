namespace MoveMentorChess.Analysis;

public interface ILlamaRuntimeEnvironment
{
    string BaseDirectory { get; }

    string CurrentDirectory { get; }

    bool FileExists(string path);
}
