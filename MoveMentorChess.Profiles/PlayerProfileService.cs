namespace MoveMentorChess.Profiles;

public sealed partial class PlayerProfileService
{
    private readonly IImportedGameStore importedGameStore;
    private readonly ProfileAnalysisDataSource analysisDataSource;
    private readonly IPlayerStrengthEstimator strengthEstimator;
    private readonly IOpeningTheoryStore? openingTheoryStore;
    private readonly IOpeningTreeStore? openingTreeStore;
    private readonly IOpeningTrainingHistoryStore? trainingHistoryStore;

    private readonly PlayerProfileSnapshotLoader snapshotLoader;
    private readonly PlayerProfileStatsAggregator statsAggregator;
    private readonly ProfileMistakeExampleSelector exampleSelector;

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
        this.analysisDataSource = analysisDataSource ?? throw new ArgumentNullException(nameof(analysisDataSource));
        this.strengthEstimator = strengthEstimator ?? new HeuristicPlayerStrengthEstimator();
        this.openingTheoryStore = openingTheoryStore;
        this.openingTreeStore = openingTreeStore;
        this.trainingHistoryStore = trainingHistoryStore;

        this.snapshotLoader = new PlayerProfileSnapshotLoader(this.analysisDataSource);
        this.statsAggregator = new PlayerProfileStatsAggregator(this.strengthEstimator);
        this.exampleSelector = new ProfileMistakeExampleSelector();
    }

    public IReadOnlyList<PlayerProfileSummary> ListProfiles(string? filterText = null, int limit = 100)
    {
        List<PlayerProfileSnapshot> snapshots = snapshotLoader.LoadSnapshots(filterText, Math.Max(limit * 8, 200));
        return snapshots
            .GroupBy(snapshot => snapshot.PlayerKey)
            .Select(statsAggregator.BuildSummary)
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
        List<PlayerProfileSnapshot> snapshots = snapshotLoader.LoadSnapshots(null, 2000)
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

    private PlayerProfileReport BuildReport(List<PlayerProfileSnapshot> snapshots)
    {
        string playerKey = snapshots[0].PlayerKey;
        string displayName = snapshots
            .GroupBy(snapshot => snapshot.DisplayName, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(item => item.Count())
            .ThenBy(item => item.Key, StringComparer.OrdinalIgnoreCase)
            .Select(item => item.Key)
            .First();

        List<HighlightedGroup> highlightedGroups = snapshots
            .SelectMany(PlayerProfileStatsAggregator.GetHighlightedGroups)
            .ToList();
        IReadOnlyList<StoredMoveAnalysis> mistakeMoves = snapshots
            .SelectMany(snapshot => snapshot.Moves)
            .Where(move => move.Quality.IsProblem() && !string.IsNullOrWhiteSpace(move.MistakeLabel))
            .ToList();

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
                PlayerProfileStatsAggregator.TryAverage(group.Select(move => move.CentipawnLoss))))
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
            .Select(group => new ProfileOpeningStat(group.First(), group.Count()))
            .OrderByDescending(item => item.Count)
            .ThenBy(item => item.Eco, StringComparer.OrdinalIgnoreCase)
            .Take(5)
            .ToList();

        IReadOnlyList<ProfileSideStat> gamesBySide = snapshots
            .GroupBy(snapshot => snapshot.Side)
            .Select(group => new ProfileSideStat(
                group.Key,
                group.Count(),
                group.Sum(snapshot => PlayerProfileStatsAggregator.GetHighlightedGroups(snapshot).Count)))
            .OrderBy(item => item.Side)
            .ToList();

        IReadOnlyList<ProfileMonthlyTrend> monthlyTrend = snapshots
            .GroupBy(snapshot => snapshot.MonthKey ?? "Unknown")
            .Select(group => new ProfileMonthlyTrend(
                group.Key,
                group.Count(),
                group.Sum(snapshot => PlayerProfileStatsAggregator.GetHighlightedGroups(snapshot).Count),
                PlayerProfileStatsAggregator.TryAverage(group.SelectMany(snapshot => snapshot.Moves).Select(move => move.CentipawnLoss))))
            .OrderBy(item => item.MonthKey, StringComparer.Ordinal)
            .ToList();

        IReadOnlyList<ProfileQuarterlyTrend> quarterlyTrend = snapshots
            .GroupBy(snapshot => snapshot.QuarterKey ?? "Unknown")
            .Select(group => new ProfileQuarterlyTrend(
                group.Key,
                group.Count(),
                group.Sum(snapshot => PlayerProfileStatsAggregator.GetHighlightedGroups(snapshot).Count),
                PlayerProfileStatsAggregator.TryAverage(group.SelectMany(snapshot => snapshot.Moves).Select(move => move.CentipawnLoss))))
            .OrderBy(item => item.QuarterKey, StringComparer.Ordinal)
            .ToList();

        ProfileProgressSignal progressSignal = statsAggregator.BuildProgressSignal(snapshots);
        IReadOnlyList<ProfileLabelTrend> labelTrends = statsAggregator.BuildLabelTrends(snapshots);
        PlayerRatingTrendReport overallRatingTrend = statsAggregator.BuildRatingTrend(snapshots, null);
        IReadOnlyList<PlayerRatingTrendReport> ratingTrendsByTimeControl = statsAggregator.BuildRatingTrendsByTimeControl(snapshots);

        IReadOnlyList<ProfileMistakeExample> allExamples = exampleSelector.BuildAllMistakeExamples(snapshots, topLabels, 9);

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
            PlayerProfileStatsAggregator.TryAverage(snapshots.SelectMany(snapshot => snapshot.Moves).Select(move => move.CentipawnLoss)),
            topLabels,
            costliestLabels,
            mistakesByPhase,
            mistakesByOpening,
            gamesBySide,
            monthlyTrend,
            quarterlyTrend,
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
            PlayerProfileStatsAggregator.TryAverage(snapshots.SelectMany(snapshot => snapshot.Moves).Select(move => move.CentipawnLoss)),
            topLabels,
            costliestLabels,
            mistakesByPhase,
            mistakesByOpening,
            gamesBySide,
            monthlyTrend,
            quarterlyTrend,
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

internal readonly record struct AnalysisVariantKey(
    string GameFingerprint,
    PlayerSide Side,
    int Depth,
    int MultiPv,
    int? MoveTimeMs);

internal readonly record struct SnapshotSelectionKey(
    string GameFingerprint,
    PlayerSide Side);

internal sealed record PlayerProfileSnapshot(
    string GameFingerprint,
    string PlayerKey,
    string DisplayName,
    PlayerSide Side,
    DateTime? GameDate,
    string? MonthKey,
    string? QuarterKey,
    string Eco,
    int Depth,
    int MultiPv,
    int? MoveTimeMs,
    DateTime AnalysisUpdatedUtc,
    int? PlayerRating,
    int? OpponentRating,
    string? Result,
    GameTimeControlCategory TimeControlCategory,
    string? TimeControl,
    string? UtcDate,
    string? UtcTime,
    string? EndDate,
    string? EndTime,
    string? Termination,
    string? Link,
    IReadOnlyList<StoredMoveAnalysis> Moves);

internal sealed record ProgressWindowSelection(
    int WindowDays,
    IReadOnlyList<PlayerProfileSnapshot> Previous,
    IReadOnlyList<PlayerProfileSnapshot> Recent);

internal sealed record HighlightedGroup(
    string Label,
    double AverageConfidence,
    GamePhase? DominantPhase,
    MoveQualityBucket Quality);

internal sealed record RecommendationContext(
    GamePhase? DominantPhase,
    PlayerSide? DominantSide,
    IReadOnlyList<string> TopOpenings);

internal sealed record RecommendationOccurrence(
    PlayerSide Side,
    GamePhase? Phase,
    string Eco);

internal sealed record MistakeExampleCandidate(
    PlayerProfileSnapshot Snapshot,
    StoredMoveAnalysis Move);

internal readonly record struct ExampleClusterKey(
    GamePhase Phase,
    string Eco);

internal sealed record PriorityLabelStat(
    string Label,
    int Count,
    int TotalCentipawnLoss,
    int? AverageCentipawnLoss,
    double AverageConfidence,
    int PriorityScore);

internal sealed class ExampleClusterKeyComparer : IEqualityComparer<ExampleClusterKey>
{
    public static ExampleClusterKeyComparer Instance { get; } = new();

    public bool Equals(ExampleClusterKey x, ExampleClusterKey y)
    {
        return x.Phase == y.Phase
            && string.Equals(x.Eco, y.Eco, StringComparison.OrdinalIgnoreCase);
    }

    public int GetHashCode(ExampleClusterKey obj)
    {
        return HashCode.Combine(obj.Phase, StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Eco ?? string.Empty));
    }
}
