namespace MoveMentorChess.Presentation.Models;

public sealed record AnalysisFilterResult(
    IReadOnlyList<SelectedMistakeViewItem> Items,
    string SummaryText);
