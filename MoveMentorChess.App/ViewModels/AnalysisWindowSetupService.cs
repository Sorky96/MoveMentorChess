using MoveMentorChess.Localization;
using MoveMentorChess.Presentation.Models;

namespace MoveMentorChess.App.ViewModels;

internal sealed record AnalysisSideOption(PlayerSide Side, string Label)
{
    public override string ToString() => Label;
}

internal static class AnalysisWindowSetupService
{
    public static IReadOnlyList<AnalysisSideOption> CreateSideOptions() =>
    [
        new(PlayerSide.White, Localizer.Text(LocalizedStrings.AnalysisWindowAnalyzeWhite)),
        new(PlayerSide.Black, Localizer.Text(LocalizedStrings.AnalysisWindowAnalyzeBlack))
    ];

    public static IReadOnlyList<AnalysisFilterOption> CreateFilterOptions() =>
    [
        new(Localizer.Text(LocalizedStrings.AnalysisWindowFilterAllHighlights), null),
        new(Localizer.Text(LocalizedStrings.AnalysisWindowFilterNotReviewed), null, AnalysisReviewFilter.NotReviewed),
        new(Localizer.Text(LocalizedStrings.AnalysisWindowFilterReviewed), null, AnalysisReviewFilter.Reviewed),
        new(Localizer.Text(LocalizedStrings.AnalysisWindowFilterBlundersOnly), MoveQualityBucket.Blunder),
        new(Localizer.Text(LocalizedStrings.AnalysisWindowFilterMistakesOnly), MoveQualityBucket.Mistake),
        new(Localizer.Text(LocalizedStrings.AnalysisWindowFilterInaccuraciesOnly), MoveQualityBucket.Inaccuracy)
    ];

    public static int GetSideIndex(PlayerSide side)
        => side == PlayerSide.Black ? 1 : 0;

    public static AnalysisSideOption GetSideOption(IReadOnlyList<AnalysisSideOption> sideOptions, PlayerSide side)
        => sideOptions[GetSideIndex(side)];

    public static AnalysisWindowState CreateWindowState(PlayerSide side, int qualityFilterIndex)
        => new(side, qualityFilterIndex, 1);

    public static int ClampFilterIndex(int index, int itemCount)
        => Math.Clamp(index, 0, Math.Max(0, itemCount - 1));

    public static AnalysisFilterOption GetFilterOption(IReadOnlyList<AnalysisFilterOption> filterOptions, int index)
        => filterOptions[ClampFilterIndex(index, filterOptions.Count)];

    public static int GetFilterIndex(IReadOnlyList<AnalysisFilterOption> filterOptions, AnalysisFilterOption? option)
    {
        if (option is null)
        {
            return 0;
        }

        for (int i = 0; i < filterOptions.Count; i++)
        {
            if (filterOptions[i].QualityFilter == option.QualityFilter
                && filterOptions[i].ReviewFilter == option.ReviewFilter)
            {
                return i;
            }
        }

        return 0;
    }
}
