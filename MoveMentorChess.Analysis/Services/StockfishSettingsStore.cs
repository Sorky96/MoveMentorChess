using System.IO;
using System.Text.Json;

namespace MoveMentorChess.Analysis;

public static class StockfishSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private static readonly object SyncLock = new();

    public static StockfishSettings Load()
    {
        return Load(SystemRuntimeSettingsEnvironment.Instance);
    }

    public static StockfishSettings Load(IRuntimeSettingsEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(environment);

        lock (SyncLock)
        {
            try
            {
                string path = GetSettingsPath(environment);
                if (!environment.FileExists(path))
                {
                    return StockfishSettings.Default;
                }

                string json = environment.ReadAllText(path);
                StockfishSettings? settings = JsonSerializer.Deserialize<StockfishSettings>(json, JsonOptions);
                return Normalize(settings);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
            {
                return StockfishSettings.Default;
            }
        }
    }

    public static void Save(StockfishSettings settings)
    {
        Save(settings, SystemRuntimeSettingsEnvironment.Instance);
    }

    public static void Save(StockfishSettings settings, IRuntimeSettingsEnvironment environment)
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

    private static StockfishSettings Normalize(StockfishSettings? settings)
    {
        settings ??= StockfishSettings.Default;
        return new StockfishSettings(
            Threads: Math.Clamp(settings.Threads <= 0 ? StockfishSettings.Default.Threads : settings.Threads, 1, 64),
            HashMb: Math.Clamp(settings.HashMb <= 0 ? StockfishSettings.Default.HashMb : settings.HashMb, 16, 4096),
            BulkAnalysisDepth: Math.Clamp(settings.BulkAnalysisDepth <= 0 ? StockfishSettings.Default.BulkAnalysisDepth : settings.BulkAnalysisDepth, 1, 30),
            BulkAnalysisMultiPv: Math.Clamp(settings.BulkAnalysisMultiPv <= 0 ? StockfishSettings.Default.BulkAnalysisMultiPv : settings.BulkAnalysisMultiPv, 1, 5),
            BulkAnalysisMoveTimeMs: Math.Clamp(settings.BulkAnalysisMoveTimeMs <= 0 ? StockfishSettings.Default.BulkAnalysisMoveTimeMs : settings.BulkAnalysisMoveTimeMs, 25, 5000),
            ExecutablePath: PathHelpers.NormalizePath(settings.ExecutablePath));
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

        return Path.Combine(baseDirectory, "MoveMentorChessServices", "settings", "stockfish-settings.json");
    }
}
