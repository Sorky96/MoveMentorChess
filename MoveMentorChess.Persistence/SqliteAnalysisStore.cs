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
    private const string DerivedAnalysisDataVersionKey = "derived_analysis_data_version";
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

        lock (sync)
        {
            using SqliteDatabase database = OpenDatabase();
            SqliteTransaction.RunImmediate(
                database,
                () => SqliteOpeningTreeStore.SaveOpeningTree(database, tree));
        }
    }

    public void ReplaceOpeningTree(OpeningTreeBuildResult tree)
    {
        ArgumentNullException.ThrowIfNull(tree);

        lock (sync)
        {
            using SqliteDatabase database = OpenDatabase();
            SqliteTransaction.RunImmediate(
                database,
                () => SqliteOpeningTreeStore.ReplaceOpeningTree(database, tree));
        }
    }

    public OpeningTreeBuildResult LoadOpeningTree()
    {
        lock (sync)
        {
            using SqliteDatabase database = OpenDatabase();
            return SqliteOpeningTreeStore.LoadOpeningTree(database);
        }
    }

    public string? GetOpeningSeedVersion()
    {
        lock (sync)
        {
            using SqliteDatabase database = OpenDatabase();
            return SqliteOpeningTreeStore.GetOpeningSeedVersion(database);
        }
    }

    public void SetOpeningSeedVersion(string version)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(version);

        lock (sync)
        {
            using SqliteDatabase database = OpenDatabase();
            SqliteOpeningTreeStore.SetOpeningSeedVersion(database, version);
        }
    }

    public OpeningTreeStoreSummary GetOpeningTreeSummary()
    {
        lock (sync)
        {
            using SqliteDatabase database = OpenDatabase();
            return SqliteOpeningTreeStore.GetOpeningTreeSummary(database);
        }
    }

    public bool TryGetOpeningPositionByKey(string positionKey, out OpeningTheoryPosition? position)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(positionKey);

        lock (sync)
        {
            using SqliteDatabase database = OpenDatabase();
            return SqliteOpeningTheoryStore.TryGetOpeningPositionByKey(database, positionKey, out position);
        }
    }

    public IReadOnlyList<OpeningTheoryMove> GetOpeningMovesByPositionKey(
        string positionKey,
        int limit = 10,
        bool playableOnly = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(positionKey);

        lock (sync)
        {
            using SqliteDatabase database = OpenDatabase();
            return SqliteOpeningTheoryStore.GetOpeningMovesByPositionKey(database, positionKey, limit, playableOnly);
        }
    }

    public IReadOnlyList<OpeningLineCatalogItem> ListOpeningLines(string? filterText = null, RepertoireSide? repertoireSide = null, int limit = 100)
    {
        lock (sync)
        {
            using SqliteDatabase database = OpenDatabase();
            return SqliteOpeningTheoryStore.ListOpeningLines(database, filterText, repertoireSide, limit);
        }
    }

    public IReadOnlyList<string> GetOpeningValidationMoves(OpeningPositionKey rootPositionKey)
    {
        lock (sync)
        {
            using SqliteDatabase database = OpenDatabase();
            return SqliteOpeningTheoryStore.GetOpeningValidationMoves(database, rootPositionKey);
        }
    }

    public IReadOnlyList<OpeningLineMove> GetOpeningPathLineMoves(OpeningPositionKey rootPositionKey)
    {
        lock (sync)
        {
            using SqliteDatabase database = OpenDatabase();
            return SqliteOpeningTheoryStore.GetOpeningPathLineMoves(database, rootPositionKey);
        }
    }

    public void SaveImportedGame(ImportedGame game)
    {
        ArgumentNullException.ThrowIfNull(game);

        lock (sync)
        {
            using SqliteDatabase database = OpenDatabase();
            SqliteTransaction.RunImmediate(
                database,
                () => SqliteImportedGameStore.SaveImportedGames(database, [game], clock.UtcNow));
        }
    }

    public void SaveImportedGames(IReadOnlyList<ImportedGame> games)
    {
        ArgumentNullException.ThrowIfNull(games);
        if (games.Count == 0)
        {
            return;
        }

        lock (sync)
        {
            using SqliteDatabase database = OpenDatabase();
            SqliteTransaction.RunImmediate(
                database,
                () => SqliteImportedGameStore.SaveImportedGames(database, games, clock.UtcNow));
        }
    }

    public bool TryLoadImportedGame(string gameFingerprint, out ImportedGame? game)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(gameFingerprint);

        lock (sync)
        {
            using SqliteDatabase database = OpenDatabase();
            return SqliteImportedGameStore.TryLoadImportedGame(database, gameFingerprint, out game);
        }
    }

    public bool DeleteImportedGame(string gameFingerprint)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(gameFingerprint);

        lock (sync)
        {
            using SqliteDatabase database = OpenDatabase();
            return SqliteImportedGameStore.DeleteImportedGame(database, gameFingerprint);
        }
    }

    public void ClearImportedAnalysisData()
    {
        lock (sync)
        {
            using SqliteDatabase database = OpenDatabase();
            SqliteTransaction.RunImmediate(
                database,
                () => SqliteImportedGameStore.ClearImportedAnalysisData(database));
        }
    }

    public void ClearDerivedAnalysisData()
    {
        lock (sync)
        {
            using SqliteDatabase database = OpenDatabase();
            SqliteTransaction.RunImmediate(database, () =>
            {
                SqliteAnalysisMetadataStore.ClearDerivedAnalysisData(database);
                SqliteAnalysisMetadataStore.SetMetadataValue(database, DerivedAnalysisDataVersionKey, derivedAnalysisDataVersion);
            });
        }
    }

    public string? GetDerivedAnalysisDataVersion()
    {
        lock (sync)
        {
            using SqliteDatabase database = OpenDatabase();
            return SqliteAnalysisMetadataStore.GetMetadataValue(database, DerivedAnalysisDataVersionKey);
        }
    }

    public IReadOnlyList<SavedImportedGameSummary> ListImportedGames(string? filterText = null, int limit = 200)
    {
        lock (sync)
        {
            using SqliteDatabase database = OpenDatabase();
            return SqliteImportedGameStore.ListImportedGames(database, filterText, limit);
        }
    }

    public IReadOnlyList<GameAnalysisResult> ListResults(string? filterText = null, int limit = 500)
    {
        lock (sync)
        {
            using SqliteDatabase database = OpenDatabase();
            return SqliteAnalysisResultStore.ListResults(database, filterText, limit);
        }
    }

    public IReadOnlyList<StoredMoveAnalysis> ListMoveAnalyses(string? filterText = null, int limit = 5000)
    {
        lock (sync)
        {
            using SqliteDatabase database = OpenDatabase();
            return SqliteAnalysisResultStore.ListMoveAnalyses(database, filterText, limit);
        }
    }

    public IReadOnlyList<MoveAdviceFeedback> ListMoveAdviceFeedback(string? filterText = null, int limit = 5000)
    {
        lock (sync)
        {
            using SqliteDatabase database = OpenDatabase();
            return SqliteMoveAdviceFeedbackStore.ListMoveAdviceFeedback(database, filterText, limit);
        }
    }

    public void SaveMoveAdviceFeedback(MoveAdviceFeedback feedback)
    {
        ArgumentNullException.ThrowIfNull(feedback);

        lock (sync)
        {
            using SqliteDatabase database = OpenDatabase();
            SqliteMoveAdviceFeedbackStore.SaveMoveAdviceFeedback(database, feedback);
        }
    }

    public bool TryLoadResult(GameAnalysisCacheKey key, out GameAnalysisResult? result)
    {
        ArgumentNullException.ThrowIfNull(key);

        lock (sync)
        {
            using SqliteDatabase database = OpenDatabase();
            return SqliteAnalysisResultStore.TryLoadResult(database, key, out result);
        }
    }

    public void SaveResult(GameAnalysisCacheKey key, GameAnalysisResult result)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(result);

        SaveImportedGame(result.Game);

        lock (sync)
        {
            using SqliteDatabase database = OpenDatabase();
            SqliteAnalysisResultStore.SaveResult(database, key, result, clock.UtcNow);
        }
    }

    public bool TryLoadWindowState(string gameFingerprint, out AnalysisWindowState? state)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(gameFingerprint);

        lock (sync)
        {
            using SqliteDatabase database = OpenDatabase();
            return SqliteAnalysisWindowStateStore.TryLoadWindowState(database, gameFingerprint, out state);
        }
    }

    public void SaveWindowState(string gameFingerprint, AnalysisWindowState state)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(gameFingerprint);
        ArgumentNullException.ThrowIfNull(state);

        lock (sync)
        {
            using SqliteDatabase database = OpenDatabase();
            SqliteAnalysisWindowStateStore.SaveWindowState(database, gameFingerprint, state, clock.UtcNow);
        }
    }

    public void SaveOpeningTrainingSessionResult(OpeningTrainingSessionResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        lock (sync)
        {
            using SqliteDatabase database = OpenDatabase();
            SqliteOpeningTrainingStore.SaveSessionResult(database, result);
        }
    }

    public IReadOnlyList<OpeningTrainingSessionResult> ListOpeningTrainingSessionResults(string? playerKey = null, int limit = 200)
    {
        lock (sync)
        {
            using SqliteDatabase database = OpenDatabase();
            return SqliteOpeningTrainingStore.ListSessionResults(database, playerKey, limit);
        }
    }

    public void SaveOpeningReviewItems(string playerKey, IReadOnlyList<OpeningReviewItem> items)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(playerKey);
        ArgumentNullException.ThrowIfNull(items);

        lock (sync)
        {
            using SqliteDatabase database = OpenDatabase();
            SqliteTransaction.RunImmediate(
                database,
                () => SqliteOpeningTrainingStore.SaveReviewItems(database, playerKey, items));
        }
    }

    public IReadOnlyList<OpeningReviewItem> ListOpeningReviewItems(string? playerKey = null, int limit = 1000)
    {
        lock (sync)
        {
            using SqliteDatabase database = OpenDatabase();
            return SqliteOpeningTrainingStore.ListReviewItems(database, playerKey, limit);
        }
    }

    public void SaveOpeningTrainingScheduledActions(string playerKey, IReadOnlyList<OpeningTrainingScheduledAction> actions)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(playerKey);
        ArgumentNullException.ThrowIfNull(actions);

        lock (sync)
        {
            using SqliteDatabase database = OpenDatabase();
            SqliteTransaction.RunImmediate(
                database,
                () => SqliteOpeningTrainingStore.SaveScheduledActions(database, playerKey, actions));
        }
    }

    public IReadOnlyList<OpeningTrainingScheduledAction> ListDueOpeningTrainingScheduledActions(string? playerKey, DateTime nowUtc, int limit = 50)
    {
        lock (sync)
        {
            using SqliteDatabase database = OpenDatabase();
            return SqliteOpeningTrainingStore.ListDueScheduledActions(database, playerKey, nowUtc, limit);
        }
    }

    public void MarkOpeningTrainingScheduledActionCompleted(string playerKey, string actionId, DateTime completedUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(playerKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(actionId);

        lock (sync)
        {
            using SqliteDatabase database = OpenDatabase();
            SqliteOpeningTrainingStore.MarkScheduledActionCompleted(database, playerKey, actionId, completedUtc);
        }
    }

    public void SaveOpeningTrainingTelemetryEvent(OpeningTrainingTelemetryEvent telemetryEvent)
    {
        ArgumentNullException.ThrowIfNull(telemetryEvent);

        lock (sync)
        {
            using SqliteDatabase database = OpenDatabase();
            SqliteOpeningTrainingStore.SaveTelemetryEvent(database, telemetryEvent);
        }
    }

    public IReadOnlyList<OpeningTrainingTelemetryEvent> ListOpeningTrainingTelemetryEvents(
        string? playerKey = null,
        DateTime? fromUtc = null,
        DateTime? toUtc = null,
        int limit = 500)
    {
        lock (sync)
        {
            using SqliteDatabase database = OpenDatabase();
            return SqliteOpeningTrainingStore.ListTelemetryEvents(database, playerKey, fromUtc, toUtc, limit);
        }
    }

    private void InitializeSchema()
    {
        lock (sync)
        {
            using SqliteDatabase database = OpenDatabase();
            SqliteSchemaInitializer.Initialize(database);
            if (applyDerivedAnalysisDataVersionPolicy)
            {
                ApplyDerivedAnalysisDataVersionPolicy(database);
            }
        }
    }

    private void ApplyDerivedAnalysisDataVersionPolicy(SqliteDatabase database)
    {
        string? storedVersion = SqliteAnalysisMetadataStore.GetMetadataValue(database, DerivedAnalysisDataVersionKey);
        if (string.Equals(storedVersion, derivedAnalysisDataVersion, StringComparison.Ordinal))
        {
            return;
        }

        SqliteTransaction.RunImmediate(database, () =>
        {
            SqliteAnalysisMetadataStore.ClearDerivedAnalysisData(database);
            SqliteAnalysisMetadataStore.SetMetadataValue(database, DerivedAnalysisDataVersionKey, derivedAnalysisDataVersion);
        });
    }

    private SqliteDatabase OpenDatabase() => new(databasePath);
}
