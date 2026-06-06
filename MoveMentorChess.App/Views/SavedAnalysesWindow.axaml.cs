using System.Globalization;
using System.Text;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using MoveMentorChess.App.ViewModels;
using MoveMentorChess.Localization;
using MoveMentorChess.Persistence;
using MoveMentorChess.Presentation.Models;

namespace MoveMentorChess.App.Views;

public partial class SavedAnalysesWindow : Window
{
    private readonly ISavedLibraryDataService dataService;
    private readonly bool canOpenAnalysis;

    public SavedAnalysesWindow()
        : this(new DefaultSavedLibraryDataService(() => null), canOpenAnalysis: true)
    {
    }

    internal SavedAnalysesWindow(ISavedLibraryDataService dataService, bool canOpenAnalysis)
    {
        this.dataService = dataService ?? throw new ArgumentNullException(nameof(dataService));
        this.canOpenAnalysis = canOpenAnalysis;
        InitializeComponent();
        RefreshList();
    }

    public SavedAnalysesWindow(IAnalysisStore analysisStore, bool canOpenAnalysis)
        : this(new StoreBackedSavedLibraryDataService(analysisStore), canOpenAnalysis)
    {
    }

    public GameAnalysisResult? SelectedResult { get; private set; }

    public SavedAnalysisAction RequestedAction { get; private set; }

    private void FilterTextBox_OnTextChanged(object? sender, TextChangedEventArgs e)
    {
        RefreshList();
    }

    private void AnalysesListBox_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        UpdateDetails();
    }

    private void AnalysesListBox_OnDoubleTapped(object? sender, TappedEventArgs e)
    {
        ConfirmSelection(canOpenAnalysis ? SavedAnalysisAction.OpenAnalysis : SavedAnalysisAction.LoadGame);
    }

    private void LoadGameButton_Click(object? sender, RoutedEventArgs e)
    {
        ConfirmSelection(SavedAnalysisAction.LoadGame);
    }

    private void OpenAnalysisButton_Click(object? sender, RoutedEventArgs e)
    {
        ConfirmSelection(SavedAnalysisAction.OpenAnalysis);
    }

    private void DeleteSelectedButton_Click(object? sender, RoutedEventArgs e)
    {
        if (AnalysesListBox.SelectedItem is not SavedAnalysisListItem item)
        {
            return;
        }

        string gameFingerprint = GameFingerprint.Compute(item.Result.Game.PgnText);
        if (!dataService.DeleteGameAndCachedAnalysis(gameFingerprint))
        {
            return;
        }

        RefreshList();
    }

    private void CloseButton_Click(object? sender, RoutedEventArgs e)
    {
        Close(false);
    }

    private void RefreshList()
    {
        SelectedResult = null;
        RequestedAction = SavedAnalysisAction.None;
        LoadGameButton.IsEnabled = false;
        OpenAnalysisButton.IsEnabled = false;
        DeleteSelectedButton.IsEnabled = false;

        IReadOnlyList<GameAnalysisResult> items = dataService.ListResults(FilterTextBox.Text, limit: 1000);
        string normalizedFilter = FilterTextBox.Text?.Trim() ?? string.Empty;
        IEnumerable<GameAnalysisResult> filtered = string.IsNullOrWhiteSpace(normalizedFilter)
            ? items
            : items.Where(result => MatchesExtendedFilter(result, normalizedFilter));

        AnalysesListBox.ItemsSource = filtered
            .Select(result => new SavedAnalysisListItem(
                result,
                $"{result.Game.WhitePlayer ?? Localizer.Text(LocalizedStrings.CommonWhite)} vs {result.Game.BlackPlayer ?? Localizer.Text(LocalizedStrings.CommonBlack)}",
                result.AnalyzedSide == PlayerSide.White ? Localizer.Text(LocalizedStrings.CommonWhite) : Localizer.Text(LocalizedStrings.CommonBlack),
                string.IsNullOrWhiteSpace(result.Game.DateText) ? Localizer.Text(LocalizedStrings.CommonUnknown) : result.Game.DateText!,
                OpeningCatalog.GetName(result.Game.Eco),
                result.HighlightedMistakes.Count.ToString(CultureInfo.InvariantCulture)))
            .ToList();

        if (AnalysesListBox.ItemCount > 0)
        {
            AnalysesListBox.SelectedIndex = 0;
        }
        else
        {
            DetailsTextBlock.Text = Localizer.Text(LocalizedStrings.SavedAnalysesNoMatches);
        }
    }

    private void UpdateDetails()
    {
        if (AnalysesListBox.SelectedItem is not SavedAnalysisListItem item)
        {
            DetailsTextBlock.Text = Localizer.Text(LocalizedStrings.SavedAnalysesSelectPrompt);
            LoadGameButton.IsEnabled = false;
            OpenAnalysisButton.IsEnabled = false;
            DeleteSelectedButton.IsEnabled = false;
            return;
        }

        SelectedResult = item.Result;
        LoadGameButton.IsEnabled = true;
        OpenAnalysisButton.IsEnabled = canOpenAnalysis;
        DeleteSelectedButton.IsEnabled = true;

        GameAnalysisResult result = item.Result;
        int blunders = result.HighlightedMistakes.Count(mistake => mistake.Quality == MoveQualityBucket.Blunder);
        int mistakes = result.HighlightedMistakes.Count(mistake => mistake.Quality == MoveQualityBucket.Mistake);
        int inaccuracies = result.HighlightedMistakes.Count(mistake => mistake.Quality == MoveQualityBucket.Inaccuracy);
        List<string> topLabels = result.HighlightedMistakes
            .Select(mistake => mistake.Tag?.Label ?? "unclassified")
            .GroupBy(label => label, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .Take(5)
            .Select(group => $"{FormatMistakeLabel(group.Key)} ({group.Count()})")
            .ToList();

        StringBuilder builder = new();
        builder.AppendLine(CultureInfo.InvariantCulture, $"{result.Game.WhitePlayer ?? Localizer.Text(LocalizedStrings.CommonWhite)} vs {result.Game.BlackPlayer ?? Localizer.Text(LocalizedStrings.CommonBlack)}");
        builder.AppendLine(Localizer.Format(LocalizedStrings.SavedAnalysesSide, FormatSide(result.AnalyzedSide)));
        builder.AppendLine(Localizer.Format(LocalizedStrings.SavedAnalysesDate, result.Game.DateText ?? Localizer.Text(LocalizedStrings.CommonUnknown)));
        builder.AppendLine(Localizer.Format(LocalizedStrings.SavedAnalysesResult, result.Game.Result ?? Localizer.Text(LocalizedStrings.CommonUnknown)));
        builder.AppendLine(Localizer.Format(LocalizedStrings.SavedAnalysesOpening, OpeningCatalog.GetName(result.Game.Eco)));
        builder.AppendLine(Localizer.Format(LocalizedStrings.SavedAnalysesMoveLabels, BuildQualityBreakdown(result.MoveAnalyses)));
        builder.AppendLine(Localizer.Format(LocalizedStrings.SavedAnalysesHighlights, result.HighlightedMistakes.Count));
        builder.AppendLine(Localizer.Format(LocalizedStrings.SavedAnalysesBreakdown, blunders, mistakes, inaccuracies));

        if (topLabels.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine(Localizer.Text(LocalizedStrings.SavedAnalysesTopLabels));
            foreach (string label in topLabels)
            {
                builder.AppendLine(CultureInfo.InvariantCulture, $"- {label}");
            }
        }

        if (result.HighlightedMistakes.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine(Localizer.Text(LocalizedStrings.SavedAnalysesTopHighlightedMistakes));
            foreach (SelectedMistake mistake in result.HighlightedMistakes.Take(5))
            {
                MoveAnalysisResult? lead = mistake.Moves
                    .OrderByDescending(move => move.Quality)
                    .ThenByDescending(move => move.CentipawnLoss ?? 0)
                    .FirstOrDefault();

                if (lead is null)
                {
                    continue;
                }

                string moveLabel = $"{lead.Replay.MoveNumber}{(lead.Replay.Side == PlayerSide.White ? "." : "...")} {lead.Replay.San}";
                builder.AppendLine(CultureInfo.InvariantCulture, $"- {moveLabel} | {FormatQuality(mistake.Quality)} | {FormatMistakeLabel(mistake.Tag?.Label ?? "unclassified")} | {Localizer.Format(LocalizedStrings.SavedAnalysesCpl, lead.CentipawnLoss?.ToString(CultureInfo.InvariantCulture) ?? "n/a")}");
            }
        }

        builder.AppendLine();
        builder.AppendLine(canOpenAnalysis
            ? Localizer.Text(LocalizedStrings.SavedAnalysesOpenInstructions)
            : Localizer.Text(LocalizedStrings.SavedAnalysesLoadInstructions));
        DetailsTextBlock.Text = builder.ToString().TrimEnd();
    }

    private void ConfirmSelection(SavedAnalysisAction action)
    {
        if (AnalysesListBox.SelectedItem is not SavedAnalysisListItem item)
        {
            return;
        }

        if (action == SavedAnalysisAction.OpenAnalysis && !canOpenAnalysis)
        {
            return;
        }

        SelectedResult = item.Result;
        RequestedAction = action;
        Close(true);
    }

    private static bool MatchesExtendedFilter(GameAnalysisResult result, string filterText)
    {
        if (string.IsNullOrWhiteSpace(filterText))
        {
            return true;
        }

        return (result.Game.WhitePlayer?.Contains(filterText, StringComparison.OrdinalIgnoreCase) ?? false)
            || (result.Game.BlackPlayer?.Contains(filterText, StringComparison.OrdinalIgnoreCase) ?? false)
            || (result.Game.DateText?.Contains(filterText, StringComparison.OrdinalIgnoreCase) ?? false)
            || (result.Game.Result?.Contains(filterText, StringComparison.OrdinalIgnoreCase) ?? false)
            || (result.Game.Eco?.Contains(filterText, StringComparison.OrdinalIgnoreCase) ?? false)
            || OpeningCatalog.Describe(result.Game.Eco).Contains(filterText, StringComparison.OrdinalIgnoreCase)
            || (result.Game.Site?.Contains(filterText, StringComparison.OrdinalIgnoreCase) ?? false)
            || result.AnalyzedSide.ToString().Contains(filterText, StringComparison.OrdinalIgnoreCase)
            || result.HighlightedMistakes.Any(mistake =>
                (mistake.Tag?.Label?.Contains(filterText, StringComparison.OrdinalIgnoreCase) ?? false)
                || mistake.Quality.ToString().Contains(filterText, StringComparison.OrdinalIgnoreCase));
    }

    private static string FormatMistakeLabel(string label)
        => AnalysisMistakePresentation.FormatMistakeLabel(label);

    private static string BuildQualityBreakdown(IReadOnlyList<MoveAnalysisResult> moveAnalyses)
    {
        return string.Join(", ", new[]
            {
                MoveQualityBucket.Book,
                MoveQualityBucket.Brilliant,
                MoveQualityBucket.Great,
                MoveQualityBucket.Best,
                MoveQualityBucket.Excellent,
                MoveQualityBucket.Good,
                MoveQualityBucket.Inaccuracy,
                MoveQualityBucket.Mistake,
                MoveQualityBucket.Blunder
            }
            .Select(quality => $"{FormatQuality(quality)} {moveAnalyses.Count(move => move.Quality == quality)}"));
    }

    private static string FormatSide(PlayerSide side)
        => side == PlayerSide.White ? Localizer.Text(LocalizedStrings.CommonWhite) : Localizer.Text(LocalizedStrings.CommonBlack);

    private static string FormatQuality(MoveQualityBucket quality)
    {
        return quality switch
        {
            MoveQualityBucket.Book => Localizer.Text(LocalizedStrings.QualityBook),
            MoveQualityBucket.Brilliant => Localizer.Text(LocalizedStrings.QualityBrilliant),
            MoveQualityBucket.Great => Localizer.Text(LocalizedStrings.QualityGreat),
            MoveQualityBucket.Best => Localizer.Text(LocalizedStrings.QualityBest),
            MoveQualityBucket.Excellent => Localizer.Text(LocalizedStrings.QualityExcellent),
            MoveQualityBucket.Good => Localizer.Text(LocalizedStrings.AdviceQualityGood),
            MoveQualityBucket.Inaccuracy => Localizer.Text(LocalizedStrings.AdviceQualityInaccuracy),
            MoveQualityBucket.Mistake => Localizer.Text(LocalizedStrings.AdviceQualityMistake),
            MoveQualityBucket.Blunder => Localizer.Text(LocalizedStrings.AdviceQualityBlunder),
            _ => quality.ToString()
        };
    }

    private sealed record SavedAnalysisListItem(
        GameAnalysisResult Result,
        string Players,
        string Side,
        string Date,
        string Opening,
        string Highlights)
    {
        public override string ToString() => $"{Players} {Side} {Date} {Opening}";
    }
}

public enum SavedAnalysisAction
{
    None,
    LoadGame,
    OpenAnalysis
}
