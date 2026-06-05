using System.Globalization;

namespace MoveMentorChess.App.ViewModels;

public sealed class PlayerProfileSummaryItemViewModel
{
    public PlayerProfileSummaryItemViewModel(PlayerProfileSummary summary)
    {
        Summary = summary;
        Header = summary.DisplayName;
        string topLabels = summary.TopLabels.Count == 0 ? "no tags" : string.Join(", ", summary.TopLabels);
        Meta = $"Games {summary.GamesAnalyzed.ToString(CultureInfo.InvariantCulture)} | Mistakes to practice {summary.HighlightedMistakes.ToString(CultureInfo.InvariantCulture)} | Avg loss {summary.AverageCentipawnLoss?.ToString(CultureInfo.InvariantCulture) ?? "n/a"} | {topLabels}";
    }

    public PlayerProfileSummary Summary { get; }

    public string Header { get; }

    public string Meta { get; }
}
