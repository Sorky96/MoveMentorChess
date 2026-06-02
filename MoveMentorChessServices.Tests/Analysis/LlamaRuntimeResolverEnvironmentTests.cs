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

    [Fact]
    public void AdviceRuntimeResolver_UsesRuntimeEnvironmentForEnvironmentOverrides()
    {
        string root = Path.Combine(Path.GetTempPath(), "MoveMentorChessServices-llama-override-env");
        string cliPath = Path.Join(root, "custom-cli.exe");
        string modelPath = Path.Join(root, "custom-model.gguf");
        FakeLlamaRuntimeEnvironment environment = new(root, root);
        environment.AddEnvironmentVariable("MoveMentorChessServices_LLAMA_CPP_CLI_PATH", cliPath);
        environment.AddEnvironmentVariable("MoveMentorChessServices_LLAMA_CPP_MODEL_PATH", modelPath);
        environment.AddEnvironmentVariable("MoveMentorChessServices_LLAMA_CPP_FULL_GPU", "true");
        environment.AddFile(cliPath);
        environment.AddFile(modelPath);

        LlamaCppAdviceRuntime? runtime = LlamaCppAdviceRuntimeResolver.Resolve(environment);

        Assert.NotNull(runtime);
        Assert.Equal(cliPath, runtime!.CliPath);
        Assert.Equal(modelPath, runtime.ModelPath);
        Assert.Equal(LlamaGpuSettingsResolver.FullGpuLayersArgument, runtime.GpuLayersArgument);
        Assert.Contains("MoveMentorChessServices_LLAMA_CPP_CLI_PATH", environment.EnvironmentVariableChecks);
        Assert.Contains("MoveMentorChessServices_LLAMA_CPP_MODEL_PATH", environment.EnvironmentVariableChecks);
    }

    [Fact]
    public void ServerResolver_UsesRuntimeEnvironmentForSettingsAndEnvironmentOverrides()
    {
        string root = Path.Combine(Path.GetTempPath(), "MoveMentorChessServices-server-settings-env");
        string settingsServerPath = Path.Join(root, "settings-server.exe");
        string environmentServerPath = Path.Join(root, "environment-server.exe");
        FakeLlamaRuntimeEnvironment environment = new(root, root)
        {
            LlamaGpuSettings = new LlamaGpuSettings(false, ServerPath: settingsServerPath)
        };
        environment.AddEnvironmentVariable("MoveMentorChessServices_LLAMA_CPP_SERVER_PATH", environmentServerPath);
        environment.AddFile(settingsServerPath);
        environment.AddFile(environmentServerPath);

        string? resolved = LlamaCppServerResolver.ResolveServerPath(environment);

        Assert.Equal(settingsServerPath, resolved);
        Assert.Equal(1, environment.LlamaGpuSettingsLoadCount);
        Assert.DoesNotContain(environmentServerPath, environment.FileExistenceChecks);
    }

    [Fact]
    public void ServerResolver_UsesRuntimeEnvironmentForServerEnvironmentOverride()
    {
        string root = Path.Combine(Path.GetTempPath(), "MoveMentorChessServices-server-override-env");
        string serverPath = Path.Join(root, "environment-server.exe");
        FakeLlamaRuntimeEnvironment environment = new(root, root);
        environment.AddEnvironmentVariable("MoveMentorChessServices_LLAMA_CPP_SERVER_PATH", serverPath);
        environment.AddFile(serverPath);

        string? resolved = LlamaCppServerResolver.ResolveServerPath(environment);

        Assert.Equal(serverPath, resolved);
        Assert.Contains("MoveMentorChessServices_LLAMA_CPP_SERVER_PATH", environment.EnvironmentVariableChecks);
        Assert.Contains(serverPath, environment.FileExistenceChecks);
    }

    [Fact]
    public void AdviceRuntimeResolver_UsesRuntimeEnvironmentForWildcardModelDiscovery()
    {
        string root = Path.Combine(Path.GetTempPath(), "MoveMentorChessServices-llama-wildcard-env");
        string modelsDirectory = Path.Join(root, "Models");
        string modelPath = Path.Join(modelsDirectory, "MoveMentorChessServices-advice-q8_0.gguf");
        FakeLlamaRuntimeEnvironment environment = new(root, root);
        environment.AddDirectory(modelsDirectory);
        environment.AddFile(modelPath);

        string? resolved = LlamaCppAdviceRuntimeResolver.ResolveModelPath(environment);

        Assert.Equal(modelPath, resolved);
        Assert.Contains(modelsDirectory, environment.DirectoryExistenceChecks);
        Assert.Contains(
            environment.FileEnumerationChecks,
            check => check.Path == modelsDirectory
                && check.SearchPattern == "MoveMentorChessServices-advice*.gguf");
    }

    private sealed class FakeLlamaRuntimeEnvironment(
        string baseDirectory,
        string currentDirectory) : ILlamaRuntimeEnvironment
    {
        private readonly HashSet<string> files = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> directories = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string?> environmentVariables = new(StringComparer.OrdinalIgnoreCase);

        public string BaseDirectory { get; } = baseDirectory;

        public string CurrentDirectory { get; } = currentDirectory;

        public LlamaGpuSettings LlamaGpuSettings { get; init; } = LlamaGpuSettings.Default;

        public int LlamaGpuSettingsLoadCount { get; private set; }

        public List<string> EnvironmentVariableChecks { get; } = [];

        public List<string> FileExistenceChecks { get; } = [];

        public List<string> DirectoryExistenceChecks { get; } = [];

        public List<(string Path, string SearchPattern)> FileEnumerationChecks { get; } = [];

        public void AddFile(string path)
        {
            files.Add(path);
        }

        public void AddDirectory(string path)
        {
            directories.Add(path);
        }

        public void AddEnvironmentVariable(string variable, string? value)
        {
            environmentVariables[variable] = value;
        }

        public string? GetEnvironmentVariable(string variable)
        {
            EnvironmentVariableChecks.Add(variable);
            return environmentVariables.GetValueOrDefault(variable);
        }

        public LlamaGpuSettings LoadLlamaGpuSettings()
        {
            LlamaGpuSettingsLoadCount++;
            return LlamaGpuSettings;
        }

        public bool FileExists(string path)
        {
            FileExistenceChecks.Add(path);
            return files.Contains(path);
        }

        public bool DirectoryExists(string path)
        {
            DirectoryExistenceChecks.Add(path);
            return directories.Contains(path);
        }

        public IEnumerable<string> EnumerateFiles(string path, string searchPattern)
        {
            FileEnumerationChecks.Add((path, searchPattern));
            string regexPattern = "^" + System.Text.RegularExpressions.Regex.Escape(searchPattern).Replace("\\*", ".*") + "$";

            return files
                .Where(file => directories.Contains(Path.GetDirectoryName(file) ?? string.Empty)
                    && string.Equals(Path.GetDirectoryName(file), path, StringComparison.OrdinalIgnoreCase)
                    && System.Text.RegularExpressions.Regex.IsMatch(Path.GetFileName(file), regexPattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                .OrderBy(file => file, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
    }
}
