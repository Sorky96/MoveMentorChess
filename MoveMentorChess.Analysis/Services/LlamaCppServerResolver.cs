namespace MoveMentorChess.Analysis;

public static class LlamaCppServerResolver
{
    public static LlamaCppServerConfig? Resolve()
    {
        return Resolve(SystemLlamaRuntimeEnvironment.Instance);
    }

    public static LlamaCppServerConfig? Resolve(ILlamaRuntimeEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(environment);

        string? serverPath = ResolveServerPath(environment);
        string? modelPath = LlamaCppAdviceRuntimeResolver.ResolveModelPath(environment);

        if (string.IsNullOrWhiteSpace(serverPath) || string.IsNullOrWhiteSpace(modelPath))
        {
            return null;
        }

        int rawPort = ParsePositiveInt(
            environment.GetEnvironmentVariable("MoveMentorChessServices_LLAMA_SERVER_PORT"),
            0);
        int port = rawPort > 0 && rawPort <= 65535 ? rawPort : 0;
        int maxTokens = ParsePositiveInt(
            environment.GetEnvironmentVariable("MoveMentorChessServices_LLAMA_CPP_MAX_TOKENS"),
            256);
        int contextSize = ParsePositiveInt(
            environment.GetEnvironmentVariable("MoveMentorChessServices_LLAMA_CPP_CONTEXT_SIZE"),
            2048);
        int timeoutMs = ParsePositiveInt(
            environment.GetEnvironmentVariable("MoveMentorChessServices_LLAMA_CPP_TIMEOUT_MS"),
            30000);
        int startupTimeoutMs = ParsePositiveInt(
            environment.GetEnvironmentVariable("MoveMentorChessServices_LLAMA_SERVER_STARTUP_TIMEOUT_MS"),
            90000);
        string gpuLayersArgument = LlamaGpuSettingsResolver.ResolveGpuLayersArgument(environment);

        return new LlamaCppServerConfig(serverPath, modelPath, port, contextSize, maxTokens, timeoutMs, startupTimeoutMs, gpuLayersArgument);
    }

    public static string? ResolveServerPath()
    {
        return ResolveServerPath(SystemLlamaRuntimeEnvironment.Instance);
    }

    public static string? ResolveServerPath(ILlamaRuntimeEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(environment);

        LlamaGpuSettings settings = environment.LoadLlamaGpuSettings();
        if (environment.FileExists(settings.ServerPath ?? string.Empty))
        {
            return settings.ServerPath;
        }

        string? fromEnvironment = Normalize(environment.GetEnvironmentVariable("MoveMentorChessServices_LLAMA_CPP_SERVER_PATH"));
        if (environment.FileExists(fromEnvironment ?? string.Empty))
        {
            return fromEnvironment;
        }

        foreach (string candidate in GetServerCandidates(environment))
        {
            if (environment.FileExists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private static IEnumerable<string> GetServerCandidates()
        => GetServerCandidates(SystemLlamaRuntimeEnvironment.Instance);

    private static IEnumerable<string> GetServerCandidates(ILlamaRuntimeEnvironment environment)
        => LlamaRuntimePathCandidates.GetExecutableCandidates("llama-server.exe", environment);

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static int ParsePositiveInt(string? value, int fallback)
        => int.TryParse(value, out int parsed) && parsed > 0 ? parsed : fallback;
}
