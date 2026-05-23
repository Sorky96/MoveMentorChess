using System.Globalization;
using static MoveMentorChess.Profiles.PlayerProfileProgressAnalyzer;
using static MoveMentorChess.Profiles.PlayerProfileStatsAggregator;

namespace MoveMentorChess.Profiles;

internal sealed class PlayerRatingTrendAnalyzer
{
    private readonly IPlayerStrengthEstimator strengthEstimator;

    public PlayerRatingTrendAnalyzer(IPlayerStrengthEstimator strengthEstimator)
    {
        this.strengthEstimator = strengthEstimator ?? throw new ArgumentNullException(nameof(strengthEstimator));
    }

    public List<PlayerRatingTrendReport> BuildByTimeControl(IReadOnlyList<PlayerProfileSnapshot> snapshots)
    {
        return snapshots
            .Where(snapshot => snapshot.TimeControlCategory != GameTimeControlCategory.Unknown)
            .GroupBy(snapshot => snapshot.TimeControlCategory)
            .OrderBy(group => group.Key)
            .Select(group => Build(group.ToList(), group.Key))
            .ToList();
    }

    public PlayerRatingTrendReport Build(
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
}
