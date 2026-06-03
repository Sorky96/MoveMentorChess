namespace MoveMentorChess.Training;

public static class TrainingPlanOpeningWeaknessSelector
{
    public static bool HasActionableOpeningWeakness(OpeningWeaknessReport? openingReport)
    {
        return openingReport is not null
            && openingReport.WeakOpenings.Any(opening =>
                opening.Category is OpeningWeaknessCategory.FixNow or OpeningWeaknessCategory.ReviewLater);
    }

    public static bool HasFixNowOpening(OpeningWeaknessReport? openingReport)
    {
        return openingReport is not null
            && openingReport.WeakOpenings.Any(opening => opening.Category == OpeningWeaknessCategory.FixNow);
    }

    public static IEnumerable<OpeningWeaknessEntry> GetActionableOpenings(OpeningWeaknessReport? openingReport)
    {
        if (openingReport is null)
        {
            return [];
        }

        return openingReport.WeakOpenings
            .Where(opening => opening.Category is OpeningWeaknessCategory.FixNow or OpeningWeaknessCategory.ReviewLater)
            .OrderBy(opening => opening.Category == OpeningWeaknessCategory.FixNow ? 0 : 1)
            .ThenByDescending(opening => opening.AverageOpeningCentipawnLoss ?? 0)
            .ThenByDescending(opening => opening.Count);
    }
}
