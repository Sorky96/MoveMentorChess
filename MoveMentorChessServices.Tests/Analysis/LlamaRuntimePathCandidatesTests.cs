using MoveMentorChess.Analysis;
using Xunit;

namespace MoveMentorChessServices.Tests.Analysis;

public sealed class LlamaRuntimePathCandidatesTests
{
    [Fact]
    public void GetExecutableCandidatesReturnsSharedLlamaSearchOrder()
    {
        string baseDirectory = TestPath("app");
        string currentDirectory = TestPath("repo");

        IReadOnlyList<string> candidates = LlamaRuntimePathCandidates.GetExecutableCandidates(
            "llama-server.exe",
            baseDirectory,
            currentDirectory);

        Assert.Equal(
            [
                Path.Join(baseDirectory, "llama-server.exe"),
                Path.Join(baseDirectory, "llama.cpp", "llama-server.exe"),
                Path.Join(currentDirectory, "llama-server.exe"),
                Path.Join(currentDirectory, "llama.cpp", "llama-server.exe"),
                Path.Join(currentDirectory, "tools", "llama.cpp", "llama-server.exe")
            ],
            candidates);
    }

    [Fact]
    public void GetModelDirectoriesReturnsSharedModelSearchOrder()
    {
        string baseDirectory = TestPath("app");
        string currentDirectory = TestPath("repo");

        IReadOnlyList<string> directories = LlamaRuntimePathCandidates.GetModelDirectories(
            baseDirectory,
            currentDirectory);

        Assert.Equal(
            [
                Path.Join(baseDirectory, "Models"),
                Path.Join(baseDirectory, "llama.cpp", "models"),
                Path.Join(currentDirectory, "Models"),
                Path.Join(currentDirectory, "llama.cpp", "models"),
                Path.Join(currentDirectory, "tools", "llama.cpp", "models")
            ],
            directories);
    }

    private static string TestPath(string name)
        => Path.GetFullPath(Path.Join(Path.GetTempPath(), "MoveMentorChessTests", name));
}
