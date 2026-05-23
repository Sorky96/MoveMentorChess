namespace MoveMentorChess.Profiles;

internal static class PlayerProfileStatsAggregator
{
    public static PlayerProfileSummary BuildSummary(IGrouping<string, PlayerProfileSnapshot> group)
    {
        string displayName = SelectDisplayName(group);

        IReadOnlyList<string> topLabels = group
            .SelectMany(GetHighlightedGroups)
            .GroupBy(item => item.Label)
            .OrderByDescending(item => item.Count())
            .ThenBy(item => item.Key, StringComparer.Ordinal)
            .Take(3)
            .Select(item => item.Key)
            .ToList();

        int? averageCpl = TryAverage(group.SelectMany(snapshot => snapshot.Moves).Select(move => move.CentipawnLoss));

        return new PlayerProfileSummary(
            group.Key,
            displayName,
            group.Count(),
            group.Sum(snapshot => GetHighlightedGroups(snapshot).Count),
            averageCpl,
            topLabels);
    }

    public static string SelectDisplayName(IEnumerable<PlayerProfileSnapshot> snapshots)
    {
        return snapshots
            .GroupBy(snapshot => snapshot.DisplayName, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .Select(group => group
                .GroupBy(snapshot => snapshot.DisplayName, StringComparer.Ordinal)
                .OrderByDescending(exactGroup => exactGroup.Count())
                .ThenBy(exactGroup => exactGroup.Key, StringComparer.Ordinal)
                .Select(exactGroup => exactGroup.Key)
                .First())
            .First();
    }

    public static PlayerProfileAggregateStats BuildReportStats(
        IReadOnlyList<PlayerProfileSnapshot> snapshots,
        IReadOnlyList<HighlightedGroup> highlightedGroups,
        IReadOnlyList<StoredMoveAnalysis> mistakeMoves)
    {
        IReadOnlyList<ProfileLabelStat> topLabels = highlightedGroups
            .GroupBy(item => item.Label)
            .Select(group => new ProfileLabelStat(
                group.Key,
                group.Count(),
                group.Average(item => item.AverageConfidence)))
            .OrderByDescending(item => item.Count)
            .ThenByDescending(item => item.AverageConfidence)
            .ThenBy(item => item.Label, StringComparer.Ordinal)
            .Take(3)
            .ToList();

        IReadOnlyList<ProfileCostlyLabelStat> costliestLabels = mistakeMoves
            .GroupBy(move => move.MistakeLabel!, StringComparer.Ordinal)
            .Select(group => new ProfileCostlyLabelStat(
                group.Key,
                group.Count(),
                group.Sum(move => Math.Max(0, move.CentipawnLoss ?? 0)),
                TryAverage(group.Select(move => move.CentipawnLoss))))
            .OrderByDescending(item => item.TotalCentipawnLoss)
            .ThenByDescending(item => item.AverageCentipawnLoss ?? 0)
            .ThenByDescending(item => item.Count)
            .ThenBy(item => item.Label, StringComparer.Ordinal)
            .Take(3)
            .ToList();

        IReadOnlyList<ProfilePhaseStat> mistakesByPhase = snapshots
            .SelectMany(snapshot => snapshot.Moves)
            .Where(move => move.Quality.IsProblem())
            .GroupBy(move => move.Phase)
            .Select(group => new ProfilePhaseStat(group.Key, group.Count()))
            .OrderByDescending(item => item.Count)
            .ThenBy(item => item.Phase)
            .ToList();

        IReadOnlyList<ProfileOpeningStat> mistakesByOpening = snapshots
            .SelectMany(snapshot => snapshot.Moves
                .Where(move => move.Quality.IsProblem())
                .Select(_ => snapshot.Eco))
            .GroupBy(eco => eco, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                string representativeEco = group
                    .GroupBy(eco => eco, StringComparer.Ordinal)
                    .OrderByDescending(exactGroup => exactGroup.Count())
                    .ThenBy(exactGroup => exactGroup.Key, StringComparer.Ordinal)
                    .Select(exactGroup => exactGroup.Key)
                    .First();

                return new ProfileOpeningStat(representativeEco, group.Count());
            })
            .OrderByDescending(item => item.Count)
            .ThenBy(item => item.Eco, StringComparer.OrdinalIgnoreCase)
            .Take(5)
            .ToList();

        IReadOnlyList<ProfileSideStat> gamesBySide = snapshots
            .GroupBy(snapshot => snapshot.Side)
            .Select(group => new ProfileSideStat(
                group.Key,
                group.Count(),
                group.Sum(snapshot => GetHighlightedGroups(snapshot).Count)))
            .OrderBy(item => item.Side)
            .ToList();

        IReadOnlyList<ProfileMonthlyTrend> monthlyTrend = snapshots
            .GroupBy(snapshot => snapshot.MonthKey ?? "Unknown")
            .Select(group => new ProfileMonthlyTrend(
                group.Key,
                group.Count(),
                group.Sum(snapshot => GetHighlightedGroups(snapshot).Count),
                TryAverage(group.SelectMany(snapshot => snapshot.Moves).Select(move => move.CentipawnLoss))))
            .OrderBy(item => item.MonthKey, StringComparer.Ordinal)
            .ToList();

        IReadOnlyList<ProfileQuarterlyTrend> quarterlyTrend = snapshots
            .GroupBy(snapshot => snapshot.QuarterKey ?? "Unknown")
            .Select(group => new ProfileQuarterlyTrend(
                group.Key,
                group.Count(),
                group.Sum(snapshot => GetHighlightedGroups(snapshot).Count),
                TryAverage(group.SelectMany(snapshot => snapshot.Moves).Select(move => move.CentipawnLoss))))
            .OrderBy(item => item.QuarterKey, StringComparer.Ordinal)
            .ToList();

        return new PlayerProfileAggregateStats(
            topLabels,
            costliestLabels,
            mistakesByPhase,
            mistakesByOpening,
            gamesBySide,
            monthlyTrend,
            quarterlyTrend);
    }

    public static List<PriorityLabelStat> BuildPriorityLabels(
        IReadOnlyList<ProfileLabelStat> topLabels,
        IReadOnlyList<ProfileCostlyLabelStat> costliestLabels,
        IReadOnlyList<HighlightedGroup> highlightedGroups,
        IReadOnlyList<StoredMoveAnalysis> mistakeMoves)
    {
        Dictionary<string, ProfileLabelStat> frequentByLabel = topLabels
            .ToDictionary(item => item.Label, StringComparer.Ordinal);
        Dictionary<string, ProfileCostlyLabelStat> costlyByLabel = costliestLabels
            .ToDictionary(item => item.Label, StringComparer.Ordinal);
        Dictionary<string, int> highlightedCounts = highlightedGroups
            .GroupBy(group => group.Label, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);

        return mistakeMoves
            .GroupBy(move => move.MistakeLabel!, StringComparer.Ordinal)
            .Select(group =>
            {
                int count = group.Count();
                int totalCpl = group.Sum(move => Math.Max(0, move.CentipawnLoss ?? 0));
                int? averageCpl = TryAverage(group.Select(move => move.CentipawnLoss));
                double averageConfidence = group
                    .Where(move => move.MistakeConfidence.HasValue)
                    .Select(move => move.MistakeConfidence!.Value)
                    .DefaultIfEmpty(0.0)
                    .Average();
                int frequencyBoost = frequentByLabel.TryGetValue(group.Key, out ProfileLabelStat? frequent)
                    ? frequent.Count * 80
                    : count * 40;
                int costlyBoost = costlyByLabel.TryGetValue(group.Key, out ProfileCostlyLabelStat? costly)
                    ? costly.TotalCentipawnLoss
                    : totalCpl;
                int highlightBoost = highlightedCounts.TryGetValue(group.Key, out int highlightedCount)
                    ? highlightedCount * 90
                    : 0;
                int priorityScore = frequencyBoost + costlyBoost + highlightBoost + (int)Math.Round(averageConfidence * 40);

                return new PriorityLabelStat(group.Key, count, totalCpl, averageCpl, averageConfidence, priorityScore);
            })
            .OrderByDescending(item => item.PriorityScore)
            .ThenByDescending(item => item.TotalCentipawnLoss)
            .ThenByDescending(item => item.Count)
            .ThenBy(item => item.Label, StringComparer.Ordinal)
            .Take(3)
            .ToList();
    }

    public static IReadOnlyList<HighlightedGroup> GetHighlightedGroups(PlayerProfileSnapshot snapshot)
    {
        List<StoredMoveAnalysis> highlightedMoves = snapshot.Moves
            .Where(move => move.IsHighlighted)
            .OrderBy(move => move.Ply)
            .ToList();

        if (highlightedMoves.Count == 0)
        {
            return [];
        }

        List<HighlightedGroup> groups = [];
        List<StoredMoveAnalysis> currentGroup = [];

        foreach (StoredMoveAnalysis move in highlightedMoves)
        {
            if (currentGroup.Count == 0 || CanMergeHighlightedMoves(currentGroup[^1], move))
            {
                currentGroup.Add(move);
                continue;
            }

            groups.Add(BuildHighlightedGroup(currentGroup));
            currentGroup = [move];
        }

        if (currentGroup.Count > 0)
        {
            groups.Add(BuildHighlightedGroup(currentGroup));
        }

        return groups;
    }

    public static int? TryAverage(IEnumerable<int?> values)
    {
        List<int> knownValues = values
            .Where(value => value.HasValue)
            .Select(value => value!.Value)
            .ToList();

        return knownValues.Count == 0
            ? null
            : (int)Math.Round(knownValues.Average());
    }

    public static int SeverityWeight(MoveQualityBucket quality)
    {
        return quality switch
        {
            MoveQualityBucket.Blunder => 4,
            MoveQualityBucket.Mistake => 3,
            MoveQualityBucket.Inaccuracy => 2,
            _ => 1
        };
    }

    private static bool CanMergeHighlightedMoves(StoredMoveAnalysis previous, StoredMoveAnalysis current)
    {
        return string.Equals(previous.MistakeLabel ?? "unclassified", current.MistakeLabel ?? "unclassified", StringComparison.Ordinal)
            && previous.Quality == current.Quality
            && previous.MoveNumber + 1 >= current.MoveNumber
            && previous.Phase == current.Phase;
    }

    private static HighlightedGroup BuildHighlightedGroup(IReadOnlyList<StoredMoveAnalysis> moves)
    {
        StoredMoveAnalysis lead = moves
            .OrderByDescending(move => SeverityWeight(move.Quality))
            .ThenByDescending(move => move.CentipawnLoss ?? int.MinValue)
            .ThenBy(move => move.Ply)
            .First();

        double averageConfidence = moves
            .Where(move => move.MistakeConfidence.HasValue)
            .Select(move => move.MistakeConfidence!.Value)
            .DefaultIfEmpty(0.0)
            .Average();

        GamePhase? dominantPhase = moves
            .GroupBy(move => move.Phase)
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key)
            .Select(group => (GamePhase?)group.Key)
            .FirstOrDefault();

        return new HighlightedGroup(
            lead.MistakeLabel ?? "unclassified",
            averageConfidence,
            dominantPhase,
            lead.Quality);
    }
}

internal sealed record HighlightedGroup(
    string Label,
    double AverageConfidence,
    GamePhase? DominantPhase,
    MoveQualityBucket Quality);

internal sealed record PriorityLabelStat(
    string Label,
    int Count,
    int TotalCentipawnLoss,
    int? AverageCentipawnLoss,
    double AverageConfidence,
    int PriorityScore);

internal sealed record PlayerProfileAggregateStats(
    IReadOnlyList<ProfileLabelStat> TopLabels,
    IReadOnlyList<ProfileCostlyLabelStat> CostliestLabels,
    IReadOnlyList<ProfilePhaseStat> MistakesByPhase,
    IReadOnlyList<ProfileOpeningStat> MistakesByOpening,
    IReadOnlyList<ProfileSideStat> GamesBySide,
    IReadOnlyList<ProfileMonthlyTrend> MonthlyTrend,
    IReadOnlyList<ProfileQuarterlyTrend> QuarterlyTrend);
