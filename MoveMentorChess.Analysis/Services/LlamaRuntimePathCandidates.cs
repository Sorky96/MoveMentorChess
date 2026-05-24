namespace MoveMentorChess.Analysis;

public static class LlamaRuntimePathCandidates
{
    public static IReadOnlyList<string> GetExecutableCandidates(string executableName)
        => GetExecutableCandidates(executableName, AppContext.BaseDirectory, Directory.GetCurrentDirectory());

    public static IReadOnlyList<string> GetModelDirectories()
        => GetModelDirectories(AppContext.BaseDirectory, Directory.GetCurrentDirectory());

    public static IReadOnlyList<string> GetExecutableCandidates(
        string executableName,
        string baseDirectory,
        string currentDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executableName);
        ArgumentException.ThrowIfNullOrWhiteSpace(baseDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(currentDirectory);

        return
        [
            Path.Join(baseDirectory, executableName),
            Path.Join(baseDirectory, "llama.cpp", executableName),
            Path.Join(currentDirectory, executableName),
            Path.Join(currentDirectory, "llama.cpp", executableName),
            Path.Join(currentDirectory, "tools", "llama.cpp", executableName)
        ];
    }

    public static IReadOnlyList<string> GetModelDirectories(string baseDirectory, string currentDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(currentDirectory);

        return
        [
            Path.Join(baseDirectory, "Models"),
            Path.Join(baseDirectory, "llama.cpp", "models"),
            Path.Join(currentDirectory, "Models"),
            Path.Join(currentDirectory, "llama.cpp", "models"),
            Path.Join(currentDirectory, "tools", "llama.cpp", "models")
        ];
    }
}
