using MoveMentorChess.Analysis;
using Xunit;

namespace MoveMentorChessServices.Tests.Analysis;

public sealed class LlamaRuntimeResolverEnvironmentTests
{
    [Fact]
    public void AdviceRuntimeResolver_UsesRuntimeEnvironmentForCliAndModelFiles()
    {
        string root = Path.Combine(Path.GetTempPath(), "MoveMentorChessServices-llama-env");
        string cliPath = Path.Join(root, "llama-cli.exe");
        string modelPath = Path.Join(root, "Models", "MoveMentorChessServices-advice.gguf");
        FakeLlamaRuntimeEnvironment environment = new(root, root);
        environment.AddFile(cliPath);
        environment.AddFile(modelPath);

        LlamaCppAdviceRuntime? runtime = LlamaCppAdviceRuntimeResolver.Resolve(environment);

        Assert.NotNull(runtime);
        Assert.Equal(cliPath, runtime!.CliPath);
        Assert.Equal(modelPath, runtime.ModelPath);
        Assert.Contains(cliPath, environment.FileExistenceChecks);
        Assert.Contains(modelPath, environment.FileExistenceChecks);
    }

    [Fact]
    public void ServerResolver_UsesRuntimeEnvironmentForServerFile()
    {
        string root = Path.Combine(Path.GetTempPath(), "MoveMentorChessServices-server-env");
        string serverPath = Path.Join(root, "llama-server.exe");
        FakeLlamaRuntimeEnvironment environment = new(root, root);
        environment.AddFile(serverPath);

        string? resolved = LlamaCppServerResolver.ResolveServerPath(environment);

        Assert.Equal(serverPath, resolved);
        Assert.Contains(serverPath, environment.FileExistenceChecks);
    }

    private sealed class FakeLlamaRuntimeEnvironment(
        string baseDirectory,
        string currentDirectory) : ILlamaRuntimeEnvironment
    {
        private readonly HashSet<string> files = new(StringComparer.OrdinalIgnoreCase);

        public string BaseDirectory { get; } = baseDirectory;

        public string CurrentDirectory { get; } = currentDirectory;

        public List<string> FileExistenceChecks { get; } = [];

        public void AddFile(string path)
        {
            files.Add(path);
        }

        public bool FileExists(string path)
        {
            FileExistenceChecks.Add(path);
            return files.Contains(path);
        }
    }
}
