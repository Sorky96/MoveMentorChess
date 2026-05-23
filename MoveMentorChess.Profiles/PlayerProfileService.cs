using System.Globalization;
using static MoveMentorChess.Profiles.PlayerProfileProgressAnalyzer;
using static MoveMentorChess.Profiles.PlayerProfileStatsAggregator;

namespace MoveMentorChess.Profiles;

public sealed partial class PlayerProfileService
{
    private readonly IImportedGameStore importedGameStore;
    private readonly IPlayerStrengthEstimator strengthEstimator;
    private readonly IOpeningTheoryStore? openingTheoryStore;
    private readonly IOpeningTreeStore? openingTreeStore;
    private readonly IOpeningTrainingHistoryStore? trainingHistoryStore;
    private readonly PlayerProfileSnapshotLoader snapshotLoader;

    public PlayerProfileService(IAnalysisStore analysisStore)
        : this(
            analysisStore,
            new ProfileAnalysisDataSource(analysisStore, analysisStore),
            new HeuristicPlayerStrengthEstimator(),
            analysisStore as IOpeningTheoryStore,
            analysisStore as IOpeningTreeStore,
            analysisStore as IOpeningTrainingHistoryStore)
    {
    }

    public PlayerProfileService(IImportedGameStore importedGameStore)
        : this(
            importedGameStore,
            RequireResultStore(importedGameStore),
            RequireMoveAnalysisStore(importedGameStore),
            importedGameStore as IOpeningTheoryStore,
            importedGameStore as IOpeningTreeStore,
            importedGameStore as IOpeningTrainingHistoryStore)
    {
    }

    public PlayerProfileService(
        IImportedGameStore importedGameStore,
        IAnalysisResultStore resultStore,
        IStoredMoveAnalysisStore moveAnalysisStore,
        IOpeningTheoryStore? openingTheoryStore = null,
        IOpeningTreeStore? openingTreeStore = null,
        IOpeningTrainingHistoryStore? trainingHistoryStore = null)
        : this(
            importedGameStore,
            new ProfileAnalysisDataSource(moveAnalysisStore, resultStore),
            new HeuristicPlayerStrengthEstimator(),
            openingTheoryStore,
            openingTreeStore,
            trainingHistoryStore)
    {
    }

    internal PlayerProfileService(
        IImportedGameStore importedGameStore,
        ProfileAnalysisDataSource analysisDataSource,
        IPlayerStrengthEstimator? strengthEstimator = null,
        IOpeningTheoryStore? openingTheoryStore = null,
        IOpeningTreeStore? openingTreeStore = null,
        IOpeningTrainingHistoryStore? trainingHistoryStore = null)
    {
        this.importedGameStore = importedGameStore ?? throw new ArgumentNullException(nameof(importedGameStore));
        ArgumentNullException.ThrowIfNull(analysisDataSource);
        this.strengthEstimator = strengthEstimator ?? new HeuristicPlayerStrengthEstimator();
        this.openingTheoryStore = openingTheoryStore;
        this.openingTreeStore = openingTreeStore;
        this.trainingHistoryStore = trainingHistoryStore;
        snapshotLoader = new PlayerProfileSnapshotLoader(analysisDataSource);
    }

    public IReadOnlyList<PlayerProfileSummary> ListProfiles(string? filterText = null, int limit = 100)
    {
        List<PlayerProfileSnapshot> snapshots = LoadSnapshots(filterText, Math.Max(limit * 8, 200));
        return snapshots
            .GroupBy(snapshot => snapshot.PlayerKey)
            .Select(BuildSummary)
            .OrderByDescending(summary => summary.GamesAnalyzed)
            .ThenByDescending(summary => summary.HighlightedMistakes)
            .ThenBy(summary => summary.DisplayName, StringComparer.OrdinalIgnoreCase)
            .Take(limit)
            .ToList();
    }

    public ProfileDataAvailability GetDataAvailability(string? filterText = null)
    {
        int importedGames = importedGameStore.ListImportedGames(filterText, 1).Count;
        int analyzedProfiles = ListProfiles(filterText, 1).Count;
        int openingTreePositions = openingTreeStore is not null
            ? openingTreeStore.GetOpeningTreeSummary().NodeCount
            : 0;

        return new ProfileDataAvailability(importedGames, analyzedProfiles, openingTreePositions);
    }

    public bool TryBuildProfile(string playerKeyOrName, out PlayerProfileReport? report)
    {
        if (string.IsNullOrWhiteSpace(playerKeyOrName))
        {
            report = null;
            return false;
        }

        string normalized = PlayerProfileSnapshotLoader.NormalizePlayerKey(playerKeyOrName);
        List<PlayerProfileSnapshot> snapshots = LoadSnapshots(null, 2000)
            .Where(snapshot => snapshot.PlayerKey == normalized
                || string.Equals(snapshot.DisplayName, playerKeyOrName, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (snapshots.Count == 0)
        {
            report = null;
            return false;
        }

        report = BuildReport(snapshots);
        return true;
    }

    public bool TryBuildOpeningWeaknessReport(string playerKeyOrName, out OpeningWeaknessReport? report)
    {
        return new OpeningWeaknessService(
            RequireResultStore(importedGameStore),
            RequireMoveAnalysisStore(importedGameStore),
            openingTheoryStore).TryBuildReport(playerKeyOrName, out report);
    }

    public bool TryBuildOpeningTrainingSession(
        string playerKeyOrName,
        out OpeningTrainingSession? session,
        OpeningTrainingSessionOptions? options = null)
    {
        return new OpeningTrainerService(
            importedGameStore,
            RequireResultStore(importedGameStore),
            RequireMoveAnalysisStore(importedGameStore),
            openingTheoryStore,
            trainingHistoryStore).TryBuildSession(playerKeyOrName, out session, options);
    }

    private List<PlayerProfileSnapshot> LoadSnapshots(string? filterText, int limit)
        => snapshotLoader.Load(filterText, limit);

    private static IAnalysisResultStore RequireResultStore(object store)
    {
        return store as IAnalysisResultStore
            ?? throw new ArgumentException("The store must also provide analysis results.", nameof(store));
    }

    private static IStoredMoveAnalysisStore RequireMoveAnalysisStore(object store)
    {
        return store as IStoredMoveAnalysisStore
            ?? throw new ArgumentException("The store must also provide stored move analyses.", nameof(store));
    }

    private List<PlayerRatingTrendReport> BuildRatingTrendsByTimeControl(IReadOnlyList<PlayerProfileSnapshot> snapshots)
    {
        return snapshots
            .Where(snapshot => snapshot.TimeControlCategory != GameTimeControlCategory.Unknown)
            .GroupBy(snapshot => snapshot.TimeControlCategory)
            .OrderBy(group => group.Key)
            .Select(group => BuildRatingTrend(group.ToList(), group.Key))
            .ToList();
    }

    private PlayerRatingTrendReport BuildRatingTrend(
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

    private PlayerProfileReport BuildReport(List<PlayerProfileSnapshot> snapshots)
    {
        string playerKey = snapshots[0].PlayerKey;
        string displayName = SelectDisplayName(snapshots);

        List<HighlightedGroup> highlightedGroups = snapshots
            .SelectMany(GetHighlightedGroups)
            .ToList();
        IReadOnlyList<StoredMoveAnalysis> mistakeMoves = snapshots
            .SelectMany(snapshot => snapshot.Moves)
            .Where(move => move.Quality.IsProblem() && !string.IsNullOrWhiteSpace(move.MistakeLabel))
            .ToList();
        PlayerProfileAggregateStats aggregateStats = BuildReportStats(snapshots, highlightedGroups, mistakeMoves);

        ProfileProgressSignal progressSignal = BuildProgressSignal(snapshots);
        IReadOnlyList<ProfileLabelTrend> labelTrends = BuildLabelTrends(snapshots);
        PlayerRatingTrendReport overallRatingTrend = BuildRatingTrend(snapshots, null);
        IReadOnlyList<PlayerRatingTrendReport> ratingTrendsByTimeControl = BuildRatingTrendsByTimeControl(snapshots);

        IReadOnlyList<ProfileMistakeExample> allExamples = BuildAllMistakeExamples(snapshots, aggregateStats.TopLabels, 9);

        WeeklyTrainingPlan emptyWeeklyPlan = new(
            $"{displayName} Weekly Training Plan",
            "Training plan is being prepared from the current profile.",
            new WeeklyTrainingBudget(
                0,
                0,
                0,
                0,
                0,
                "Budget will be calculated after training priorities are ranked."),
            []);
        PlayerProfileReport draftReport = new(
            playerKey,
            displayName,
            snapshots.Count,
            snapshots.Sum(snapshot => snapshot.Moves.Count),
            highlightedGroups.Count,
            TryAverage(snapshots.SelectMany(snapshot => snapshot.Moves).Select(move => move.CentipawnLoss)),
            aggregateStats.TopLabels,
            aggregateStats.CostliestLabels,
            aggregateStats.MistakesByPhase,
            aggregateStats.MistakesByOpening,
            aggregateStats.GamesBySide,
            aggregateStats.MonthlyTrend,
            aggregateStats.QuarterlyTrend,
            overallRatingTrend,
            ratingTrendsByTimeControl,
            progressSignal,
            labelTrends,
            [],
            emptyWeeklyPlan,
            allExamples,
            new TrainingPlanReport(
                playerKey,
                displayName,
                progressSignal.Direction,
                "Training plan is being prepared from the current profile.",
                [],
                [],
                emptyWeeklyPlan));
        OpeningWeaknessReport? openingReport = new OpeningWeaknessService(
            RequireResultStore(importedGameStore),
            RequireMoveAnalysisStore(importedGameStore),
            openingTheoryStore)
            .TryBuildReport(playerKey, out OpeningWeaknessReport? builtOpeningReport)
                ? builtOpeningReport
                : null;
        IReadOnlyList<OpeningTrainingSessionResult> trainingHistory = trainingHistoryStore is not null
            ? trainingHistoryStore.ListOpeningTrainingSessionResults(playerKey)
            : [];
        TrainingPlanReport trainingPlan = new TrainingPlanService().Build(draftReport, openingReport, trainingHistory);

        return new PlayerProfileReport(
            playerKey,
            displayName,
            snapshots.Count,
            snapshots.Sum(snapshot => snapshot.Moves.Count),
            highlightedGroups.Count,
            TryAverage(snapshots.SelectMany(snapshot => snapshot.Moves).Select(move => move.CentipawnLoss)),
            aggregateStats.TopLabels,
            aggregateStats.CostliestLabels,
            aggregateStats.MistakesByPhase,
            aggregateStats.MistakesByOpening,
            aggregateStats.GamesBySide,
            aggregateStats.MonthlyTrend,
            aggregateStats.QuarterlyTrend,
            overallRatingTrend,
            ratingTrendsByTimeControl,
            progressSignal,
            labelTrends,
            trainingPlan.Recommendations,
            trainingPlan.WeeklyPlan,
            allExamples,
            trainingPlan);
    }
}

public sealed record ProfileDataAvailability(
    int ImportedGames,
    int AnalyzedProfiles,
    int OpeningTreePositions);

public sealed partial class PlayerProfileService
{
    private static List<RecommendationOccurrence> BuildRecommendationOccurrences(PlayerProfileSnapshot snapshot, string label)
    {
        List<RecommendationOccurrence> highlightedOccurrences = GetHighlightedGroups(snapshot)
            .Where(group => string.Equals(group.Label, label, StringComparison.Ordinal))
            .Select(group => new RecommendationOccurrence(snapshot.Side, group.DominantPhase, snapshot.Eco))
            .ToList();

        if (highlightedOccurrences.Any(item => item.Phase.HasValue))
        {
            return highlightedOccurrences;
        }

        List<RecommendationOccurrence> moveOccurrences = snapshot.Moves
            .Where(move => string.Equals(move.MistakeLabel ?? "unclassified", label, StringComparison.Ordinal))
            .Select(move => new RecommendationOccurrence(snapshot.Side, move.Phase, snapshot.Eco))
            .ToList();

        return moveOccurrences.Count > 0
            ? moveOccurrences
            : highlightedOccurrences;
    }

    private static List<ProfileMistakeExample> BuildAllMistakeExamples(
        IReadOnlyList<PlayerProfileSnapshot> snapshots,
        IReadOnlyList<ProfileLabelStat> topLabels,
        int maxTotal)
    {
        if (topLabels.Count == 0)
        {
            return [];
        }

        int perLabel = Math.Max(1, maxTotal / topLabels.Count);
        return topLabels
            .SelectMany(label =>
            {
                RecommendationContext context = BuildRecommendationContext(snapshots, label.Label);
                return ProfileMistakeExampleSelector.BuildForLabel(snapshots, label.Label, context, perLabel);
            })
            .OrderByDescending(example => example.CentipawnLoss ?? 0)
            .Take(maxTotal)
            .ToList();
    }

    private static RecommendationContext BuildRecommendationContext(IReadOnlyList<PlayerProfileSnapshot> snapshots, string label)
    {
        List<RecommendationOccurrence> occurrences = snapshots
            .SelectMany(snapshot => BuildRecommendationOccurrences(snapshot, label))
            .ToList();

        if (occurrences.Count == 0)
        {
            return new RecommendationContext(null, null, []);
        }

        GamePhase? dominantPhase = occurrences
            .Where(item => item.Phase.HasValue)
            .GroupBy(item => item.Phase!.Value)
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key)
            .Select(group => (GamePhase?)group.Key)
            .FirstOrDefault();

        PlayerSide? dominantSide = occurrences
            .GroupBy(item => item.Side)
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key)
            .Select(group => (PlayerSide?)group.Key)
            .FirstOrDefault();

        IReadOnlyList<string> topOpenings = occurrences
            .GroupBy(item => item.Eco, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .Take(2)
            .Select(group => group.Key)
            .ToList();

        return new RecommendationContext(dominantPhase, dominantSide, topOpenings);
    }
}
