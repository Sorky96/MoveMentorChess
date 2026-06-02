namespace MoveMentorChess.Analysis;

public static class LlamaCppAdviceRuntimeResolver
{
    private const string PreferredModelBaseName = "MoveMentorChessServices-advice";
    private static readonly string[] PreferredModelFileNames =
    [
        "MoveMentorChessServices-advice.gguf",
        "MoveMentorChessServices-advice-q4_k_m.gguf",
        "qwen2.5-3b-instruct-q4_k_m.gguf",
        "qwen2.5-3b-instruct-q5_k_m.gguf",
        "advice-model.gguf"
    ];

    public static LlamaCppAdviceRuntime? Resolve()
    {
        return Resolve(SystemLlamaRuntimeEnvironment.Instance);
    }

    public static LlamaCppAdviceRuntime? Resolve(ILlamaRuntimeEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(environment);

        string? cliPath = ResolveCliPath(environment);
        string? modelPath = ResolveModelPath(environment);

        if (string.IsNullOrWhiteSpace(cliPath) || string.IsNullOrWhiteSpace(modelPath))
        {
            return null;
        }

        int maxTokens = ParsePositiveInt(
            environment.GetEnvironmentVariable("MoveMentorChessServices_LLAMA_CPP_MAX_TOKENS"),
            96);
        int contextSize = ParsePositiveInt(
            environment.GetEnvironmentVariable("MoveMentorChessServices_LLAMA_CPP_CONTEXT_SIZE"),
            2048);
        int timeoutMs = ParsePositiveInt(
            environment.GetEnvironmentVariable("MoveMentorChessServices_LLAMA_CPP_TIMEOUT_MS"),
            120000);
        string gpuLayersArgument = LlamaGpuSettingsResolver.ResolveGpuLayersArgument(environment);

        return new LlamaCppAdviceRuntime(cliPath, modelPath, maxTokens, contextSize, timeoutMs, gpuLayersArgument);
    }

    public static string? ResolveCliPath()
    {
        return ResolveCliPath(SystemLlamaRuntimeEnvironment.Instance);
    }

    public static string? ResolveCliPath(ILlamaRuntimeEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(environment);

        string? fromEnvironment = Normalize(environment.GetEnvironmentVariable("MoveMentorChessServices_LLAMA_CPP_CLI_PATH"));
        if (environment.FileExists(fromEnvironment ?? string.Empty))
        {
            return fromEnvironment;
        }

        foreach (string candidate in GetCliCandidates(environment))
        {
            if (environment.FileExists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    public static string? ResolveModelPath()
    {
        return ResolveModelPath(SystemLlamaRuntimeEnvironment.Instance);
    }

    public static string? ResolveModelPath(ILlamaRuntimeEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(environment);

        string? fromEnvironment = Normalize(environment.GetEnvironmentVariable("MoveMentorChessServices_LLAMA_CPP_MODEL_PATH"));
        if (environment.FileExists(fromEnvironment ?? string.Empty))
        {
            return fromEnvironment;
        }

        foreach (string candidate in GetModelCandidates(environment))
        {
            if (environment.FileExists(candidate))
            {
                return candidate;
            }
        }

        foreach (string directory in GetModelDirectories(environment))
        {
            if (!environment.DirectoryExists(directory))
            {
                continue;
            }

            string? matchingModel = environment
                .EnumerateFiles(directory, $"{PreferredModelBaseName}*.gguf")
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();

            if (!string.IsNullOrWhiteSpace(matchingModel))
            {
                return matchingModel;
            }
        }

        return null;
    }

    private static IEnumerable<string> GetCliCandidates()
        => GetCliCandidates(SystemLlamaRuntimeEnvironment.Instance);

    private static IEnumerable<string> GetCliCandidates(ILlamaRuntimeEnvironment environment)
        => LlamaRuntimePathCandidates.GetExecutableCandidates("llama-cli.exe", environment);

    private static IEnumerable<string> GetModelCandidates()
        => GetModelCandidates(SystemLlamaRuntimeEnvironment.Instance);

    private static IEnumerable<string> GetModelCandidates(ILlamaRuntimeEnvironment environment)
    {
        foreach (string directory in GetModelDirectories(environment))
        {
            foreach (string fileName in PreferredModelFileNames)
            {
                yield return Path.Combine(directory, fileName);
            }
        }
    }

    private static IEnumerable<string> GetModelDirectories()
        => GetModelDirectories(SystemLlamaRuntimeEnvironment.Instance);

    private static IEnumerable<string> GetModelDirectories(ILlamaRuntimeEnvironment environment)
        => LlamaRuntimePathCandidates.GetModelDirectories(environment);

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static int ParsePositiveInt(string? value, int fallback)
        => int.TryParse(value, out int parsed) && parsed > 0 ? parsed : fallback;
}
