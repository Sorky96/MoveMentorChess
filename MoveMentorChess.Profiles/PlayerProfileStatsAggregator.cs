using System.Globalization;

namespace MoveMentorChess.Profiles;

internal sealed class PlayerProfileStatsAggregator
{
    private const int MinimumGamesPerProgressWindow = 2;
    private static readonly int[] ProgressWindowDays = [14, 30];
    private readonly IPlayerStrengthEstimator strengthEstimator;

    public PlayerProfileStatsAggregator(IPlayerStrengthEstimator strengthEstimator)
    {
        this.strengthEstimator = strengthEstimator ?? throw new ArgumentNullException(nameof(strengthEstimator));
    }

    public PlayerProfileSummary BuildSummary(IGrouping<string, PlayerProfileSnapshot> group)
    {
        string displayName = group
            .GroupBy(snapshot => snapshot.DisplayName, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(item => item.Count())
            .ThenBy(item => item.Key, StringComparer.OrdinalIgnoreCase)
            .Select(item => item.Key)
            .First();

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

    public List<PriorityLabelStat> BuildPriorityLabels(
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

    public ProfileProgressSignal BuildProgressSignal(IReadOnlyList<PlayerProfileSnapshot> snapshots)
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

        int cplDelta = (recentPeriod.AverageCentipawnLoss ?? 0) - (previousPeriod.AverageCentipawnLoss ?? 0);
        double highlightDelta = recentPeriod.HighlightedMistakesPerGame - previousPeriod.HighlightedMistakesPerGame;

        if ((previousPeriod.AverageCentipawnLoss is null || recentPeriod.AverageCentipawnLoss is null)
            && selection.Previous.Count < MinimumGamesPerProgressWindow)
        {
            return new ProfileProgressSignal(
                ProfileProgressDirection.InsufficientData,
                "Not enough reliable data to measure progress yet.",
                recentPeriod,
                previousPeriod);
        }

        ProfileProgressDirection direction;
        string summary;
        if (cplDelta <= -35 || (cplDelta <= -25 && highlightDelta <= -0.15))
        {
            direction = ProfileProgressDirection.Improving;
            summary = $"Recent games are cleaner: average CPL improved by {Math.Abs(cplDelta)} and highlighted mistakes per game also dropped.";
        }
        else if (cplDelta >= 35 || (cplDelta >= 25 && highlightDelta >= 0.15))
        {
            direction = ProfileProgressDirection.Regressing;
            summary = $"Recent games are rougher: average CPL rose by {cplDelta} and the number of highlighted mistakes per game increased.";
        }
        else
        {
            direction = ProfileProgressDirection.Stable;
            summary = "Recent results are broadly stable versus the earlier sample, with no strong improvement or regression signal yet.";
        }

        return new ProfileProgressSignal(direction, summary, recentPeriod, previousPeriod);
    }

    public List<ProfileLabelTrend> BuildLabelTrends(IReadOnlyList<PlayerProfileSnapshot> snapshots)
    {
        if (!TrySelectProgressWindows(snapshots, out ProgressWindowSelection? selection) || selection is null)
        {
            return [];
        }

        return snapshots
            .SelectMany(snapshot => snapshot.Moves)
            .Where(move => move.Move.Quality.IsProblem() && !string.IsNullOrWhiteSpace(move.Advice.MistakeLabel))
            .Select(move => move.Advice.MistakeLabel!)
            .Distinct(StringComparer.Ordinal)
            .Select(label => BuildLabelTrend(label, selection.Previous, selection.Recent))
            .OrderBy(item => item.Label, StringComparer.Ordinal)
            .ToList();
    }

    public List<PlayerRatingTrendReport> BuildRatingTrendsByTimeControl(IReadOnlyList<PlayerProfileSnapshot> snapshots)
    {
        return snapshots
            .Where(snapshot => snapshot.TimeControlCategory != GameTimeControlCategory.Unknown)
            .GroupBy(snapshot => snapshot.TimeControlCategory)
            .OrderBy(group => group.Key)
            .Select(group => BuildRatingTrend(group.ToList(), group.Key))
            .ToList();
    }

    public PlayerRatingTrendReport BuildRatingTrend(
        IReadOnlyList<PlayerProfileSnapshot> snapshots,
        GameTimeControlCategory? category)
    {
        List<PlayerProfileSnapshot> ordered = snapshots
            .Where(snapshot => !category.HasValue || snapshot.TimeControlCategory == category.Value)
            .OrderBy(snapshot => GetSnapshotDate(snapshot) ?? DateTime.MaxValue)
            .ThenBy(snapshot => snapshot.GameFingerprint, StringComparer.Ordinal)
            .ToList();

        Dictionary<GameTimeControlCategory, int> sampleSizes = ordered
            .GroupBy(snapshot => snapshot.TimeControlCategory)
            .ToDictionary(group => group.Key, group => group.Count());

        List<PlayerRatingSnapshot> ratingPoints = [];
        List<MoveMentorStrengthPoint> strengthPoints = [];
        foreach (PlayerProfileSnapshot snapshot in ordered)
        {
            double? actualScore = GetActualScore(snapshot);
            double? expectedScore = GetExpectedScore(snapshot.PlayerRating, snapshot.OpponentRating);
            ratingPoints.Add(new PlayerRatingSnapshot(
                snapshot.GameFingerprint,
                GetSnapshotDate(snapshot),
                snapshot.TimeControlCategory,
                snapshot.PlayerRating,
                snapshot.OpponentRating,
                actualScore,
                expectedScore));

            int sampleSize = sampleSizes.TryGetValue(snapshot.TimeControlCategory, out int count) ? count : ordered.Count;
            strengthPoints.Add(strengthEstimator.Estimate(new PlayerStrengthEstimateInput(
                snapshot.GameFingerprint,
                GetSnapshotDate(snapshot),
                snapshot.TimeControlCategory,
                snapshot.PlayerRating,
                snapshot.OpponentRating,
                actualScore,
                expectedScore,
                snapshot.Moves,
                sampleSize)));
        }

        IReadOnlyList<ProfileMonthlyTrend> cplTrend = ordered
            .GroupBy(BuildWeekKey)
            .Select(group => new ProfileMonthlyTrend(
                group.Key,
                group.Count(),
                group.Sum(snapshot => GetHighlightedGroups(snapshot).Count),
                TryAverage(group.SelectMany(snapshot => snapshot.Moves).Select(move => move.CentipawnLoss))))
            .OrderBy(item => item.MonthKey, StringComparer.Ordinal)
            .TakeLast(8)
            .ToList();

        IReadOnlyList<ProfileMoveQualityTrend> qualityTrend = ordered
            .GroupBy(BuildWeekKey)
            .Select(group => BuildMoveQualityTrend(group.Key, group.ToList()))
            .OrderBy(item => item.PeriodKey, StringComparer.Ordinal)
            .TakeLast(8)
            .ToList();

        MoveMentorStrengthPoint? currentStrength = strengthPoints.LastOrDefault();
        int? currentRating = ratingPoints.LastOrDefault(point => point.PlayerRating.HasValue)?.PlayerRating;
        string label = category.HasValue ? category.Value.ToString() : "Overall";
        string summary = currentStrength is null
            ? $"No MoveMentor estimated strength data yet for {label}."
            : $"{label}: MoveMentor estimated strength {currentStrength.EstimatedStrength} ({currentStrength.Low}-{currentStrength.High}), {currentStrength.Confidence.ToString().ToLowerInvariant()} confidence.";

        return new PlayerRatingTrendReport(
            category,
            ordered.Count,
            currentRating,
            currentStrength,
            ratingPoints,
            strengthPoints,
            cplTrend,
            qualityTrend,
            summary);
    }

    private static ProfileMoveQualityTrend BuildMoveQualityTrend(string periodKey, List<PlayerProfileSnapshot> snapshots)
    {
        int gameCount = Math.Max(1, snapshots.Count);
        IReadOnlyList<StoredMoveAnalysis> moves = snapshots.SelectMany(snapshot => snapshot.Moves).ToList();
        return new ProfileMoveQualityTrend(
            periodKey,
            snapshots.Count,
            Math.Round(moves.Count(move => move.Quality == MoveQualityBucket.Blunder) / (double)gameCount, 2),
            Math.Round(moves.Count(move => move.Quality == MoveQualityBucket.Mistake) / (double)gameCount, 2),
            Math.Round(moves.Count(move => move.Quality == MoveQualityBucket.Inaccuracy) / (double)gameCount, 2),
            Math.Round(moves.Count(move => move.Quality is MoveQualityBucket.Brilliant or MoveQualityBucket.Great or MoveQualityBucket.Best) / (double)gameCount, 2));
    }

    private static string BuildWeekKey(PlayerProfileSnapshot snapshot)
    {
        DateTime date = (GetSnapshotDate(snapshot) ?? snapshot.AnalysisUpdatedUtc).Date;
        int daysFromMonday = ((int)date.DayOfWeek + 6) % 7;
        DateTime weekStart = date.AddDays(-daysFromMonday);
        return weekStart.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }

    private static double? GetExpectedScore(int? playerRating, int? opponentRating)
    {
        if (!playerRating.HasValue || !opponentRating.HasValue)
        {
            return null;
        }

        return 1.0 / (1.0 + Math.Pow(10.0, (opponentRating.Value - playerRating.Value) / 400.0));
    }

    private static double? GetActualScore(PlayerProfileSnapshot snapshot)
    {
        if (string.IsNullOrWhiteSpace(snapshot.Result))
        {
            return null;
        }

        return snapshot.Result.Trim() switch
        {
            "1/2-1/2" => 0.5,
            "1-0" => snapshot.Side == PlayerSide.White ? 1.0 : 0.0,
            "0-1" => snapshot.Side == PlayerSide.Black ? 1.0 : 0.0,
            _ => null
        };
    }

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

    private static DateTime? GetSnapshotDate(PlayerProfileSnapshot snapshot)
    {
        return snapshot.GameDate?.Date ?? snapshot.AnalysisUpdatedUtc.Date;
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
        int? previousAverageCpl = TryAverage(previousMoves.Select(move => move.CentipawnLoss));
        int? recentAverageCpl = TryAverage(recentMoves.Select(move => move.CentipawnLoss));

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
        int highlightedMistakes = snapshots.Sum(snapshot => GetHighlightedGroups(snapshot).Count);
        double highlightsPerGame = snapshots.Count == 0
            ? 0.0
            : Math.Round((double)highlightedMistakes / snapshots.Count, 2);

        return new ProfileProgressPeriod(
            label,
            snapshots.Count,
            TryAverage(snapshots.SelectMany(snapshot => snapshot.Moves).Select(move => move.CentipawnLoss)),
            highlightsPerGame);
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
}
