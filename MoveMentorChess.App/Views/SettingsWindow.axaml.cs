using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using MoveMentorChess.Analysis;

namespace MoveMentorChess.App.Views;

public partial class SettingsWindow : Window
{
    public SettingsWindow()
        : this(LlamaGpuSettingsStore.Load(), StockfishSettingsStore.Load())
    {
    }

    public SettingsWindow(LlamaGpuSettings settings, StockfishSettings stockfishSettings)
    {
        InitializeComponent();
        ExplanationLevelComboBox.ItemsSource = new[]
        {
            new ExplanationLevelOption(ExplanationLevel.Beginner, "Beginner"),
            new ExplanationLevelOption(ExplanationLevel.Intermediate, "Intermediate"),
            new ExplanationLevelOption(ExplanationLevel.Advanced, "Advanced")
        };
        NarrationStyleComboBox.ItemsSource = new[]
        {
            new NarrationStyleOption(AdviceNarrationStyle.RegularTrainer, "Regular Trainer"),
            new NarrationStyleOption(AdviceNarrationStyle.LevyRozman, "Levy Rozman"),
            new NarrationStyleOption(AdviceNarrationStyle.HikaruNakamura, "Hikaru Nakamura"),
            new NarrationStyleOption(AdviceNarrationStyle.BotezLive, "BotezLive"),
            new NarrationStyleOption(AdviceNarrationStyle.WittyAlien, "Witty Alien")
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
            NormalizePath(LlamaServerPathTextBox.Text));

    public StockfishSettings SelectedStockfishSettings =>
        new(
            ReadInt(StockfishThreadsNumeric, StockfishSettings.Default.Threads),
            ReadInt(StockfishHashNumeric, StockfishSettings.Default.HashMb),
            ReadInt(BulkDepthNumeric, StockfishSettings.Default.BulkAnalysisDepth),
            ReadInt(BulkMultiPvNumeric, StockfishSettings.Default.BulkAnalysisMultiPv),
            ReadInt(BulkMoveTimeNumeric, StockfishSettings.Default.BulkAnalysisMoveTimeMs),
            NormalizePath(StockfishPathTextBox.Text));

    private void SaveButton_Click(object? sender, RoutedEventArgs e)
    {
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
        string? path = await PickExecutablePathAsync("Select stockfish.exe");
        if (!string.IsNullOrWhiteSpace(path))
        {
            StockfishPathTextBox.Text = path;
        }
    }

    private async void BrowseLlamaServerButton_Click(object? sender, RoutedEventArgs e)
    {
        string? path = await PickExecutablePathAsync("Select llama-server.exe");
        if (!string.IsNullOrWhiteSpace(path))
        {
            LlamaServerPathTextBox.Text = path;
        }
    }

    private void RefreshModeDescription()
    {
        bool useFullGpuPower = FullGpuPowerCheckBox.IsChecked == true;
        ModeDescriptionTextBlock.Text = useFullGpuPower
            ? $"Full GPU mode is enabled. llama.cpp will request '-ngl {LlamaGpuSettingsResolver.FullGpuLayersArgument}', which means pushing all possible model layers onto the GPU."
            : $"Balanced mode is enabled. llama.cpp will request '-ngl {LlamaGpuSettingsResolver.BalancedGpuLayersArgument}', which is safer on smaller cards and remains the default.";
    }

    private void RefreshStockfishDescription()
    {
        StockfishSettings settings = SelectedStockfishSettings;
        StockfishDescriptionTextBlock.Text =
            $"Stockfish will use {settings.Threads} thread(s), {settings.HashMb} MB hash, and bulk PGN analysis will run at depth {settings.BulkAnalysisDepth}, MultiPV {settings.BulkAnalysisMultiPv}, {settings.BulkAnalysisMoveTimeMs} ms per position.";
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

    private static string? NormalizePath(string? path)
        => string.IsNullOrWhiteSpace(path) ? null : path.Trim();

    private sealed record ExplanationLevelOption(ExplanationLevel Level, string Label)
    {
        public override string ToString() => Label;
    }

    private sealed record NarrationStyleOption(AdviceNarrationStyle Style, string Label)
    {
        public override string ToString() => Label;
    }
}
