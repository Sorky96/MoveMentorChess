using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using MoveMentorChess.Analysis;
using MoveMentorChess.Localization;

namespace MoveMentorChess.App.Views;

public partial class SettingsWindow : Window
{
    public SettingsWindow()
        : this(LlamaGpuSettingsStore.Load(), StockfishSettingsStore.Load(), ApplicationSettingsStore.Load())
    {
    }

    public SettingsWindow(
        LlamaGpuSettings settings,
        StockfishSettings stockfishSettings,
        ApplicationSettings applicationSettings)
    {
        InitializeComponent();
        Localizer.UseApplicationCulture(applicationSettings.CultureName);
        ApplyLocalizedText();
        LanguageComboBox.ItemsSource = LanguageCatalog.SupportedLanguages;
        LanguageComboBox.SelectedItem = LanguageCatalog.Resolve(applicationSettings.CultureName);
        ExplanationLevelComboBox.ItemsSource = new[]
        {
            new ExplanationLevelOption(ExplanationLevel.Beginner, Localizer.Text(LocalizedStrings.ExplanationBeginner)),
            new ExplanationLevelOption(ExplanationLevel.Intermediate, Localizer.Text(LocalizedStrings.ExplanationIntermediate)),
            new ExplanationLevelOption(ExplanationLevel.Advanced, Localizer.Text(LocalizedStrings.ExplanationAdvanced))
        };
        NarrationStyleComboBox.ItemsSource = new[]
        {
            new NarrationStyleOption(AdviceNarrationStyle.RegularTrainer, Localizer.Text(LocalizedStrings.NarrationRegularTrainer)),
            new NarrationStyleOption(AdviceNarrationStyle.LevyRozman, Localizer.Text(LocalizedStrings.NarrationLevyRozman)),
            new NarrationStyleOption(AdviceNarrationStyle.HikaruNakamura, Localizer.Text(LocalizedStrings.NarrationHikaruNakamura)),
            new NarrationStyleOption(AdviceNarrationStyle.BotezLive, Localizer.Text(LocalizedStrings.NarrationBotezLive)),
            new NarrationStyleOption(AdviceNarrationStyle.WittyAlien, Localizer.Text(LocalizedStrings.NarrationWittyAlien))
        };

        FullGpuPowerCheckBox.IsChecked = settings.UseFullGpuPower;
        LlamaServerPathTextBox.Text = settings.ServerPath;
        StockfishPathTextBox.Text = stockfishSettings.ExecutablePath;
        StockfishThreadsNumeric.Value = stockfishSettings.Threads;
        StockfishHashNumeric.Value = stockfishSettings.HashMb;
        BulkDepthNumeric.Value = stockfishSettings.BulkAnalysisDepth;
        BulkMultiPvNumeric.Value = stockfishSettings.BulkAnalysisMultiPv;
        BulkMoveTimeNumeric.Value = stockfishSettings.BulkAnalysisMoveTimeMs;
        ExplanationLevelComboBox.SelectedItem = ExplanationLevelComboBox.Items
            .OfType<ExplanationLevelOption>()
            .FirstOrDefault(option => option.Level == settings.DefaultExplanationLevel);
        NarrationStyleComboBox.SelectedItem = NarrationStyleComboBox.Items
            .OfType<NarrationStyleOption>()
            .FirstOrDefault(option => option.Style == settings.NarrationStyle);
        FullGpuPowerCheckBox.IsCheckedChanged += (_, _) => RefreshModeDescription();
        StockfishThreadsNumeric.ValueChanged += (_, _) => RefreshStockfishDescription();
        StockfishHashNumeric.ValueChanged += (_, _) => RefreshStockfishDescription();
        BulkDepthNumeric.ValueChanged += (_, _) => RefreshStockfishDescription();
        BulkMultiPvNumeric.ValueChanged += (_, _) => RefreshStockfishDescription();
        BulkMoveTimeNumeric.ValueChanged += (_, _) => RefreshStockfishDescription();
        RefreshModeDescription();
        RefreshStockfishDescription();
    }

    public LlamaGpuSettings SelectedSettings =>
        new(
            FullGpuPowerCheckBox.IsChecked == true,
            ExplanationLevelComboBox.SelectedItem is ExplanationLevelOption levelOption
                ? levelOption.Level
                : ExplanationLevel.Intermediate,
            NarrationStyleComboBox.SelectedItem is NarrationStyleOption narrationOption
                ? narrationOption.Style
                : AdviceNarrationStyle.RegularTrainer,
            PathHelpers.NormalizePath(LlamaServerPathTextBox.Text));

    public StockfishSettings SelectedStockfishSettings =>
        new(
            ReadInt(StockfishThreadsNumeric, StockfishSettings.Default.Threads),
            ReadInt(StockfishHashNumeric, StockfishSettings.Default.HashMb),
            ReadInt(BulkDepthNumeric, StockfishSettings.Default.BulkAnalysisDepth),
            ReadInt(BulkMultiPvNumeric, StockfishSettings.Default.BulkAnalysisMultiPv),
            ReadInt(BulkMoveTimeNumeric, StockfishSettings.Default.BulkAnalysisMoveTimeMs),
            PathHelpers.NormalizePath(StockfishPathTextBox.Text));

    public ApplicationSettings SelectedApplicationSettings =>
        new(LanguageComboBox.SelectedItem is LanguageOption language
            ? language.CultureName
            : LanguageCatalog.English.CultureName);

    private void SaveButton_Click(object? sender, RoutedEventArgs e)
    {
        ApplicationSettingsStore.Save(SelectedApplicationSettings);
        Localizer.UseApplicationCulture(SelectedApplicationSettings.CultureName);
        LlamaGpuSettingsStore.Save(SelectedSettings);
        StockfishSettingsStore.Save(SelectedStockfishSettings);
        LlamaCppServerManager.Instance.Shutdown();
        Close(true);
    }

    private void CancelButton_Click(object? sender, RoutedEventArgs e)
    {
        Close(false);
    }

    private async void BrowseStockfishButton_Click(object? sender, RoutedEventArgs e)
    {
        string? path = await PickExecutablePathAsync(Localizer.Text(LocalizedStrings.SettingsSelectStockfishExecutable));
        if (!string.IsNullOrWhiteSpace(path))
        {
            StockfishPathTextBox.Text = path;
        }
    }

    private async void BrowseLlamaServerButton_Click(object? sender, RoutedEventArgs e)
    {
        string? path = await PickExecutablePathAsync(Localizer.Text(LocalizedStrings.SettingsSelectLlamaServerExecutable));
        if (!string.IsNullOrWhiteSpace(path))
        {
            LlamaServerPathTextBox.Text = path;
        }
    }

    private void RefreshModeDescription()
    {
        bool useFullGpuPower = FullGpuPowerCheckBox.IsChecked == true;
        ModeDescriptionTextBlock.Text = useFullGpuPower
            ? Localizer.Format(LocalizedStrings.SettingsFullGpuDescription, LlamaGpuSettingsResolver.FullGpuLayersArgument)
            : Localizer.Format(LocalizedStrings.SettingsBalancedGpuDescription, LlamaGpuSettingsResolver.BalancedGpuLayersArgument);
    }

    private void RefreshStockfishDescription()
    {
        StockfishSettings settings = SelectedStockfishSettings;
        StockfishDescriptionTextBlock.Text = Localizer.Format(
            LocalizedStrings.SettingsStockfishDescription,
            settings.Threads,
            settings.HashMb,
            settings.BulkAnalysisDepth,
            settings.BulkAnalysisMultiPv,
            settings.BulkAnalysisMoveTimeMs);
    }

    private void ApplyLocalizedText()
    {
        Title = Localizer.Text(LocalizedStrings.SettingsTitle);
        TitleTextBlock.Text = Localizer.Text(LocalizedStrings.SettingsTitle);
        CloseButton.Content = Localizer.Text(LocalizedStrings.SettingsClose);
        IntroTextBlock.Text = Localizer.Text(LocalizedStrings.SettingsIntro);
        SetupChecklistTitleTextBlock.Text = Localizer.Text(LocalizedStrings.SettingsSetupChecklistTitle);
        SetupChecklistBodyTextBlock.Text = Localizer.Text(LocalizedStrings.SettingsSetupChecklistBody);
        LanguageLabelTextBlock.Text = Localizer.Text(LocalizedStrings.SettingsLanguage);
        ModelTitleTextBlock.Text = Localizer.Text(LocalizedStrings.SettingsModel);
        FullGpuPowerCheckBox.Content = Localizer.Text(LocalizedStrings.SettingsUseFullGpu);
        LlamaPathLabelTextBlock.Text = Localizer.Text(LocalizedStrings.SettingsLlamaPath);
        LlamaServerPathTextBox.PlaceholderText = Localizer.Text(LocalizedStrings.SettingsLlamaPlaceholder);
        BrowseLlamaButton.Content = Localizer.Text(LocalizedStrings.SettingsBrowse);
        ExplanationLevelLabelTextBlock.Text = Localizer.Text(LocalizedStrings.SettingsExplanationLevel);
        NarrationStyleLabelTextBlock.Text = Localizer.Text(LocalizedStrings.SettingsNarrationStyle);
        StockfishTitleTextBlock.Text = Localizer.Text(LocalizedStrings.SettingsStockfish);
        StockfishPathLabelTextBlock.Text = Localizer.Text(LocalizedStrings.SettingsStockfishPath);
        StockfishPathTextBox.PlaceholderText = Localizer.Text(LocalizedStrings.SettingsStockfishPlaceholder);
        BrowseStockfishButton.Content = Localizer.Text(LocalizedStrings.SettingsBrowse);
        EngineThreadsLabelTextBlock.Text = Localizer.Text(LocalizedStrings.SettingsEngineThreads);
        HashMemoryLabelTextBlock.Text = Localizer.Text(LocalizedStrings.SettingsHashMemory);
        BulkDepthLabelTextBlock.Text = Localizer.Text(LocalizedStrings.SettingsBulkDepth);
        BulkMultiPvLabelTextBlock.Text = Localizer.Text(LocalizedStrings.SettingsBulkMultiPv);
        BulkMoveTimeLabelTextBlock.Text = Localizer.Text(LocalizedStrings.SettingsBulkMoveTime);
        FooterTextBlock.Text = Localizer.Text(LocalizedStrings.SettingsFooter);
        CancelButton.Content = Localizer.Text(LocalizedStrings.SettingsCancel);
        SaveButton.Content = Localizer.Text(LocalizedStrings.SettingsSave);
    }

    private static int ReadInt(NumericUpDown numeric, int fallback)
    {
        return numeric.Value.HasValue
            ? Convert.ToInt32(numeric.Value.Value)
            : fallback;
    }

    private async Task<string?> PickExecutablePathAsync(string title)
    {
        IReadOnlyList<IStorageFile> files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("Executable files")
                {
                    Patterns = ["*.exe"]
                },
                FilePickerFileTypes.All
            ]
        });

        return files.Count == 0 ? null : files[0].Path.LocalPath;
    }

    private sealed record ExplanationLevelOption(ExplanationLevel Level, string Label)
    {
        public override string ToString() => Label;
    }

    private sealed record NarrationStyleOption(AdviceNarrationStyle Style, string Label)
    {
        public override string ToString() => Label;
    }
}
