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

    [Fact]
    public void GetExecutableCandidatesUsesRuntimeEnvironment()
    {
        string baseDirectory = TestPath("app-env");
        string currentDirectory = TestPath("repo-env");
        TestLlamaRuntimeEnvironment environment = new(baseDirectory, currentDirectory);

        IReadOnlyList<string> candidates = LlamaRuntimePathCandidates.GetExecutableCandidates(
            "llama-cli.exe",
            environment);

        Assert.Equal(Path.Join(baseDirectory, "llama-cli.exe"), candidates[0]);
        Assert.Equal(Path.Join(currentDirectory, "tools", "llama.cpp", "llama-cli.exe"), candidates[^1]);
    }

    private static string TestPath(string name)
        => Path.GetFullPath(Path.Join(Path.GetTempPath(), "MoveMentorChessTests", name));

    private sealed record TestLlamaRuntimeEnvironment(
        string BaseDirectory,
        string CurrentDirectory) : ILlamaRuntimeEnvironment;
}
