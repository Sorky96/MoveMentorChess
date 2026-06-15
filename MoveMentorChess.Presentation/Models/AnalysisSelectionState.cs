using MoveMentorChess.Localization;

namespace MoveMentorChess.Presentation.Models;

public enum AnalysisReviewFilter
{
    All,
    NotReviewed,
    Reviewed
}

public sealed record AnalysisFilterOption(
    string Label,
    MoveQualityBucket? QualityFilter,
    AnalysisReviewFilter ReviewFilter = AnalysisReviewFilter.All)
{
    public override string ToString() => Label;
}

public sealed record AnalysisFilterResult(
    IReadOnlyList<SelectedMistakeViewItem> Items,
    string SummaryText);

public sealed class AnalysisSelectionState
{
    private readonly HashSet<int> reviewedPlies = [];

    public GameAnalysisResult? CurrentResult { get; private set; }

    public bool CurrentResultIsCached { get; private set; }

    public IReadOnlySet<int> ReviewedPlies => reviewedPlies;

    public void SetCurrentResult(GameAnalysisResult? result, bool isCached)
    {
        CurrentResult = result;
        CurrentResultIsCached = result is not null && isCached;
    }

    public void ClearCurrentResult()
    {
        CurrentResult = null;
        CurrentResultIsCached = false;
    }

    public void MarkReviewed(MoveAnalysisResult lead)
    {
        reviewedPlies.Add(lead.Replay.Ply);
    }

    public bool IsReviewed(MoveAnalysisResult lead)
        => reviewedPlies.Contains(lead.Replay.Ply);

    public AnalysisFilterResult BuildFilterResult(AnalysisFilterOption? filter)
    {
        if (CurrentResult is null)
        {
            return new AnalysisFilterResult([], Localizer.Text(LocalizedStrings.AnalysisWindowChooseSideRunAnalysis));
        }

        IEnumerable<SelectedMistake> visibleMistakes = CurrentResult.HighlightedMistakes;
        if (filter?.QualityFilter is MoveQualityBucket quality)
        {
            visibleMistakes = visibleMistakes.Where(mistake => mistake.Quality == quality);
        }

        visibleMistakes = filter?.ReviewFilter switch
        {
            AnalysisReviewFilter.NotReviewed => visibleMistakes.Where(mistake => !IsReviewed(AnalysisMistakePresentation.GetLeadMove(mistake))),
            AnalysisReviewFilter.Reviewed => visibleMistakes.Where(mistake => IsReviewed(AnalysisMistakePresentation.GetLeadMove(mistake))),
            _ => visibleMistakes
        };

        List<SelectedMistakeViewItem> items = visibleMistakes
            .Select(mistake => new SelectedMistakeViewItem(
                mistake,
                CurrentResult,
                IsReviewed(AnalysisMistakePresentation.GetLeadMove(mistake))))
            .ToList();

        return new AnalysisFilterResult(items, BuildSummaryText(items));
    }

    public int CountReviewedHighlights()
        => CurrentResult is null
            ? 0
            : AnalysisTimelinePresentation.CountReviewedHighlights(CurrentResult, reviewedPlies);

    private string BuildSummaryText(List<SelectedMistakeViewItem> items)
    {
        if (CurrentResult is null)
        {
            return Localizer.Text(LocalizedStrings.AnalysisWindowChooseSideRunAnalysis);
        }

        int blunders = CurrentResult.HighlightedMistakes.Count(item => item.Quality == MoveQualityBucket.Blunder);
        int mistakes = CurrentResult.HighlightedMistakes.Count(item => item.Quality == MoveQualityBucket.Mistake);
        int inaccuracies = CurrentResult.HighlightedMistakes.Count(item => item.Quality == MoveQualityBucket.Inaccuracy);
        string cacheSuffix = CurrentResultIsCached
            ? $" {Localizer.Text(LocalizedStrings.AnalysisWindowLoadedFromCacheSentence)}"
            : string.Empty;
        string diagnosis = AnalysisTimelinePresentation.BuildSummaryDiagnosis(CurrentResult);
        return Localizer.Format(
            LocalizedStrings.AnalysisWindowFilterSummary,
            items.Count,
            FormatSide(CurrentResult.AnalyzedSide),
            blunders,
            mistakes,
            inaccuracies,
            CountReviewedHighlights(),
            CurrentResult.HighlightedMistakes.Count,
            diagnosis,
            cacheSuffix);
    }

    private static string FormatSide(PlayerSide side)
        => side == PlayerSide.White
            ? Localizer.Text(LocalizedStrings.CommonWhite)
            : Localizer.Text(LocalizedStrings.CommonBlack);
}
