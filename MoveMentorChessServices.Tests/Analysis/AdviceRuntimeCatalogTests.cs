using MoveMentorChess.Analysis;
using Xunit;

namespace MoveMentorChessServices.Tests;

[Collection("Advice runtime process state")]
public sealed class AdviceRuntimeCatalogTests
{
    [Fact]
    public void BuildLlamaCppArgumentsUsesGrammarPromptAndModel()
    {
        IReadOnlyList<string> arguments = LlamaCppAdviceModel.BuildArguments(
            "C:\\models\\MoveMentorChessServices-advice.gguf",
            "Prompt body",
            180,
            2048,
            "all");

        Assert.Equal("-m", arguments[0]);
        Assert.Equal("C:\\models\\MoveMentorChessServices-advice.gguf", arguments[1]);
        Assert.Equal("-c", arguments[2]);
        Assert.Equal("2048", arguments[3]);
        Assert.Equal("-n", arguments[4]);
        Assert.Equal("180", arguments[5]);
        Assert.Equal("--single-turn", arguments[6]);
        Assert.Equal("--simple-io", arguments[7]);
        Assert.Equal("--no-display-prompt", arguments[8]);
        Assert.Equal("--log-disable", arguments[9]);
        Assert.Equal("-ngl", arguments[10]);
        Assert.Equal("all", arguments[11]);
        Assert.Equal("--grammar", arguments[12]);
        Assert.Contains("short_text", arguments[13], StringComparison.Ordinal);
        Assert.Equal("-p", arguments[14]);
        Assert.Equal("Prompt body", arguments[15]);
    }

    [Fact]
    public void BuildLlamaCppJsonGrammarRequiresAdviceFields()
    {
        string grammar = LlamaCppAdviceModel.BuildJsonGrammar();

        Assert.Contains("short_text", grammar, StringComparison.Ordinal);
        Assert.Contains("detailed_text", grammar, StringComparison.Ordinal);
        Assert.Contains("training_hint", grammar, StringComparison.Ordinal);
    }

    [Fact]
    public void LlamaCppAdviceRuntimeResolverUsesEnvironmentOverridesWhenFilesExist()
    {
        string tempDirectory = Path.Combine(Path.GetTempPath(), $"MoveMentorChessServices-llama-test-{Guid.NewGuid():N}");
        string cliPath = Path.Combine(tempDirectory, "llama-cli.exe");
        string modelPath = Path.Combine(tempDirectory, "MoveMentorChessServices-advice.gguf");
        FakeLlamaRuntimeEnvironment environment = new(tempDirectory, tempDirectory);
        environment.AddEnvironmentVariable("MoveMentorChessServices_LLAMA_CPP_CLI_PATH", cliPath);
        environment.AddEnvironmentVariable("MoveMentorChessServices_LLAMA_CPP_MODEL_PATH", modelPath);
        environment.AddEnvironmentVariable("MoveMentorChessServices_LLAMA_CPP_MAX_TOKENS", "190");
        environment.AddEnvironmentVariable("MoveMentorChessServices_LLAMA_CPP_CONTEXT_SIZE", "2048");
        environment.AddEnvironmentVariable("MoveMentorChessServices_LLAMA_CPP_TIMEOUT_MS", "12000");
        environment.AddEnvironmentVariable("MoveMentorChessServices_LLAMA_CPP_FULL_GPU", "false");
        environment.AddFile(cliPath);
        environment.AddFile(modelPath);

        LlamaCppAdviceRuntime? runtime = LlamaCppAdviceRuntimeResolver.Resolve(environment);

        Assert.NotNull(runtime);
        Assert.Equal(cliPath, runtime!.CliPath);
        Assert.Equal(modelPath, runtime.ModelPath);
        Assert.Equal(190, runtime.MaxTokens);
        Assert.Equal(2048, runtime.ContextSize);
        Assert.Equal(12000, runtime.TimeoutMs);
        Assert.Equal(LlamaGpuSettingsResolver.BalancedGpuLayersArgument, runtime.GpuLayersArgument);
    }

    [Fact]
    public void ReturnsReadyStatusForSupportedLlamaCppSetup()
    {
        string tempDirectory = Path.Combine(Path.GetTempPath(), $"MoveMentorChessServices-llama-status-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDirectory);
        string cliPath = Path.Combine(tempDirectory, "llama-cli.exe");
        string modelPath = Path.Combine(tempDirectory, "MoveMentorChessServices-advice.gguf");
        File.WriteAllText(cliPath, "cli");
        File.WriteAllText(modelPath, "model");

        string? previousCli = Environment.GetEnvironmentVariable("MoveMentorChessServices_LLAMA_CPP_CLI_PATH");
        string? previousModel = Environment.GetEnvironmentVariable("MoveMentorChessServices_LLAMA_CPP_MODEL_PATH");
        string? previousServer = Environment.GetEnvironmentVariable("MoveMentorChessServices_LLAMA_CPP_SERVER_PATH");
        string? previousContextSize = Environment.GetEnvironmentVariable("MoveMentorChessServices_LLAMA_CPP_CONTEXT_SIZE");
        string? previousFullGpu = Environment.GetEnvironmentVariable("MoveMentorChessServices_LLAMA_CPP_FULL_GPU");

        try
        {
            Environment.SetEnvironmentVariable("MoveMentorChessServices_LLAMA_CPP_CLI_PATH", cliPath);
            Environment.SetEnvironmentVariable("MoveMentorChessServices_LLAMA_CPP_MODEL_PATH", modelPath);
            Environment.SetEnvironmentVariable("MoveMentorChessServices_LLAMA_CPP_SERVER_PATH", null);
            Environment.SetEnvironmentVariable("MoveMentorChessServices_LLAMA_CPP_CONTEXT_SIZE", "2048");
            Environment.SetEnvironmentVariable("MoveMentorChessServices_LLAMA_CPP_FULL_GPU", "false");

            AdviceRuntimeStatus status = AdviceRuntimeCatalog.GetStatus();

            Assert.True(status.IsReady);
            Assert.Contains("llama.cpp ready", status.StatusText, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Environment.SetEnvironmentVariable("MoveMentorChessServices_LLAMA_CPP_CLI_PATH", previousCli);
            Environment.SetEnvironmentVariable("MoveMentorChessServices_LLAMA_CPP_MODEL_PATH", previousModel);
            Environment.SetEnvironmentVariable("MoveMentorChessServices_LLAMA_CPP_SERVER_PATH", previousServer);
            Environment.SetEnvironmentVariable("MoveMentorChessServices_LLAMA_CPP_CONTEXT_SIZE", previousContextSize);
            Environment.SetEnvironmentVariable("MoveMentorChessServices_LLAMA_CPP_FULL_GPU", previousFullGpu);

            if (Directory.Exists(tempDirectory))
            {
                Directory.Delete(tempDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public void ReturnsFallbackStatusWhenNoRuntimeIsConfigured()
    {
        string? previousCli = Environment.GetEnvironmentVariable("MoveMentorChessServices_LLAMA_CPP_CLI_PATH");
        string? previousModel = Environment.GetEnvironmentVariable("MoveMentorChessServices_LLAMA_CPP_MODEL_PATH");
        string? previousServer = Environment.GetEnvironmentVariable("MoveMentorChessServices_LLAMA_CPP_SERVER_PATH");
        string? previousContextSize = Environment.GetEnvironmentVariable("MoveMentorChessServices_LLAMA_CPP_CONTEXT_SIZE");
        string? previousFullGpu = Environment.GetEnvironmentVariable("MoveMentorChessServices_LLAMA_CPP_FULL_GPU");
        string? previousCommand = Environment.GetEnvironmentVariable("MoveMentorChessServices_LOCAL_ADVICE_COMMAND");
        string? previousArgs = Environment.GetEnvironmentVariable("MoveMentorChessServices_LOCAL_ADVICE_ARGS");
        string? previousWorkdir = Environment.GetEnvironmentVariable("MoveMentorChessServices_LOCAL_ADVICE_WORKDIR");

        try
        {
            Environment.SetEnvironmentVariable("MoveMentorChessServices_LLAMA_CPP_CLI_PATH", null);
            Environment.SetEnvironmentVariable("MoveMentorChessServices_LLAMA_CPP_MODEL_PATH", null);
            Environment.SetEnvironmentVariable("MoveMentorChessServices_LLAMA_CPP_SERVER_PATH", null);
            Environment.SetEnvironmentVariable("MoveMentorChessServices_LLAMA_CPP_CONTEXT_SIZE", "2048");
            Environment.SetEnvironmentVariable("MoveMentorChessServices_LLAMA_CPP_FULL_GPU", "false");
            Environment.SetEnvironmentVariable("MoveMentorChessServices_LOCAL_ADVICE_COMMAND", null);
            Environment.SetEnvironmentVariable("MoveMentorChessServices_LOCAL_ADVICE_ARGS", null);
            Environment.SetEnvironmentVariable("MoveMentorChessServices_LOCAL_ADVICE_WORKDIR", null);

            AdviceRuntimeStatus status = AdviceRuntimeCatalog.GetStatus();

            Assert.False(status.IsReady);
            Assert.Contains("heuristic fallback", status.StatusText, StringComparison.OrdinalIgnoreCase);
            Assert.NotNull(status.InstallHint);
        }
        finally
        {
            Environment.SetEnvironmentVariable("MoveMentorChessServices_LLAMA_CPP_CLI_PATH", previousCli);
            Environment.SetEnvironmentVariable("MoveMentorChessServices_LLAMA_CPP_MODEL_PATH", previousModel);
            Environment.SetEnvironmentVariable("MoveMentorChessServices_LLAMA_CPP_SERVER_PATH", previousServer);
            Environment.SetEnvironmentVariable("MoveMentorChessServices_LLAMA_CPP_CONTEXT_SIZE", previousContextSize);
            Environment.SetEnvironmentVariable("MoveMentorChessServices_LLAMA_CPP_FULL_GPU", previousFullGpu);
            Environment.SetEnvironmentVariable("MoveMentorChessServices_LOCAL_ADVICE_COMMAND", previousCommand);
            Environment.SetEnvironmentVariable("MoveMentorChessServices_LOCAL_ADVICE_ARGS", previousArgs);
            Environment.SetEnvironmentVariable("MoveMentorChessServices_LOCAL_ADVICE_WORKDIR", previousWorkdir);
        }
    }

    [Fact]
    public void LlamaCppAdviceRuntimeResolverRecognizesRecommendedQwenFileName()
    {
        string tempDirectory = Path.Combine(Path.GetTempPath(), $"MoveMentorChessServices-llama-qwen-{Guid.NewGuid():N}");
        string modelsDirectory = Path.Combine(tempDirectory, "Models");
        string cliPath = Path.Combine(tempDirectory, "llama-cli.exe");
        string modelPath = Path.Combine(modelsDirectory, "qwen2.5-3b-instruct-q4_k_m.gguf");
        FakeLlamaRuntimeEnvironment environment = new(tempDirectory, tempDirectory);
        environment.AddEnvironmentVariable("MoveMentorChessServices_LLAMA_CPP_CLI_PATH", cliPath);
        environment.AddEnvironmentVariable("MoveMentorChessServices_LLAMA_CPP_MODEL_PATH", null);
        environment.AddFile(cliPath);
        environment.AddDirectory(modelsDirectory);
        environment.AddFile(modelPath);

        LlamaCppAdviceRuntime? runtime = LlamaCppAdviceRuntimeResolver.Resolve(environment);

        Assert.NotNull(runtime);
        Assert.Equal(modelPath, runtime!.ModelPath);
    }

    [Fact]
    public void LlamaCppServerResolverUsesEnvironmentOverrideWhenFileExists()
    {
        string tempDirectory = Path.Combine(Path.GetTempPath(), $"MoveMentorChessServices-server-resolve-{Guid.NewGuid():N}");
        string serverPath = Path.Combine(tempDirectory, "llama-server.exe");
        string modelPath = Path.Combine(tempDirectory, "MoveMentorChessServices-advice.gguf");
        FakeLlamaRuntimeEnvironment environment = new(tempDirectory, tempDirectory);
        environment.AddEnvironmentVariable("MoveMentorChessServices_LLAMA_CPP_SERVER_PATH", serverPath);
        environment.AddEnvironmentVariable("MoveMentorChessServices_LLAMA_CPP_MODEL_PATH", modelPath);
        environment.AddEnvironmentVariable("MoveMentorChessServices_LLAMA_CPP_CLI_PATH", null);
        environment.AddEnvironmentVariable("MoveMentorChessServices_LLAMA_CPP_CONTEXT_SIZE", "2048");
        environment.AddEnvironmentVariable("MoveMentorChessServices_LLAMA_CPP_FULL_GPU", "false");
        environment.AddFile(serverPath);
        environment.AddFile(modelPath);

        LlamaCppServerConfig? config = LlamaCppServerResolver.Resolve(environment);

        Assert.NotNull(config);
        Assert.Equal(serverPath, config!.ServerPath);
        Assert.Equal(modelPath, config.ModelPath);
        Assert.Equal(LlamaGpuSettingsResolver.BalancedGpuLayersArgument, config.GpuLayersArgument);
    }

    [Fact]
    public void LlamaCppServerResolverReturnsNullWhenServerExeMissing()
    {
        string root = Path.Combine(Path.GetTempPath(), $"MoveMentorChessServices-missing-server-{Guid.NewGuid():N}");
        string missingServerPath = Path.Combine(root, "missing-llama-server.exe");
        FakeLlamaRuntimeEnvironment environment = new(root, root);
        environment.AddEnvironmentVariable("MoveMentorChessServices_LLAMA_CPP_SERVER_PATH", missingServerPath);

        string? serverPath = LlamaCppServerResolver.ResolveServerPath(environment);

        Assert.Null(serverPath);
    }

    [Fact]
    public void LlamaGpuSettingsResolverUsesEnvironmentOverrideForFullGpuMode()
    {
        string? previousValue = Environment.GetEnvironmentVariable("MoveMentorChessServices_LLAMA_CPP_FULL_GPU");

        try
        {
            Environment.SetEnvironmentVariable("MoveMentorChessServices_LLAMA_CPP_FULL_GPU", "true");

            string gpuLayersArgument = LlamaGpuSettingsResolver.ResolveGpuLayersArgument();

            Assert.Equal(LlamaGpuSettingsResolver.FullGpuLayersArgument, gpuLayersArgument);
        }
        finally
        {
            Environment.SetEnvironmentVariable("MoveMentorChessServices_LLAMA_CPP_FULL_GPU", previousValue);
        }
    }

    [Fact]
    public void PrefersServerOverCliWhenBothExist()
    {
        string tempDirectory = Path.Combine(Path.GetTempPath(), $"MoveMentorChessServices-catalog-priority-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDirectory);
        string serverPath = Path.Combine(tempDirectory, "llama-server.exe");
        string cliPath = Path.Combine(tempDirectory, "llama-cli.exe");
        string modelPath = Path.Combine(tempDirectory, "MoveMentorChessServices-advice.gguf");
        File.WriteAllText(serverPath, "server");
        File.WriteAllText(cliPath, "cli");
        File.WriteAllText(modelPath, "model");

        string? previousServer = Environment.GetEnvironmentVariable("MoveMentorChessServices_LLAMA_CPP_SERVER_PATH");
        string? previousCli = Environment.GetEnvironmentVariable("MoveMentorChessServices_LLAMA_CPP_CLI_PATH");
        string? previousModel = Environment.GetEnvironmentVariable("MoveMentorChessServices_LLAMA_CPP_MODEL_PATH");
        string? previousContextSize = Environment.GetEnvironmentVariable("MoveMentorChessServices_LLAMA_CPP_CONTEXT_SIZE");
        string? previousFullGpu = Environment.GetEnvironmentVariable("MoveMentorChessServices_LLAMA_CPP_FULL_GPU");

        try
        {
            Environment.SetEnvironmentVariable("MoveMentorChessServices_LLAMA_CPP_SERVER_PATH", serverPath);
            Environment.SetEnvironmentVariable("MoveMentorChessServices_LLAMA_CPP_CLI_PATH", cliPath);
            Environment.SetEnvironmentVariable("MoveMentorChessServices_LLAMA_CPP_MODEL_PATH", modelPath);
            Environment.SetEnvironmentVariable("MoveMentorChessServices_LLAMA_CPP_CONTEXT_SIZE", "2048");
            Environment.SetEnvironmentVariable("MoveMentorChessServices_LLAMA_CPP_FULL_GPU", "false");

            ILocalAdviceModel? model = AdviceRuntimeCatalog.TryCreateConfiguredModel();

            Assert.NotNull(model);
            Assert.IsType<LlamaCppHttpAdviceModel>(model);
            Assert.Equal("llama.cpp (server)", model!.Name);
        }
        finally
        {
            Environment.SetEnvironmentVariable("MoveMentorChessServices_LLAMA_CPP_SERVER_PATH", previousServer);
            Environment.SetEnvironmentVariable("MoveMentorChessServices_LLAMA_CPP_CLI_PATH", previousCli);
            Environment.SetEnvironmentVariable("MoveMentorChessServices_LLAMA_CPP_MODEL_PATH", previousModel);
            Environment.SetEnvironmentVariable("MoveMentorChessServices_LLAMA_CPP_CONTEXT_SIZE", previousContextSize);
            Environment.SetEnvironmentVariable("MoveMentorChessServices_LLAMA_CPP_FULL_GPU", previousFullGpu);

            if (Directory.Exists(tempDirectory))
            {
                Directory.Delete(tempDirectory, recursive: true);
            }
        }
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
            return environmentVariables.GetValueOrDefault(variable);
        }

        public LlamaGpuSettings LoadLlamaGpuSettings()
        {
            return LlamaGpuSettings.Default;
        }

        public bool FileExists(string path)
        {
            return files.Contains(path);
        }

        public bool DirectoryExists(string path)
        {
            return directories.Contains(path);
        }

        public IEnumerable<string> EnumerateFiles(string path, string searchPattern)
        {
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

[CollectionDefinition("Advice runtime process state", DisableParallelization = true)]
public sealed class AdviceRuntimeProcessStateGroup;
