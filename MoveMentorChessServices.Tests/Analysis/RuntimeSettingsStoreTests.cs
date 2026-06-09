using MoveMentorChess.Analysis;
using MoveMentorChess.Localization;
using Xunit;

namespace MoveMentorChessServices.Tests.Analysis;

public sealed class RuntimeSettingsStoreTests
{
    [Fact]
    public void StockfishSettingsStore_UsesInjectedEnvironmentForPathAndRoundTrip()
    {
        FakeRuntimeSettingsEnvironment environment = new(@"C:\local-app-data", @"C:\app-base");
        StockfishSettings settings = new(0, 9999, 99, 99, 1, @" C:\engines\stockfish.exe ");

        StockfishSettingsStore.Save(settings, environment);
        StockfishSettings loaded = StockfishSettingsStore.Load(environment);

        string expectedPath = Path.Join(@"C:\local-app-data", "MoveMentorChessServices", "settings", "stockfish-settings.json");
        Assert.Equal(expectedPath, StockfishSettingsStore.GetSettingsPath(environment));
        Assert.Contains(Path.GetDirectoryName(expectedPath)!, environment.CreatedDirectories);
        Assert.Equal(StockfishSettings.Default.Threads, loaded.Threads);
        Assert.Equal(4096, loaded.HashMb);
        Assert.Equal(30, loaded.BulkAnalysisDepth);
        Assert.Equal(5, loaded.BulkAnalysisMultiPv);
        Assert.Equal(25, loaded.BulkAnalysisMoveTimeMs);
        Assert.Equal(@"C:\engines\stockfish.exe", loaded.ExecutablePath);
    }

    [Fact]
    public void LlamaGpuSettingsStore_FallsBackToBaseDirectoryWhenLocalAppDataIsMissing()
    {
        FakeRuntimeSettingsEnvironment environment = new(string.Empty, @"C:\app-base");
        LlamaGpuSettings settings = new(
            true,
            ExplanationLevel.Advanced,
            AdviceNarrationStyle.WittyAlien,
            @" C:\llama\llama-server.exe ");

        LlamaGpuSettingsStore.Save(settings, environment);
        LlamaGpuSettings loaded = LlamaGpuSettingsStore.Load(environment);

        string expectedPath = Path.Join(@"C:\app-base", "MoveMentorChessServices", "settings", "llama-gpu-settings.json");
        Assert.Equal(expectedPath, LlamaGpuSettingsStore.GetSettingsPath(environment));
        Assert.True(loaded.UseFullGpuPower);
        Assert.Equal(ExplanationLevel.Advanced, loaded.DefaultExplanationLevel);
        Assert.Equal(AdviceNarrationStyle.WittyAlien, loaded.NarrationStyle);
        Assert.Equal(@"C:\llama\llama-server.exe", loaded.ServerPath);
    }

    [Fact]
    public void SettingsStores_ReturnDefaultsForMissingOrInvalidFiles()
    {
        FakeRuntimeSettingsEnvironment environment = new(@"C:\local-app-data", @"C:\app-base");
        string llamaPath = LlamaGpuSettingsStore.GetSettingsPath(environment);
        environment.WriteAllText(llamaPath, "{not valid json");

        Assert.Equal(StockfishSettings.Default, StockfishSettingsStore.Load(environment));
        Assert.Equal(LlamaGpuSettings.Default, LlamaGpuSettingsStore.Load(environment));
    }

    [Fact]
    public void ApplicationSettingsStore_UsesSystemLanguageDefaultAndNormalizesUnsupportedValues()
    {
        FakeRuntimeSettingsEnvironment environment = new(@"C:\local-app-data", @"C:\app-base");

        ApplicationSettingsStore.Save(new ApplicationSettings("de-DE"), environment);
        ApplicationSettings loaded = ApplicationSettingsStore.Load(environment);

        string expectedPath = Path.Join(@"C:\local-app-data", "MoveMentorChessServices", "settings", "application-settings.json");
        Assert.Equal(expectedPath, ApplicationSettingsStore.GetSettingsPath(environment));
        Assert.Equal("de", loaded.CultureName);

        ApplicationSettingsStore.Save(new ApplicationSettings("xx-INVALID"), environment);
        Assert.Equal(LanguageCatalog.English.CultureName, ApplicationSettingsStore.Load(environment).CultureName);
    }

    [Fact]
    public void ApplicationSettingsStore_ReplacesSettingsFileFromTemporaryFile()
    {
        FakeRuntimeSettingsEnvironment environment = new(@"C:\local-app-data", @"C:\app-base");

        ApplicationSettingsStore.Save(new ApplicationSettings("pl"), environment);

        string expectedPath = ApplicationSettingsStore.GetSettingsPath(environment);
        Assert.Single(environment.ReplacedFiles);
        Assert.Equal(expectedPath, environment.ReplacedFiles[0].DestinationPath);
        Assert.EndsWith(".tmp", environment.ReplacedFiles[0].SourcePath, StringComparison.Ordinal);
        Assert.False(environment.FileExists(environment.ReplacedFiles[0].SourcePath));
        Assert.Equal("pl", ApplicationSettingsStore.Load(environment).CultureName);
    }

    [Fact]
    public void ApplicationSettingsStore_WrapsSaveIoFailures()
    {
        FakeRuntimeSettingsEnvironment environment = new(@"C:\local-app-data", @"C:\app-base")
        {
            ReplaceException = new IOException("disk full")
        };

        ApplicationSettingsSaveException exception = Assert.Throws<ApplicationSettingsSaveException>(
            () => ApplicationSettingsStore.Save(new ApplicationSettings("de"), environment));

        Assert.Equal(ApplicationSettingsStore.GetSettingsPath(environment), exception.Path);
        Assert.Same(environment.ReplaceException, exception.InnerException);
    }

    private sealed class FakeRuntimeSettingsEnvironment(
        string localApplicationDataDirectory,
        string baseDirectory) : IRuntimeSettingsEnvironment
    {
        private readonly Dictionary<string, string> files = new(StringComparer.OrdinalIgnoreCase);

        public string LocalApplicationDataDirectory { get; } = localApplicationDataDirectory;

        public string BaseDirectory { get; } = baseDirectory;

        public HashSet<string> CreatedDirectories { get; } = new(StringComparer.OrdinalIgnoreCase);

        public List<(string SourcePath, string DestinationPath)> ReplacedFiles { get; } = [];

        public IOException? ReplaceException { get; init; }

        public bool FileExists(string path) => files.ContainsKey(path);

        public string ReadAllText(string path) => files[path];

        public void CreateDirectory(string path)
        {
            CreatedDirectories.Add(path);
        }

        public void WriteAllText(string path, string contents)
        {
            files[path] = contents;
        }

        public void ReplaceFile(string sourcePath, string destinationPath)
        {
            if (ReplaceException is not null)
            {
                throw ReplaceException;
            }

            ReplacedFiles.Add((sourcePath, destinationPath));
            files[destinationPath] = files[sourcePath];
            files.Remove(sourcePath);
        }
    }
}
