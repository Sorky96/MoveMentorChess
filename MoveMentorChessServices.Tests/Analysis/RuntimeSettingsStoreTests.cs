using MoveMentorChess.Analysis;
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

        string expectedPath = Path.Combine(@"C:\local-app-data", "MoveMentorChessServices", "settings", "stockfish-settings.json");
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

        string expectedPath = Path.Combine(@"C:\app-base", "MoveMentorChessServices", "settings", "llama-gpu-settings.json");
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

    private sealed class FakeRuntimeSettingsEnvironment(
        string localApplicationDataDirectory,
        string baseDirectory) : IRuntimeSettingsEnvironment
    {
        private readonly Dictionary<string, string> files = new(StringComparer.OrdinalIgnoreCase);

        public string LocalApplicationDataDirectory { get; } = localApplicationDataDirectory;

        public string BaseDirectory { get; } = baseDirectory;

        public HashSet<string> CreatedDirectories { get; } = new(StringComparer.OrdinalIgnoreCase);

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
    }
}
