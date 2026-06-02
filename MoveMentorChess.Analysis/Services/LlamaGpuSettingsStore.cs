using System.IO;
using System.Text.Json;

namespace MoveMentorChess.Analysis;

public static class LlamaGpuSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private static readonly object SyncLock = new();

    public static LlamaGpuSettings Load()
    {
        return Load(SystemRuntimeSettingsEnvironment.Instance);
    }

    public static LlamaGpuSettings Load(IRuntimeSettingsEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(environment);

        lock (SyncLock)
        {
            try
            {
                string path = GetSettingsPath(environment);
                if (!environment.FileExists(path))
                {
                    return LlamaGpuSettings.Default;
                }

                string json = environment.ReadAllText(path);
                LlamaGpuSettings? settings = JsonSerializer.Deserialize<LlamaGpuSettings>(json, JsonOptions);
                return Normalize(settings);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
            {
                return LlamaGpuSettings.Default;
            }
        }
    }

    public static void Save(LlamaGpuSettings settings)
    {
        Save(settings, SystemRuntimeSettingsEnvironment.Instance);
    }

    public static void Save(LlamaGpuSettings settings, IRuntimeSettingsEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(environment);

        lock (SyncLock)
        {
            string path = GetSettingsPath(environment);
            string directory = Path.GetDirectoryName(path) ?? environment.BaseDirectory;
            environment.CreateDirectory(directory);
            string json = JsonSerializer.Serialize(Normalize(settings), JsonOptions);
            environment.WriteAllText(path, json);
        }
    }

    private static LlamaGpuSettings Normalize(LlamaGpuSettings? settings)
    {
        settings ??= LlamaGpuSettings.Default;
        return settings with
        {
            ServerPath = PathHelpers.NormalizePath(settings.ServerPath)
        };
    }

    public static string GetSettingsPath()
    {
        return GetSettingsPath(SystemRuntimeSettingsEnvironment.Instance);
    }

    public static string GetSettingsPath(IRuntimeSettingsEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(environment);

        string baseDirectory = environment.LocalApplicationDataDirectory;
        if (string.IsNullOrWhiteSpace(baseDirectory))
        {
            baseDirectory = environment.BaseDirectory;
        }

        return Path.Combine(baseDirectory, "MoveMentorChessServices", "settings", "llama-gpu-settings.json");
    }
}
