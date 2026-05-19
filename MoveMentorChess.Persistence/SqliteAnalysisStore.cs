namespace MoveMentorChess.Persistence;

public sealed class SqliteAnalysisStore :
    IAnalysisStore,
    IImportedGameStore,
    IAnalysisResultStore,
    IStoredMoveAnalysisStore,
    IAdviceFeedbackStore,
    IAnalysisWindowStateStore,
    IOpeningTreeStore,
    IOpeningTheoryStore,
    IOpeningLineContextStore,
    IOpeningTrainingHistoryStore,
    IOpeningTrainingTelemetryStore
{
    private const string AppDataDirectoryName = "MoveMentorChessServices";
    private const string DatabaseFileName = "analysis-cache.db";
    public const string CurrentDerivedAnalysisDataVersion = "derived-analysis-v1";

    private readonly string databasePath;
    private readonly string derivedAnalysisDataVersion;
    private readonly bool applyDerivedAnalysisDataVersionPolicy;
    private readonly IClock clock;
    private readonly object sync = new();

    public SqliteAnalysisStore(
        string databasePath,
        string derivedAnalysisDataVersion = CurrentDerivedAnalysisDataVersion,
        bool applyDerivedAnalysisDataVersionPolicy = true,
        IClock? clock = null)
    {
        if (string.IsNullOrWhiteSpace(databasePath))
        {
            throw new ArgumentException("Database path is required.", nameof(databasePath));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(derivedAnalysisDataVersion);

        this.databasePath = databasePath;
        this.derivedAnalysisDataVersion = derivedAnalysisDataVersion;
        this.applyDerivedAnalysisDataVersionPolicy = applyDerivedAnalysisDataVersionPolicy;
        this.clock = clock ?? SystemClock.Instance;
        string? directory = Path.GetDirectoryName(databasePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        InitializeSchema();
    }

    public static SqliteAnalysisStore CreateDefault()
    {
        return new SqliteAnalysisStore(GetDefaultDatabasePath());
    }

    public static string GetDefaultDatabasePath()
    {
        string baseDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            AppDataDirectoryName);
        return Path.Combine(baseDirectory, DatabaseFileName);
    }

    public void SaveOpeningTree(OpeningTreeBuildResult tree)
    {
        ArgumentNullException.ThrowIfNull(tree);

        WithImmediateTransaction(database => SqliteOpeningTreeStore.SaveOpeningTree(database, tree));
    }

    public void ReplaceOpeningTree(OpeningTreeBuildResult tree)
    {
        ArgumentNullException.ThrowIfNull(tree);

        WithImmediateTransaction(database => SqliteOpeningTreeStore.ReplaceOpeningTree(database, tree));
    }

    public OpeningTreeBuildResult LoadOpeningTree()
    {
        return WithDatabase(SqliteOpeningTreeStore.LoadOpeningTree);
    }

    public string? GetOpeningSeedVersion()
    {
        return WithDatabase(SqliteOpeningTreeStore.GetOpeningSeedVersion);
    }

    public void SetOpeningSeedVersion(string version)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(version);

        WithDatabase(database => SqliteOpeningTreeStore.SetOpeningSeedVersion(database, version));
    }

    public OpeningTreeStoreSummary GetOpeningTreeSummary()
    {
        return WithDatabase(SqliteOpeningTreeStore.GetOpeningTreeSummary);
    }

    public bool TryGetOpeningPositionByKey(string positionKey, out OpeningTheoryPosition? position)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(positionKey);

        OpeningTheoryPosition? loadedPosition = null;
        bool found = WithDatabase(database =>
            SqliteOpeningTheoryStore.TryGetOpeningPositionByKey(database, positionKey, out loadedPosition));
        position = loadedPosition;
        return found;
    }

    public IReadOnlyList<OpeningTheoryMove> GetOpeningMovesByPositionKey(
        string positionKey,
        int limit = 10,
        bool playableOnly = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(positionKey);

        return WithDatabase(database =>
            SqliteOpeningTheoryStore.GetOpeningMovesByPositionKey(database, positionKey, limit, playableOnly));
    }

    public IReadOnlyList<OpeningLineCatalogItem> ListOpeningLines(string? filterText = null, RepertoireSide? repertoireSide = null, int limit = 100)
    {
        return WithDatabase(database =>
            SqliteOpeningTheoryStore.ListOpeningLines(database, filterText, repertoireSide, limit));
    }

    public IReadOnlyList<string> GetOpeningValidationMoves(OpeningPositionKey rootPositionKey)
    {
        return WithDatabase(database =>
            SqliteOpeningTheoryStore.GetOpeningValidationMoves(database, rootPositionKey));
    }

    public IReadOnlyList<OpeningLineMove> GetOpeningPathLineMoves(OpeningPositionKey rootPositionKey)
    {
        return WithDatabase(database =>
            SqliteOpeningTheoryStore.GetOpeningPathLineMoves(database, rootPositionKey));
    }

    public void SaveImportedGame(ImportedGame game)
    {
        ArgumentNullException.ThrowIfNull(game);

        WithImmediateTransaction(database =>
            SqliteImportedGameStore.SaveImportedGames(database, [game], clock.UtcNow));
    }

    public void SaveImportedGames(IReadOnlyList<ImportedGame> games)
    {
        ArgumentNullException.ThrowIfNull(games);
        if (games.Count == 0)
        {
            return;
        }

        WithImmediateTransaction(database =>
            SqliteImportedGameStore.SaveImportedGames(database, games, clock.UtcNow));
    }

    public bool TryLoadImportedGame(string gameFingerprint, out ImportedGame? game)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(gameFingerprint);

        ImportedGame? loadedGame = null;
        bool found = WithDatabase(database =>
            SqliteImportedGameStore.TryLoadImportedGame(database, gameFingerprint, out loadedGame));
        game = loadedGame;
        return found;
    }

    public bool DeleteImportedGame(string gameFingerprint)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(gameFingerprint);

        return WithDatabase(database => SqliteImportedGameStore.DeleteImportedGame(database, gameFingerprint));
    }

    public void ClearImportedAnalysisData()
    {
        WithImmediateTransaction(SqliteImportedGameStore.ClearImportedAnalysisData);
    }

    public void ClearDerivedAnalysisData()
    {
        WithDatabase(database =>
            SqliteAnalysisMetadataStore.ResetDerivedAnalysisData(database, derivedAnalysisDataVersion));
    }

    public string? GetDerivedAnalysisDataVersion()
    {
        return WithDatabase(SqliteAnalysisMetadataStore.GetDerivedAnalysisDataVersion);
    }

    public IReadOnlyList<SavedImportedGameSummary> ListImportedGames(string? filterText = null, int limit = 200)
    {
        return WithDatabase(database =>
            SqliteImportedGameStore.ListImportedGames(database, filterText, limit));
    }

    public IReadOnlyList<GameAnalysisResult> ListResults(string? filterText = null, int limit = 500)
    {
        return WithDatabase(database =>
            SqliteAnalysisResultStore.ListResults(database, filterText, limit));
    }

    public IReadOnlyList<StoredMoveAnalysis> ListMoveAnalyses(string? filterText = null, int limit = 5000)
    {
        return WithDatabase(database =>
            SqliteAnalysisResultStore.ListMoveAnalyses(database, filterText, limit));
    }

    public IReadOnlyList<MoveAdviceFeedback> ListMoveAdviceFeedback(string? filterText = null, int limit = 5000)
    {
        return WithDatabase(database =>
            SqliteMoveAdviceFeedbackStore.ListMoveAdviceFeedback(database, filterText, limit));
    }

    public void SaveMoveAdviceFeedback(MoveAdviceFeedback feedback)
    {
        ArgumentNullException.ThrowIfNull(feedback);

        WithDatabase(database => SqliteMoveAdviceFeedbackStore.SaveMoveAdviceFeedback(database, feedback));
    }

    public bool TryLoadResult(GameAnalysisCacheKey key, out GameAnalysisResult? result)
    {
        ArgumentNullException.ThrowIfNull(key);

        GameAnalysisResult? loadedResult = null;
        bool found = WithDatabase(database =>
            SqliteAnalysisResultStore.TryLoadResult(database, key, out loadedResult));
        result = loadedResult;
        return found;
    }

    public void SaveResult(GameAnalysisCacheKey key, GameAnalysisResult result)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(result);

        SaveImportedGame(result.Game);

        WithDatabase(database => SqliteAnalysisResultStore.SaveResult(database, key, result, clock.UtcNow));
    }

    public bool TryLoadWindowState(string gameFingerprint, out AnalysisWindowState? state)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(gameFingerprint);

        AnalysisWindowState? loadedState = null;
        bool found = WithDatabase(database =>
            SqliteAnalysisWindowStateStore.TryLoadWindowState(database, gameFingerprint, out loadedState));
        state = loadedState;
        return found;
    }

    public void SaveWindowState(string gameFingerprint, AnalysisWindowState state)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(gameFingerprint);
        ArgumentNullException.ThrowIfNull(state);

        WithDatabase(database =>
            SqliteAnalysisWindowStateStore.SaveWindowState(database, gameFingerprint, state, clock.UtcNow));
    }

    public void SaveOpeningTrainingSessionResult(OpeningTrainingSessionResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        WithDatabase(database => SqliteOpeningTrainingStore.SaveSessionResult(database, result));
    }

    public IReadOnlyList<OpeningTrainingSessionResult> ListOpeningTrainingSessionResults(string? playerKey = null, int limit = 200)
    {
        return WithDatabase(database =>
            SqliteOpeningTrainingStore.ListSessionResults(database, playerKey, limit));
    }

    public void SaveOpeningReviewItems(string playerKey, IReadOnlyList<OpeningReviewItem> items)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(playerKey);
        ArgumentNullException.ThrowIfNull(items);

        WithImmediateTransaction(database =>
            SqliteOpeningTrainingStore.SaveReviewItems(database, playerKey, items));
    }

    public IReadOnlyList<OpeningReviewItem> ListOpeningReviewItems(string? playerKey = null, int limit = 1000)
    {
        return WithDatabase(database =>
            SqliteOpeningTrainingStore.ListReviewItems(database, playerKey, limit));
    }

    public void SaveOpeningTrainingScheduledActions(string playerKey, IReadOnlyList<OpeningTrainingScheduledAction> actions)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(playerKey);
        ArgumentNullException.ThrowIfNull(actions);

        WithImmediateTransaction(database =>
            SqliteOpeningTrainingStore.SaveScheduledActions(database, playerKey, actions));
    }

    public IReadOnlyList<OpeningTrainingScheduledAction> ListDueOpeningTrainingScheduledActions(string? playerKey, DateTime nowUtc, int limit = 50)
    {
        return WithDatabase(database =>
            SqliteOpeningTrainingStore.ListDueScheduledActions(database, playerKey, nowUtc, limit));
    }

    public void MarkOpeningTrainingScheduledActionCompleted(string playerKey, string actionId, DateTime completedUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(playerKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(actionId);

        WithDatabase(database =>
            SqliteOpeningTrainingStore.MarkScheduledActionCompleted(database, playerKey, actionId, completedUtc));
    }

    public void SaveOpeningTrainingTelemetryEvent(OpeningTrainingTelemetryEvent telemetryEvent)
    {
        ArgumentNullException.ThrowIfNull(telemetryEvent);

        WithDatabase(database => SqliteOpeningTrainingTelemetryStore.SaveTelemetryEvent(database, telemetryEvent));
    }

    public IReadOnlyList<OpeningTrainingTelemetryEvent> ListOpeningTrainingTelemetryEvents(
        string? playerKey = null,
        DateTime? fromUtc = null,
        DateTime? toUtc = null,
        int limit = 500)
    {
        return WithDatabase(database =>
            SqliteOpeningTrainingTelemetryStore.ListTelemetryEvents(database, playerKey, fromUtc, toUtc, limit));
    }

    private void InitializeSchema()
    {
        WithDatabase(database =>
        {
            SqliteSchemaInitializer.Initialize(database);
            if (applyDerivedAnalysisDataVersionPolicy)
            {
                SqliteAnalysisMetadataStore.ApplyDerivedAnalysisDataVersionPolicy(
                    database,
                    derivedAnalysisDataVersion);
            }
        });
    }

    private SqliteDatabase OpenDatabase() => new(databasePath);

    private void WithDatabase(Action<SqliteDatabase> action)
    {
        lock (sync)
        {
            using SqliteDatabase database = OpenDatabase();
            action(database);
        }
    }

    private T WithDatabase<T>(Func<SqliteDatabase, T> action)
    {
        lock (sync)
        {
            using SqliteDatabase database = OpenDatabase();
            return action(database);
        }
    }

    private void WithImmediateTransaction(Action<SqliteDatabase> action)
    {
        WithDatabase(database => SqliteTransaction.RunImmediate(database, () => action(database)));
    }
}
