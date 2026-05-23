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
    private readonly PlayerProfileReportBuilder reportBuilder;

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
        reportBuilder = new PlayerProfileReportBuilder(new PlayerRatingTrendAnalyzer(this.strengthEstimator));
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

    private PlayerProfileReport BuildReport(List<PlayerProfileSnapshot> snapshots)
    {
        string playerKey = snapshots[0].PlayerKey;
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
        return reportBuilder.Build(snapshots, openingReport, trainingHistory);
    }
}

public sealed record ProfileDataAvailability(
    int ImportedGames,
    int AnalyzedProfiles,
    int OpeningTreePositions);
