using System.IO;
using System.Text.Json;
using MoveMentorChess.Localization;

namespace MoveMentorChess.Analysis;

public static class ApplicationSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private static readonly object SyncLock = new();

    public static ApplicationSettings Load()
    {
        return Load(SystemRuntimeSettingsEnvironment.Instance);
    }

    public static ApplicationSettings Load(IRuntimeSettingsEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(environment);

        lock (SyncLock)
        {
            try
            {
                string path = GetSettingsPath(environment);
                if (!environment.FileExists(path))
                {
                    return Normalize(ApplicationSettings.Default);
                }

                string json = environment.ReadAllText(path);
                ApplicationSettings? settings = JsonSerializer.Deserialize<ApplicationSettings>(json, JsonOptions);
                return Normalize(settings);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
            {
                return Normalize(ApplicationSettings.Default);
            }
        }
    }

    public static void Save(ApplicationSettings settings)
    {
        Save(settings, SystemRuntimeSettingsEnvironment.Instance);
    }

    public static void Save(ApplicationSettings settings, IRuntimeSettingsEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(environment);

        lock (SyncLock)
        {
            string path = GetSettingsPath(environment);
            string directory = Path.GetDirectoryName(path) ?? environment.BaseDirectory;
            string tempPath = Path.Join(directory, $"{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
            try
            {
                environment.CreateDirectory(directory);
                string json = JsonSerializer.Serialize(Normalize(settings), JsonOptions);
                environment.WriteAllText(tempPath, json);
                environment.ReplaceFile(tempPath, path);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                throw new ApplicationSettingsSaveException(path, ex);
            }
        }
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

        return Path.Join(baseDirectory, "MoveMentorChessServices", "settings", "application-settings.json");
    }

    private static ApplicationSettings Normalize(ApplicationSettings? settings)
    {
        settings ??= ApplicationSettings.Default;
        return new ApplicationSettings(LanguageCatalog.Resolve(settings.CultureName).CultureName);
    }
}

public sealed class ApplicationSettingsSaveException : IOException
{
    public ApplicationSettingsSaveException(string path, Exception innerException)
        : base($"Could not save application settings to '{path}'.", innerException)
    {
        Path = path;
    }

    public string Path { get; }
}
