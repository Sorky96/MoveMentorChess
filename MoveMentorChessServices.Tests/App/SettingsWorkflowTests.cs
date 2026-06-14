using System.IO;
using MoveMentorChess.Analysis;
using MoveMentorChess.App.Composition;
using MoveMentorChess.Localization;
using Xunit;

namespace MoveMentorChessServices.Tests.App;

public sealed class SettingsWorkflowTests
{
    [Fact]
    public void Load_ReturnsStoredSettingsAndNormalizesApplicationLanguage()
    {
        LlamaGpuSettings llamaSettings = new(
            true,
            ExplanationLevel.Advanced,
            AdviceNarrationStyle.HikaruNakamura,
            @"C:\llama\llama-server.exe");
        StockfishSettings stockfishSettings = new(4, 512, 12, 3, 300, @"C:\stockfish.exe");

        DefaultSettingsWorkflow workflow = CreateWorkflow(
            loadLlamaSettings: () => llamaSettings,
            loadStockfishSettings: () => stockfishSettings,
            loadApplicationSettings: () => new ApplicationSettings("pt-PT"));

        RuntimeSettingsSnapshot snapshot = workflow.Load();

        Assert.Equal(llamaSettings, snapshot.LlamaGpuSettings);
        Assert.Equal(stockfishSettings, snapshot.StockfishSettings);
        Assert.Equal("pt-BR", snapshot.ApplicationSettings.CultureName);
    }

    [Fact]
    public void Save_PersistsSettingsAppliesCultureAndShutsDownLlamaServer()
    {
        List<string> events = [];
        RecordingRuntimeLifecycle runtimeLifecycle = new(events);
        LlamaGpuSettings llamaSettings = new(true, ExplanationLevel.Beginner, AdviceNarrationStyle.BotezLive, "llama.exe");
        StockfishSettings stockfishSettings = new(8, 1024, 14, 2, 400, "stockfish.exe");
        ApplicationSettings? savedApplicationSettings = null;
        LlamaGpuSettings? savedLlamaSettings = null;
        StockfishSettings? savedStockfishSettings = null;
        DefaultSettingsWorkflow workflow = CreateWorkflow(
            saveLlamaSettings: settings =>
            {
                events.Add("save-llama");
                savedLlamaSettings = settings;
            },
            saveStockfishSettings: settings =>
            {
                events.Add("save-stockfish");
                savedStockfishSettings = settings;
            },
            saveApplicationSettings: settings =>
            {
                events.Add($"save-application:{settings.CultureName}");
                savedApplicationSettings = settings;
            },
            applyApplicationCulture: cultureName => events.Add($"culture:{cultureName}"),
            runtimeLifecycle: runtimeLifecycle);

        workflow.Save(new RuntimeSettingsSnapshot(
            llamaSettings,
            stockfishSettings,
            new ApplicationSettings("de-DE")));

        Assert.Equal("de", savedApplicationSettings?.CultureName);
        Assert.Equal(llamaSettings, savedLlamaSettings);
        Assert.Equal(stockfishSettings, savedStockfishSettings);
        Assert.Equal(1, runtimeLifecycle.ShutdownCalls);
        Assert.Equal(
            [
                "save-application:de",
                "culture:de",
                "save-llama",
                "save-stockfish",
                "shutdown-llama"
            ],
            events);
    }

    [Fact]
    public void Save_StopsBeforeOtherSideEffectsWhenApplicationSettingsSaveFails()
    {
        List<string> events = [];
        RecordingRuntimeLifecycle runtimeLifecycle = new(events);
        ApplicationSettingsSaveException failure = new("application-settings.json", new IOException("disk full"));
        DefaultSettingsWorkflow workflow = CreateWorkflow(
            saveLlamaSettings: _ => events.Add("save-llama"),
            saveStockfishSettings: _ => events.Add("save-stockfish"),
            saveApplicationSettings: _ =>
            {
                events.Add("save-application");
                throw failure;
            },
            applyApplicationCulture: _ => events.Add("culture"),
            runtimeLifecycle: runtimeLifecycle);

        ApplicationSettingsSaveException exception = Assert.Throws<ApplicationSettingsSaveException>(
            () => workflow.Save(new RuntimeSettingsSnapshot(
                LlamaGpuSettings.Default,
                StockfishSettings.Default,
                new ApplicationSettings("pl"))));

        Assert.Same(failure, exception);
        Assert.Equal(["save-application"], events);
        Assert.Equal(0, runtimeLifecycle.ShutdownCalls);
    }

    [Fact]
    public void Save_NormalizesUnsupportedSelectedLanguageBeforePersisting()
    {
        ApplicationSettings? savedApplicationSettings = null;
        string? appliedCulture = null;
        DefaultSettingsWorkflow workflow = CreateWorkflow(
            saveApplicationSettings: settings => savedApplicationSettings = settings,
            applyApplicationCulture: cultureName => appliedCulture = cultureName);

        workflow.Save(new RuntimeSettingsSnapshot(
            LlamaGpuSettings.Default,
            StockfishSettings.Default,
            new ApplicationSettings("xx-INVALID")));

        Assert.Equal(LanguageCatalog.English.CultureName, savedApplicationSettings?.CultureName);
        Assert.Equal(LanguageCatalog.English.CultureName, appliedCulture);
    }

    private static DefaultSettingsWorkflow CreateWorkflow(
        Func<LlamaGpuSettings>? loadLlamaSettings = null,
        Action<LlamaGpuSettings>? saveLlamaSettings = null,
        Func<StockfishSettings>? loadStockfishSettings = null,
        Action<StockfishSettings>? saveStockfishSettings = null,
        Func<ApplicationSettings>? loadApplicationSettings = null,
        Action<ApplicationSettings>? saveApplicationSettings = null,
        Action<string>? applyApplicationCulture = null,
        IAppRuntimeLifecycle? runtimeLifecycle = null)
    {
        return new DefaultSettingsWorkflow(
            loadLlamaSettings ?? (() => LlamaGpuSettings.Default),
            saveLlamaSettings ?? (_ => { }),
            loadStockfishSettings ?? (() => StockfishSettings.Default),
            saveStockfishSettings ?? (_ => { }),
            loadApplicationSettings ?? (() => ApplicationSettings.Default),
            saveApplicationSettings ?? (_ => { }),
            applyApplicationCulture ?? (_ => { }),
            runtimeLifecycle ?? new RecordingRuntimeLifecycle());
    }

    private sealed class RecordingRuntimeLifecycle : IAppRuntimeLifecycle
    {
        private readonly List<string>? events;

        public RecordingRuntimeLifecycle(List<string>? events = null)
        {
            this.events = events;
        }

        public int CleanupCalls { get; private set; }

        public int ShutdownCalls { get; private set; }

        public void CleanupStartupProcesses()
        {
            CleanupCalls++;
            events?.Add("cleanup-startup");
        }

        public void ShutdownLlamaServer()
        {
            ShutdownCalls++;
            events?.Add("shutdown-llama");
        }
    }
}
