using MoveMentorChess.Analysis;
using MoveMentorChess.Localization;

namespace MoveMentorChess.App.Composition;

internal sealed class DefaultSettingsWorkflow : ISettingsWorkflow
{
    private readonly Func<LlamaGpuSettings> loadLlamaSettings;
    private readonly Action<LlamaGpuSettings> saveLlamaSettings;
    private readonly Func<StockfishSettings> loadStockfishSettings;
    private readonly Action<StockfishSettings> saveStockfishSettings;
    private readonly Func<ApplicationSettings> loadApplicationSettings;
    private readonly Action<ApplicationSettings> saveApplicationSettings;
    private readonly Action<string> applyApplicationCulture;
    private readonly IAppRuntimeLifecycle runtimeLifecycle;

    public DefaultSettingsWorkflow()
        : this(new LlamaAppRuntimeLifecycle())
    {
    }

    internal DefaultSettingsWorkflow(IAppRuntimeLifecycle runtimeLifecycle)
        : this(
            LlamaGpuSettingsStore.Load,
            LlamaGpuSettingsStore.Save,
            StockfishSettingsStore.Load,
            StockfishSettingsStore.Save,
            ApplicationSettingsStore.Load,
            ApplicationSettingsStore.Save,
            Localizer.UseApplicationCulture,
            runtimeLifecycle)
    {
    }

    internal DefaultSettingsWorkflow(
        Func<LlamaGpuSettings> loadLlamaSettings,
        Action<LlamaGpuSettings> saveLlamaSettings,
        Func<StockfishSettings> loadStockfishSettings,
        Action<StockfishSettings> saveStockfishSettings,
        Func<ApplicationSettings> loadApplicationSettings,
        Action<ApplicationSettings> saveApplicationSettings,
        Action<string> applyApplicationCulture,
        IAppRuntimeLifecycle runtimeLifecycle)
    {
        this.loadLlamaSettings = loadLlamaSettings ?? throw new ArgumentNullException(nameof(loadLlamaSettings));
        this.saveLlamaSettings = saveLlamaSettings ?? throw new ArgumentNullException(nameof(saveLlamaSettings));
        this.loadStockfishSettings = loadStockfishSettings ?? throw new ArgumentNullException(nameof(loadStockfishSettings));
        this.saveStockfishSettings = saveStockfishSettings ?? throw new ArgumentNullException(nameof(saveStockfishSettings));
        this.loadApplicationSettings = loadApplicationSettings ?? throw new ArgumentNullException(nameof(loadApplicationSettings));
        this.saveApplicationSettings = saveApplicationSettings ?? throw new ArgumentNullException(nameof(saveApplicationSettings));
        this.applyApplicationCulture = applyApplicationCulture ?? throw new ArgumentNullException(nameof(applyApplicationCulture));
        this.runtimeLifecycle = runtimeLifecycle ?? throw new ArgumentNullException(nameof(runtimeLifecycle));
    }

    public RuntimeSettingsSnapshot Load()
    {
        return new RuntimeSettingsSnapshot(
            loadLlamaSettings(),
            loadStockfishSettings(),
            NormalizeApplicationSettings(loadApplicationSettings()));
    }

    public void Save(RuntimeSettingsSnapshot settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        ApplicationSettings applicationSettings = NormalizeApplicationSettings(settings.ApplicationSettings);
        string cultureName = applicationSettings.CultureName ?? LanguageCatalog.English.CultureName;
        saveApplicationSettings(applicationSettings);
        applyApplicationCulture(cultureName);
        saveLlamaSettings(settings.LlamaGpuSettings);
        saveStockfishSettings(settings.StockfishSettings);
        runtimeLifecycle.ShutdownLlamaServer();
    }

    private static ApplicationSettings NormalizeApplicationSettings(ApplicationSettings settings)
    {
        return new ApplicationSettings(LanguageCatalog.Resolve(settings.CultureName).CultureName);
    }
}
