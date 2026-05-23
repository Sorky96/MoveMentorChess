namespace MoveMentorChess.Profiles;

internal static class PlayerProfileProgressAnalyzer
{
    private const int MinimumGamesPerProgressWindow = 2;
    private static readonly int[] ProgressWindowDays = [14, 30];

    public static ProfileProgressSignal BuildProgressSignal(IReadOnlyList<PlayerProfileSnapshot> snapshots)
    {
        if (!TrySelectProgressWindows(snapshots, out ProgressWindowSelection? selection) || selection is null)
        {
            return new ProfileProgressSignal(
                ProfileProgressDirection.InsufficientData,
                "Not enough dated games yet to compare recent form across recent time windows.",
                null,
                null);
        }

        ProfileProgressPeriod previousPeriod = BuildProgressPeriod(selection.Previous, $"Previous {selection.WindowDays} days");
        ProfileProgressPeriod recentPeriod = BuildProgressPeriod(selection.Recent, $"Last {selection.WindowDays} days");

        if (previousPeriod.AverageCentipawnLoss is null || recentPeriod.AverageCentipawnLoss is null)
        {
            return new ProfileProgressSignal(
                ProfileProgressDirection.InsufficientData,
                "Not enough reliable data to measure progress yet.",
                recentPeriod,
                previousPeriod);
        }

        int cplDelta = recentPeriod.AverageCentipawnLoss.Value - previousPeriod.AverageCentipawnLoss.Value;
        double highlightDelta = recentPeriod.HighlightedMistakesPerGame - previousPeriod.HighlightedMistakesPerGame;

        ProfileProgressDirection direction;
        string summary;
        if (cplDelta <= -35 || (cplDelta <= -25 && highlightDelta <= -0.15))
        {
            direction = ProfileProgressDirection.Improving;
            summary = cplDelta <= -35
                ? $"Recent games are cleaner: average CPL improved by {Math.Abs(cplDelta)}."
                : $"Recent games are cleaner: average CPL improved by {Math.Abs(cplDelta)} and highlighted mistakes per game also dropped.";
        }
        else if (cplDelta >= 35 || (cplDelta >= 25 && highlightDelta >= 0.15))
        {
            direction = ProfileProgressDirection.Regressing;
            summary = cplDelta >= 35
                ? $"Recent games are rougher: average CPL rose by {cplDelta}."
                : $"Recent games are rougher: average CPL rose by {cplDelta} and the number of highlighted mistakes per game increased.";
        }
        else
        {
            direction = ProfileProgressDirection.Stable;
            summary = "Recent results are broadly stable versus the earlier sample, with no strong improvement or regression signal yet.";
        }

        return new ProfileProgressSignal(direction, summary, recentPeriod, previousPeriod);
    }

    public static List<ProfileLabelTrend> BuildLabelTrends(IReadOnlyList<PlayerProfileSnapshot> snapshots)
    {
        if (!TrySelectProgressWindows(snapshots, out ProgressWindowSelection? selection) || selection is null)
        {
            return [];
        }

        return selection.Previous
            .Concat(selection.Recent)
            .SelectMany(snapshot => snapshot.Moves)
            .Where(move => move.Move.Quality.IsProblem() && !string.IsNullOrWhiteSpace(move.Advice.MistakeLabel))
            .Select(move => move.Advice.MistakeLabel!)
            .Distinct(StringComparer.Ordinal)
            .Select(label => BuildLabelTrend(label, selection.Previous, selection.Recent))
            .OrderBy(item => item.Label, StringComparer.Ordinal)
            .ToList();
    }

    public static DateTime? GetSnapshotDate(PlayerProfileSnapshot snapshot)
        => snapshot.GameDate?.Date ?? snapshot.AnalysisUpdatedUtc.Date;

    private static bool TrySelectProgressWindows(
        IReadOnlyList<PlayerProfileSnapshot> snapshots,
        out ProgressWindowSelection? selection)
    {
        selection = null;
        List<PlayerProfileSnapshot> dated = snapshots
            .Where(snapshot => GetSnapshotDate(snapshot).HasValue)
            .OrderBy(snapshot => GetSnapshotDate(snapshot))
            .ThenBy(snapshot => snapshot.GameFingerprint, StringComparer.Ordinal)
            .ToList();
        if (dated.Count < MinimumGamesPerProgressWindow * 2)
        {
            return false;
        }

        DateTime anchorDate = dated
            .Select(snapshot => GetSnapshotDate(snapshot)!.Value.Date)
            .Max();

        foreach (int windowDays in ProgressWindowDays)
        {
            DateTime recentStart = anchorDate.AddDays(-(windowDays - 1));
            DateTime previousStart = recentStart.AddDays(-windowDays);
            DateTime previousEnd = recentStart.AddDays(-1);

            List<PlayerProfileSnapshot> recent = dated
                .Where(snapshot =>
                {
                    DateTime date = GetSnapshotDate(snapshot)!.Value.Date;
                    return date >= recentStart && date <= anchorDate;
                })
                .ToList();
            List<PlayerProfileSnapshot> previous = dated
                .Where(snapshot =>
                {
                    DateTime date = GetSnapshotDate(snapshot)!.Value.Date;
                    return date >= previousStart && date <= previousEnd;
                })
                .ToList();

            if (recent.Count >= MinimumGamesPerProgressWindow && previous.Count >= MinimumGamesPerProgressWindow)
            {
                selection = new ProgressWindowSelection(windowDays, previous, recent);
                return true;
            }
        }

        return false;
    }

    private static ProfileLabelTrend BuildLabelTrend(
        string label,
        IReadOnlyList<PlayerProfileSnapshot> previousSnapshots,
        IReadOnlyList<PlayerProfileSnapshot> recentSnapshots)
    {
        List<StoredMoveAnalysis> previousMoves = GetMovesForLabel(previousSnapshots, label);
        List<StoredMoveAnalysis> recentMoves = GetMovesForLabel(recentSnapshots, label);

        int previousCount = previousMoves.Count;
        int recentCount = recentMoves.Count;
        int totalCount = previousCount + recentCount;
        int? previousAverageCpl = PlayerProfileStatsAggregator.TryAverage(previousMoves.Select(move => move.CentipawnLoss));
        int? recentAverageCpl = PlayerProfileStatsAggregator.TryAverage(recentMoves.Select(move => move.CentipawnLoss));

        ProfileProgressDirection direction;
        if (totalCount < 2)
        {
            direction = ProfileProgressDirection.InsufficientData;
        }
        else if (previousCount == 0 && recentCount >= 2)
        {
            direction = ProfileProgressDirection.Regressing;
        }
        else if (recentCount == 0 && previousCount >= 2)
        {
            direction = ProfileProgressDirection.Improving;
        }
        else
        {
            int countDelta = recentCount - previousCount;
            int cplDelta = (recentAverageCpl ?? 0) - (previousAverageCpl ?? 0);

            bool worseningByFrequency = countDelta >= 1;
            bool improvingByFrequency = countDelta <= -1;
            bool worseningByCost = recentAverageCpl.HasValue && previousAverageCpl.HasValue && cplDelta >= 25;
            bool improvingByCost = recentAverageCpl.HasValue && previousAverageCpl.HasValue && cplDelta <= -25;

            if (worseningByFrequency || (countDelta >= 0 && worseningByCost))
            {
                direction = ProfileProgressDirection.Regressing;
            }
            else if (improvingByFrequency || (countDelta <= 0 && improvingByCost))
            {
                direction = ProfileProgressDirection.Improving;
            }
            else
            {
                direction = ProfileProgressDirection.Stable;
            }
        }

        return new ProfileLabelTrend(
            label,
            direction,
            recentCount,
            previousCount,
            recentAverageCpl,
            previousAverageCpl);
    }

    private static List<StoredMoveAnalysis> GetMovesForLabel(IReadOnlyList<PlayerProfileSnapshot> snapshots, string label)
    {
        return snapshots
            .SelectMany(snapshot => snapshot.Moves)
            .Where(move =>
                move.Move.Quality.IsProblem()
                && string.Equals(move.Advice.MistakeLabel, label, StringComparison.Ordinal))
            .ToList();
    }

    private static ProfileProgressPeriod BuildProgressPeriod(IReadOnlyList<PlayerProfileSnapshot> snapshots, string label)
    {
        int highlightedMistakes = snapshots.Sum(snapshot => PlayerProfileStatsAggregator.GetHighlightedGroups(snapshot).Count);
        double highlightsPerGame = snapshots.Count == 0
            ? 0.0
            : Math.Round((double)highlightedMistakes / snapshots.Count, 2);

        return new ProfileProgressPeriod(
            label,
            snapshots.Count,
            PlayerProfileStatsAggregator.TryAverage(snapshots.SelectMany(snapshot => snapshot.Moves).Select(move => move.CentipawnLoss)),
            highlightsPerGame);
    }

    private sealed record ProgressWindowSelection(
        int WindowDays,
        IReadOnlyList<PlayerProfileSnapshot> Previous,
        IReadOnlyList<PlayerProfileSnapshot> Recent);
}
