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
    private const int SqliteRow = SqliteResult.Row;
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
            database.ExecuteNonQuery("BEGIN IMMEDIATE;");
            try
            {
                SqliteOpeningTreeStore.SaveOpeningTree(database, tree);
                database.ExecuteNonQuery("COMMIT;");
            }
            catch
            {
                database.ExecuteNonQuery("ROLLBACK;");
                throw;
            }
        }
    }

    public void ReplaceOpeningTree(OpeningTreeBuildResult tree)
    {
        ArgumentNullException.ThrowIfNull(tree);

        lock (sync)
        {
            using SqliteDatabase database = OpenDatabase();
            database.ExecuteNonQuery("BEGIN IMMEDIATE;");
            try
            {
                SqliteOpeningTreeStore.ReplaceOpeningTree(database, tree);
                database.ExecuteNonQuery("COMMIT;");
            }
            catch
            {
                database.ExecuteNonQuery("ROLLBACK;");
                throw;
            }
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
            database.ExecuteNonQuery("BEGIN IMMEDIATE;");
            try
            {
                SqliteImportedGameStore.SaveImportedGames(database, [game], clock.UtcNow);
                database.ExecuteNonQuery("COMMIT;");
            }
            catch
            {
                database.ExecuteNonQuery("ROLLBACK;");
                throw;
            }
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
            database.ExecuteNonQuery("BEGIN IMMEDIATE;");
            try
            {
                SqliteImportedGameStore.SaveImportedGames(database, games, clock.UtcNow);
                database.ExecuteNonQuery("COMMIT;");
            }
            catch
            {
                database.ExecuteNonQuery("ROLLBACK;");
                throw;
            }
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
            database.ExecuteNonQuery("BEGIN IMMEDIATE;");
            try
            {
                SqliteImportedGameStore.ClearImportedAnalysisData(database);
                database.ExecuteNonQuery("COMMIT;");
            }
            catch
            {
                database.ExecuteNonQuery("ROLLBACK;");
                throw;
            }
        }
    }

    public void ClearDerivedAnalysisData()
    {
        lock (sync)
        {
            using SqliteDatabase database = OpenDatabase();
            database.ExecuteNonQuery("BEGIN IMMEDIATE;");
            try
            {
                SqliteAnalysisMetadataStore.ClearDerivedAnalysisData(database);
                SqliteAnalysisMetadataStore.SetMetadataValue(database, DerivedAnalysisDataVersionKey, derivedAnalysisDataVersion);
                database.ExecuteNonQuery("COMMIT;");
            }
            catch
            {
                database.ExecuteNonQuery("ROLLBACK;");
                throw;
            }
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
            database.ExecuteNonQuery("BEGIN IMMEDIATE;");
            try
            {
                SqliteOpeningTrainingStore.SaveReviewItems(database, playerKey, items);
                database.ExecuteNonQuery("COMMIT;");
            }
            catch
            {
                database.ExecuteNonQuery("ROLLBACK;");
                throw;
            }
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
            database.ExecuteNonQuery("BEGIN IMMEDIATE;");
            try
            {
                SqliteOpeningTrainingStore.SaveScheduledActions(database, playerKey, actions);
                database.ExecuteNonQuery("COMMIT;");
            }
            catch
            {
                database.ExecuteNonQuery("ROLLBACK;");
                throw;
            }
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
            database.ExecuteNonQuery("""
                CREATE TABLE IF NOT EXISTS imported_games (
                    game_fingerprint TEXT NOT NULL PRIMARY KEY,
                    pgn_text TEXT NOT NULL,
                    white_player TEXT NULL,
                    black_player TEXT NULL,
                    white_elo INTEGER NULL,
                    black_elo INTEGER NULL,
                    date_text TEXT NULL,
                    result_text TEXT NULL,
                    eco TEXT NULL,
                    site TEXT NULL,
                    round_text TEXT NULL,
                    current_position TEXT NULL,
                    timezone TEXT NULL,
                    eco_url TEXT NULL,
                    utc_date TEXT NULL,
                    utc_time TEXT NULL,
                    time_control TEXT NULL,
                    time_control_category INTEGER NOT NULL DEFAULT 0,
                    termination TEXT NULL,
                    start_time TEXT NULL,
                    end_date TEXT NULL,
                    end_time TEXT NULL,
                    link TEXT NULL,
                    updated_utc TEXT NOT NULL
                );
                """);
            database.ExecuteNonQuery("""
                CREATE TABLE IF NOT EXISTS analysis_results (
                    game_fingerprint TEXT NOT NULL,
                    analyzed_side INTEGER NOT NULL,
                    depth INTEGER NOT NULL,
                    multi_pv INTEGER NOT NULL,
                    move_time_ms INTEGER NOT NULL,
                    payload_json TEXT NOT NULL,
                    created_utc TEXT NOT NULL,
                    updated_utc TEXT NOT NULL,
                    PRIMARY KEY (game_fingerprint, analyzed_side, depth, multi_pv, move_time_ms)
                );
                """);
            database.ExecuteNonQuery("""
                CREATE TABLE IF NOT EXISTS analysis_moves (
                    game_fingerprint TEXT NOT NULL,
                    analyzed_side INTEGER NOT NULL,
                    depth INTEGER NOT NULL,
                    multi_pv INTEGER NOT NULL,
                    move_time_ms INTEGER NOT NULL,
                    ply INTEGER NOT NULL,
                    move_number INTEGER NOT NULL,
                    san TEXT NOT NULL,
                    move_uci TEXT NOT NULL,
                    fen_before TEXT NOT NULL,
                    fen_after TEXT NOT NULL,
                    phase INTEGER NOT NULL,
                    eval_before_cp INTEGER NULL,
                    eval_after_cp INTEGER NULL,
                    best_mate_in INTEGER NULL,
                    played_mate_in INTEGER NULL,
                    centipawn_loss INTEGER NULL,
                    quality INTEGER NOT NULL,
                    material_delta_cp INTEGER NOT NULL,
                    best_move_uci TEXT NULL,
                    mistake_label TEXT NULL,
                    mistake_confidence TEXT NULL,
                    evidence_json TEXT NULL,
                    short_explanation TEXT NULL,
                    detailed_explanation TEXT NULL,
                    training_hint TEXT NULL,
                    is_highlighted INTEGER NOT NULL DEFAULT 0,
                    PRIMARY KEY (game_fingerprint, analyzed_side, depth, multi_pv, move_time_ms, ply)
                );
                """);
            database.ExecuteNonQuery("""
                CREATE TABLE IF NOT EXISTS analysis_window_states (
                    game_fingerprint TEXT NOT NULL PRIMARY KEY,
                    selected_side INTEGER NOT NULL,
                    quality_filter_index INTEGER NOT NULL,
                    explanation_level_index INTEGER NOT NULL DEFAULT 1,
                    updated_utc TEXT NOT NULL
                );
                """);
            database.ExecuteNonQuery("""
                CREATE TABLE IF NOT EXISTS move_advice_feedbacks (
                    feedback_id TEXT NOT NULL PRIMARY KEY,
                    timestamp_utc TEXT NOT NULL,
                    game_fingerprint TEXT NOT NULL,
                    analyzed_side INTEGER NOT NULL,
                    depth INTEGER NOT NULL,
                    multi_pv INTEGER NOT NULL,
                    move_time_ms INTEGER NOT NULL,
                    ply INTEGER NOT NULL,
                    move_number INTEGER NOT NULL,
                    played_san TEXT NOT NULL,
                    played_uci TEXT NOT NULL,
                    fen_before TEXT NOT NULL,
                    fen_after TEXT NOT NULL,
                    eval_before_cp INTEGER NULL,
                    eval_after_cp INTEGER NULL,
                    best_move_uci TEXT NULL,
                    original_label TEXT NULL,
                    original_confidence TEXT NULL,
                    original_evidence_json TEXT NULL,
                    quality INTEGER NOT NULL,
                    centipawn_loss INTEGER NULL,
                    feedback_kind TEXT NOT NULL,
                    corrected_label TEXT NULL,
                    comment TEXT NULL,
                    source TEXT NOT NULL
                );
                """);
            database.ExecuteNonQuery("""
                CREATE INDEX IF NOT EXISTS idx_move_advice_feedbacks_move_latest
                ON move_advice_feedbacks (game_fingerprint, analyzed_side, depth, multi_pv, move_time_ms, ply, timestamp_utc DESC);
                """);
            database.ExecuteNonQuery("""
                CREATE TABLE IF NOT EXISTS app_metadata (
                    key TEXT NOT NULL PRIMARY KEY,
                    value TEXT NOT NULL
                );
                """);
            database.ExecuteNonQuery("""
                CREATE TABLE IF NOT EXISTS opening_training_session_results (
                    session_id TEXT NOT NULL PRIMARY KEY,
                    player_key TEXT NOT NULL,
                    display_name TEXT NOT NULL,
                    created_utc TEXT NOT NULL,
                    completed_utc TEXT NOT NULL,
                    outcome INTEGER NOT NULL,
                    position_count INTEGER NOT NULL,
                    attempt_count INTEGER NOT NULL,
                    correct_count INTEGER NOT NULL,
                    playable_count INTEGER NOT NULL,
                    wrong_count INTEGER NOT NULL,
                    related_openings_json TEXT NOT NULL,
                    theme_labels_json TEXT NOT NULL,
                    payload_json TEXT NOT NULL
                );
                """);
            database.ExecuteNonQuery("""
                CREATE INDEX IF NOT EXISTS idx_opening_training_session_results_player_completed
                ON opening_training_session_results (player_key, completed_utc DESC);
                """);
            database.ExecuteNonQuery("""
                CREATE TABLE IF NOT EXISTS opening_review_items (
                    player_key TEXT NOT NULL,
                    branch_key TEXT NOT NULL,
                    position_key TEXT NOT NULL,
                    last_reviewed_utc TEXT NULL,
                    next_review_utc TEXT NOT NULL,
                    ease TEXT NOT NULL,
                    correct_streak INTEGER NOT NULL,
                    wrong_streak INTEGER NOT NULL,
                    total_attempts INTEGER NOT NULL,
                    opening_key TEXT NULL,
                    opening_line_key TEXT NULL,
                    PRIMARY KEY (player_key, branch_key, position_key)
                );
                """);
            database.ExecuteNonQuery("""
                CREATE INDEX IF NOT EXISTS idx_opening_review_items_player_next_review
                ON opening_review_items (player_key, next_review_utc ASC);
                """);
            database.ExecuteNonQuery("""
                CREATE TABLE IF NOT EXISTS opening_training_scheduled_actions (
                    id TEXT NOT NULL PRIMARY KEY,
                    player_key TEXT NOT NULL,
                    session_id TEXT NOT NULL,
                    kind INTEGER NOT NULL,
                    line_key TEXT NULL,
                    branch_key TEXT NULL,
                    position_key TEXT NULL,
                    created_utc TEXT NOT NULL,
                    due_utc TEXT NOT NULL,
                    status INTEGER NOT NULL,
                    completed_utc TEXT NULL,
                    priority INTEGER NOT NULL,
                    source_action_id TEXT NULL
                );
                """);
            database.ExecuteNonQuery("""
                CREATE INDEX IF NOT EXISTS idx_opening_training_scheduled_actions_due
                ON opening_training_scheduled_actions (player_key, status, due_utc ASC, priority DESC);
                """);
            database.ExecuteNonQuery("""
                CREATE TABLE IF NOT EXISTS opening_training_telemetry_events (
                    event_id TEXT NOT NULL PRIMARY KEY,
                    event_name TEXT NOT NULL,
                    occurred_utc TEXT NOT NULL,
                    player_key TEXT NULL,
                    line_key TEXT NULL,
                    opening_key TEXT NULL,
                    session_id TEXT NULL,
                    recommendation_id TEXT NULL,
                    special_mode TEXT NULL,
                    properties_json TEXT NOT NULL
                );
                """);
            database.ExecuteNonQuery("""
                CREATE INDEX IF NOT EXISTS idx_opening_training_telemetry_events_player_date
                ON opening_training_telemetry_events (player_key, occurred_utc DESC);
                """);
            EnsureColumnExists(
                database,
                "analysis_window_states",
                "explanation_level_index",
                "INTEGER NOT NULL DEFAULT 1");
            EnsureColumnExists(database, "imported_games", "white_elo", "INTEGER NULL");
            EnsureColumnExists(database, "imported_games", "black_elo", "INTEGER NULL");
            EnsureColumnExists(database, "imported_games", "round_text", "TEXT NULL");
            EnsureColumnExists(database, "imported_games", "current_position", "TEXT NULL");
            EnsureColumnExists(database, "imported_games", "timezone", "TEXT NULL");
            EnsureColumnExists(database, "imported_games", "eco_url", "TEXT NULL");
            EnsureColumnExists(database, "imported_games", "utc_date", "TEXT NULL");
            EnsureColumnExists(database, "imported_games", "utc_time", "TEXT NULL");
            EnsureColumnExists(database, "imported_games", "time_control", "TEXT NULL");
            EnsureColumnExists(database, "imported_games", "time_control_category", "INTEGER NOT NULL DEFAULT 0");
            EnsureColumnExists(database, "imported_games", "termination", "TEXT NULL");
            EnsureColumnExists(database, "imported_games", "start_time", "TEXT NULL");
            EnsureColumnExists(database, "imported_games", "end_date", "TEXT NULL");
            EnsureColumnExists(database, "imported_games", "end_time", "TEXT NULL");
            EnsureColumnExists(database, "imported_games", "link", "TEXT NULL");
            EnsureColumnExists(database, "opening_review_items", "opening_key", "TEXT NULL");
            EnsureColumnExists(database, "opening_review_items", "opening_line_key", "TEXT NULL");
            EnsureOpeningTreeSchema(database);
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

        database.ExecuteNonQuery("BEGIN IMMEDIATE;");
        try
        {
            SqliteAnalysisMetadataStore.ClearDerivedAnalysisData(database);
            SqliteAnalysisMetadataStore.SetMetadataValue(database, DerivedAnalysisDataVersionKey, derivedAnalysisDataVersion);
            database.ExecuteNonQuery("COMMIT;");
        }
        catch
        {
            database.ExecuteNonQuery("ROLLBACK;");
            throw;
        }
    }

    private SqliteDatabase OpenDatabase() => new(databasePath);

    private static string NormalizePlayerKey(string? playerKey)
        => string.IsNullOrWhiteSpace(playerKey) ? string.Empty : playerKey.Trim().ToLowerInvariant();

    private static void EnsureOpeningTreeSchema(SqliteDatabase database)
    {
        database.ExecuteNonQuery("""
            CREATE TABLE IF NOT EXISTS opening_position_nodes (
                id TEXT NOT NULL PRIMARY KEY,
                position_key TEXT NOT NULL UNIQUE,
                fen TEXT NOT NULL,
                ply INTEGER NOT NULL,
                move_number INTEGER NOT NULL,
                side_to_move TEXT NOT NULL,
                occurrence_count INTEGER NOT NULL,
                distinct_game_count INTEGER NOT NULL
            );
            """);
        database.ExecuteNonQuery("""
            CREATE TABLE IF NOT EXISTS opening_move_edges (
                id TEXT NOT NULL PRIMARY KEY,
                from_node_id TEXT NOT NULL,
                to_node_id TEXT NOT NULL,
                move_uci TEXT NOT NULL,
                move_san TEXT NOT NULL,
                occurrence_count INTEGER NOT NULL,
                distinct_game_count INTEGER NOT NULL,
                is_main_move INTEGER NOT NULL,
                is_playable_move INTEGER NOT NULL,
                rank_within_position INTEGER NOT NULL,
                UNIQUE (from_node_id, move_uci, to_node_id)
            );
            """);
        database.ExecuteNonQuery("""
            CREATE INDEX IF NOT EXISTS idx_opening_position_nodes_position_key
            ON opening_position_nodes (position_key);
            """);
        database.ExecuteNonQuery("""
            CREATE INDEX IF NOT EXISTS idx_opening_move_edges_from_node_id
            ON opening_move_edges (from_node_id);
            """);
        database.ExecuteNonQuery("""
            CREATE INDEX IF NOT EXISTS idx_opening_move_edges_to_node_id
            ON opening_move_edges (to_node_id);
            """);
        database.ExecuteNonQuery("""
            CREATE TABLE IF NOT EXISTS opening_node_tags (
                id TEXT NOT NULL PRIMARY KEY,
                node_id TEXT NOT NULL,
                eco TEXT NOT NULL,
                opening_name TEXT NOT NULL,
                variation_name TEXT NOT NULL,
                source_kind TEXT NOT NULL,
                UNIQUE (node_id, eco, opening_name, variation_name, source_kind)
            );
            """);
        database.ExecuteNonQuery("""
            CREATE INDEX IF NOT EXISTS idx_opening_node_tags_node_id
            ON opening_node_tags (node_id);
            """);
    }

    private static Guid ParseGuid(string? value)
    {
        return Guid.TryParse(value, out Guid parsed) ? parsed : Guid.Empty;
    }

    private static void EnsureColumnExists(SqliteDatabase database, string tableName, string columnName, string definition)
    {
        using SqliteStatement statement = database.Prepare($"PRAGMA table_info({tableName});");
        while (statement.Step() == SqliteRow)
        {
            if (string.Equals(statement.GetText(1), columnName, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
        }

        database.ExecuteNonQuery($"ALTER TABLE {tableName} ADD COLUMN {columnName} {definition};");
    }

}
