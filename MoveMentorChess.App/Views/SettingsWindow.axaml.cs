using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Platform.Storage;
using MoveMentorChess.Analysis;
using MoveMentorChess.App.Composition;
using MoveMentorChess.Localization;

namespace MoveMentorChess.App.Views;

public partial class SettingsWindow : Window
{
    private readonly ISettingsWorkflow settingsWorkflow;

    public SettingsWindow()
        : this(new DefaultSettingsWorkflow())
    {
    }

    internal SettingsWindow(ISettingsWorkflow settingsWorkflow)
        : this(
            settingsWorkflow ?? throw new ArgumentNullException(nameof(settingsWorkflow)),
            settingsWorkflow.Load())
    {
    }

    public SettingsWindow(
        LlamaGpuSettings settings,
        StockfishSettings stockfishSettings,
        ApplicationSettings applicationSettings)
        : this(new DefaultSettingsWorkflow(), new RuntimeSettingsSnapshot(settings, stockfishSettings, applicationSettings))
    {
    }

    private SettingsWindow(
        ISettingsWorkflow settingsWorkflow,
        RuntimeSettingsSnapshot snapshot)
    {
        this.settingsWorkflow = settingsWorkflow ?? throw new ArgumentNullException(nameof(settingsWorkflow));
        InitializeComponent();
        Localizer.UseApplicationCulture(snapshot.ApplicationSettings.CultureName);
        ApplyLocalizedText();
        LanguageComboBox.ItemsSource = LanguageCatalog.SupportedLanguages;
        LanguageComboBox.SelectedItem = LanguageCatalog.Resolve(snapshot.ApplicationSettings.CultureName);
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

        FullGpuPowerCheckBox.IsChecked = snapshot.LlamaGpuSettings.UseFullGpuPower;
        LlamaServerPathTextBox.Text = snapshot.LlamaGpuSettings.ServerPath;
        StockfishPathTextBox.Text = snapshot.StockfishSettings.ExecutablePath;
        StockfishThreadsNumeric.Value = snapshot.StockfishSettings.Threads;
        StockfishHashNumeric.Value = snapshot.StockfishSettings.HashMb;
        BulkDepthNumeric.Value = snapshot.StockfishSettings.BulkAnalysisDepth;
        BulkMultiPvNumeric.Value = snapshot.StockfishSettings.BulkAnalysisMultiPv;
        BulkMoveTimeNumeric.Value = snapshot.StockfishSettings.BulkAnalysisMoveTimeMs;
        ExplanationLevelComboBox.SelectedItem = ExplanationLevelComboBox.Items
            .OfType<ExplanationLevelOption>()
            .FirstOrDefault(option => option.Level == snapshot.LlamaGpuSettings.DefaultExplanationLevel);
        NarrationStyleComboBox.SelectedItem = NarrationStyleComboBox.Items
            .OfType<NarrationStyleOption>()
            .FirstOrDefault(option => option.Style == snapshot.LlamaGpuSettings.NarrationStyle);
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

    private async void SaveButton_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            settingsWorkflow.Save(new RuntimeSettingsSnapshot(
                SelectedSettings,
                SelectedStockfishSettings,
                SelectedApplicationSettings));
        }
        catch (ApplicationSettingsSaveException)
        {
            await ShowSaveFailedDialogAsync();
            return;
        }

        Close(true);
    }

    private void CancelButton_Click(object? sender, RoutedEventArgs e)
    {
        Close(false);
    }

    private async Task ShowSaveFailedDialogAsync()
    {
        Window dialog = new()
        {
            Title = Localizer.Text(LocalizedStrings.SettingsSaveFailedTitle),
            Width = 460,
            Height = 210,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false
        };
        Button okButton = new()
        {
            Content = Localizer.Text(LocalizedStrings.MainDialogOk),
            MinWidth = 96,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        okButton.Click += (_, _) => dialog.Close();
        dialog.Content = new StackPanel
        {
            Margin = new Thickness(18),
            Spacing = 16,
            Children =
            {
                new TextBlock
                {
                    Text = Localizer.Text(LocalizedStrings.SettingsSaveFailedMessage),
                    TextWrapping = Avalonia.Media.TextWrapping.Wrap
                },
                okButton
            }
        };

        await dialog.ShowDialog(this);
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
