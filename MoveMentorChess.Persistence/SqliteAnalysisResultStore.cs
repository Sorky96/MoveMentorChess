using System.Globalization;
using System.Text.Json;

namespace MoveMentorChess.Persistence;

internal static class SqliteAnalysisResultStore
{
    private const int SqliteRow = SqliteResult.Row;
    private const int NoMoveTimeMs = -1;

    private static readonly JsonSerializerOptions JsonOptions = new();

    public static IReadOnlyList<GameAnalysisResult> ListResults(
        SqliteDatabase database,
        string? filterText = null,
        int limit = 500)
    {
        string normalizedFilter = filterText?.Trim().ToLowerInvariant() ?? string.Empty;
        int safeLimit = Math.Clamp(limit, 1, 5000);
        List<GameAnalysisResult> items = [];

        using SqliteStatement statement = database.Prepare($"""
            SELECT
                analysis_results.game_fingerprint,
                analysis_results.analyzed_side,
                analysis_results.depth,
                analysis_results.multi_pv,
                analysis_results.move_time_ms,
                analysis_results.payload_json
            FROM analysis_results
            LEFT JOIN imported_games ON imported_games.game_fingerprint = analysis_results.game_fingerprint
            {(string.IsNullOrWhiteSpace(normalizedFilter)
                ? string.Empty
                : "WHERE lower(coalesce(imported_games.white_player, '')) LIKE ?1 OR lower(coalesce(imported_games.black_player, '')) LIKE ?1 OR lower(coalesce(imported_games.date_text, '')) LIKE ?1 OR lower(coalesce(imported_games.result_text, '')) LIKE ?1 OR lower(coalesce(imported_games.eco, '')) LIKE ?1 OR lower(coalesce(imported_games.site, '')) LIKE ?1")}
            ORDER BY analysis_results.updated_utc DESC
            LIMIT {safeLimit};
            """);

        if (!string.IsNullOrWhiteSpace(normalizedFilter))
        {
            statement.BindText(1, $"%{normalizedFilter}%");
        }

        while (statement.Step() == SqliteRow)
        {
            GameAnalysisCacheKey key = new(
                statement.GetText(0) ?? string.Empty,
                (PlayerSide)statement.GetInt(1),
                statement.GetInt(2),
                statement.GetInt(3),
                ReadMoveTime(statement.GetInt(4)));
            string? payload = statement.GetText(5);
            if (string.IsNullOrWhiteSpace(payload))
            {
                continue;
            }

            GameAnalysisResult? item = JsonSerializer.Deserialize<GameAnalysisResult>(payload, JsonOptions);
            if (item is not null)
            {
                items.Add(NormalizeLoadedResult(database, key, item));
            }
        }

        return items;
    }

    public static IReadOnlyList<StoredMoveAnalysis> ListMoveAnalyses(
        SqliteDatabase database,
        string? filterText = null,
        int limit = 5000)
    {
        string normalizedFilter = filterText?.Trim().ToLowerInvariant() ?? string.Empty;
        int safeLimit = Math.Clamp(limit, 1, 20000);
        List<StoredMoveAnalysis> items = [];

        using SqliteStatement statement = database.Prepare($"""
            SELECT
                analysis_moves.game_fingerprint,
                analysis_moves.analyzed_side,
                analysis_moves.depth,
                analysis_moves.multi_pv,
                analysis_moves.move_time_ms,
                analysis_results.updated_utc,
                imported_games.white_player,
                imported_games.black_player,
                imported_games.date_text,
                imported_games.result_text,
                imported_games.eco,
                imported_games.site,
                imported_games.white_elo,
                imported_games.black_elo,
                imported_games.time_control,
                imported_games.time_control_category,
                imported_games.utc_date,
                imported_games.utc_time,
                imported_games.end_date,
                imported_games.end_time,
                imported_games.termination,
                imported_games.link,
                analysis_moves.ply,
                analysis_moves.move_number,
                analysis_moves.san,
                analysis_moves.move_uci,
                analysis_moves.fen_before,
                analysis_moves.fen_after,
                analysis_moves.phase,
                analysis_moves.eval_before_cp,
                analysis_moves.eval_after_cp,
                analysis_moves.best_mate_in,
                analysis_moves.played_mate_in,
                analysis_moves.centipawn_loss,
                analysis_moves.quality,
                analysis_moves.material_delta_cp,
                analysis_moves.best_move_uci,
                analysis_moves.mistake_label,
                analysis_moves.mistake_confidence,
                analysis_moves.evidence_json,
                analysis_moves.short_explanation,
                analysis_moves.detailed_explanation,
                analysis_moves.training_hint,
                analysis_moves.is_highlighted,
                latest_feedback.feedback_kind,
                latest_feedback.corrected_label,
                latest_feedback.comment,
                latest_feedback.timestamp_utc
            FROM analysis_moves
            LEFT JOIN analysis_results ON analysis_results.game_fingerprint = analysis_moves.game_fingerprint
                AND analysis_results.analyzed_side = analysis_moves.analyzed_side
                AND analysis_results.depth = analysis_moves.depth
                AND analysis_results.multi_pv = analysis_moves.multi_pv
                AND analysis_results.move_time_ms = analysis_moves.move_time_ms
            LEFT JOIN imported_games ON imported_games.game_fingerprint = analysis_moves.game_fingerprint
            LEFT JOIN move_advice_feedbacks AS latest_feedback ON latest_feedback.feedback_id = (
                SELECT feedback_id
                FROM move_advice_feedbacks
                WHERE move_advice_feedbacks.game_fingerprint = analysis_moves.game_fingerprint
                  AND move_advice_feedbacks.analyzed_side = analysis_moves.analyzed_side
                  AND move_advice_feedbacks.depth = analysis_moves.depth
                  AND move_advice_feedbacks.multi_pv = analysis_moves.multi_pv
                  AND move_advice_feedbacks.move_time_ms = analysis_moves.move_time_ms
                  AND move_advice_feedbacks.ply = analysis_moves.ply
                ORDER BY timestamp_utc DESC, feedback_id DESC
                LIMIT 1
            )
            {(string.IsNullOrWhiteSpace(normalizedFilter)
                ? string.Empty
                : "WHERE lower(coalesce(imported_games.white_player, '')) LIKE ?1 OR lower(coalesce(imported_games.black_player, '')) LIKE ?1 OR lower(coalesce(imported_games.date_text, '')) LIKE ?1 OR lower(coalesce(imported_games.result_text, '')) LIKE ?1 OR lower(coalesce(imported_games.eco, '')) LIKE ?1 OR lower(coalesce(imported_games.site, '')) LIKE ?1 OR lower(coalesce(latest_feedback.corrected_label, analysis_moves.mistake_label, '')) LIKE ?1 OR lower(coalesce(analysis_moves.mistake_label, '')) LIKE ?1 OR lower(coalesce(analysis_moves.san, '')) LIKE ?1 OR lower(coalesce(analysis_moves.move_uci, '')) LIKE ?1")}
            ORDER BY analysis_results.updated_utc DESC, analysis_moves.ply ASC
            LIMIT {safeLimit};
            """);

        if (!string.IsNullOrWhiteSpace(normalizedFilter))
        {
            statement.BindText(1, $"%{normalizedFilter}%");
        }

        while (statement.Step() == SqliteRow)
        {
            string? timeControl = statement.GetText(14);
            string? originalLabel = statement.GetText(37);
            string? correctedLabel = statement.GetText(45);
            AdviceFeedbackKind? manualFeedbackKind = ParseNullableFeedbackKind(statement.GetText(44));
            DateTime? manualCorrectedUtc = ParseNullableUtc(statement.GetText(47));
            items.Add(StoredMoveAnalysisMapper.FromSqliteRow(
                new StoredGameContext(
                    statement.GetText(0) ?? string.Empty,
                    statement.GetText(6),
                    statement.GetText(7),
                    statement.GetText(8),
                    statement.GetText(9),
                    statement.GetText(10),
                    statement.GetText(11),
                    statement.GetNullableInt(12),
                    statement.GetNullableInt(13),
                    timeControl,
                    ParseTimeControlCategory(statement.GetNullableInt(15), timeControl),
                    statement.GetText(16),
                    statement.GetText(17),
                    statement.GetText(18),
                    statement.GetText(19),
                    statement.GetText(20),
                    statement.GetText(21)),
                new StoredAnalysisRunContext(
                    (PlayerSide)statement.GetInt(1),
                    statement.GetInt(2),
                    statement.GetInt(3),
                    ReadMoveTime(statement.GetInt(4)),
                    ParseUtc(statement.GetText(5))),
                new StoredMoveContext(
                    statement.GetInt(22),
                    statement.GetInt(23),
                    statement.GetText(24) ?? string.Empty,
                    statement.GetText(25) ?? string.Empty,
                    statement.GetText(26) ?? string.Empty,
                    statement.GetText(27) ?? string.Empty,
                    (GamePhase)statement.GetInt(28),
                    statement.GetNullableInt(29),
                    statement.GetNullableInt(30),
                    statement.GetNullableInt(31),
                    statement.GetNullableInt(32),
                    statement.GetNullableInt(33),
                    (MoveQualityBucket)statement.GetInt(34),
                    statement.GetInt(35),
                    statement.GetText(36)),
                new StoredMoveAdviceContext(
                    string.IsNullOrWhiteSpace(correctedLabel) ? originalLabel : correctedLabel,
                    ParseNullableDouble(statement.GetText(38)),
                    DeserializeEvidence(statement.GetText(39)),
                    statement.GetText(40),
                    statement.GetText(41),
                    statement.GetText(42),
                    statement.GetInt(43) != 0,
                    originalLabel),
                new StoredManualFeedbackContext(
                    manualFeedbackKind,
                    correctedLabel,
                    statement.GetText(46),
                    manualCorrectedUtc)));
        }

        return items;
    }

    public static bool TryLoadResult(
        SqliteDatabase database,
        GameAnalysisCacheKey key,
        out GameAnalysisResult? result)
    {
        using SqliteStatement statement = database.Prepare("""
            SELECT payload_json
            FROM analysis_results
            WHERE game_fingerprint = ?1
              AND analyzed_side = ?2
              AND depth = ?3
              AND multi_pv = ?4
              AND move_time_ms = ?5
            LIMIT 1;
            """);

        statement.BindText(1, key.GameFingerprint);
        statement.BindInt(2, (int)key.Side);
        statement.BindInt(3, key.Depth);
        statement.BindInt(4, key.MultiPv);
        statement.BindInt(5, NormalizeMoveTime(key.MoveTimeMs));

        int stepResult = statement.Step();
        if (stepResult != SqliteRow)
        {
            result = null;
            return false;
        }

        string? payload = statement.GetText(0);
        if (string.IsNullOrWhiteSpace(payload))
        {
            result = null;
            return false;
        }

        result = JsonSerializer.Deserialize<GameAnalysisResult>(payload, JsonOptions);
        if (result is not null)
        {
            result = NormalizeLoadedResult(database, key, result);
        }

        return result is not null;
    }

    public static void SaveResult(
        SqliteDatabase database,
        GameAnalysisCacheKey key,
        GameAnalysisResult result,
        DateTime timestampUtc)
    {
        string timestamp = timestampUtc.ToUniversalTime().ToString("O");
        string payload = JsonSerializer.Serialize(result, JsonOptions);
        database.ExecuteNonQuery("BEGIN IMMEDIATE;");
        try
        {
            using SqliteStatement statement = database.Prepare("""
                INSERT INTO analysis_results (
                    game_fingerprint,
                    analyzed_side,
                    depth,
                    multi_pv,
                    move_time_ms,
                    payload_json,
                    created_utc,
                    updated_utc)
                VALUES (?1, ?2, ?3, ?4, ?5, ?6, ?7, ?8)
                ON CONFLICT (game_fingerprint, analyzed_side, depth, multi_pv, move_time_ms)
                DO UPDATE SET
                    payload_json = excluded.payload_json,
                    updated_utc = excluded.updated_utc;
                """);

            statement.BindText(1, key.GameFingerprint);
            statement.BindInt(2, (int)key.Side);
            statement.BindInt(3, key.Depth);
            statement.BindInt(4, key.MultiPv);
            statement.BindInt(5, NormalizeMoveTime(key.MoveTimeMs));
            statement.BindText(6, payload);
            statement.BindText(7, timestamp);
            statement.BindText(8, timestamp);
            statement.StepUntilDone();

            ReplaceMoveAnalyses(database, key, result, ParseUtc(timestamp));
            database.ExecuteNonQuery("COMMIT;");
        }
        catch
        {
            database.ExecuteNonQuery("ROLLBACK;");
            throw;
        }
    }

    private static GameAnalysisResult NormalizeLoadedResult(
        SqliteDatabase database,
        GameAnalysisCacheKey key,
        GameAnalysisResult result)
    {
        IReadOnlyDictionary<int, StoredMoveAnnotation> annotations = LoadMoveAnnotations(database, key);
        Dictionary<int, MoveAnalysisResult> normalizedMoves = result.MoveAnalyses
            .Select(move => NormalizeMove(move, annotations))
            .ToDictionary(move => move.Replay.Ply);
        IReadOnlyList<MoveAnalysisResult> moveAnalyses = result.MoveAnalyses
            .Select(move => normalizedMoves[move.Replay.Ply])
            .ToList();
        IReadOnlyList<SelectedMistake> highlightedMistakes = result.HighlightedMistakes
            .Select(mistake => NormalizeMistake(mistake, normalizedMoves, annotations))
            .ToList();

        return result with
        {
            MoveAnalyses = moveAnalyses,
            HighlightedMistakes = highlightedMistakes
        };
    }

    private static MoveAnalysisResult NormalizeMove(
        MoveAnalysisResult move,
        IReadOnlyDictionary<int, StoredMoveAnnotation> annotations)
    {
        if (!annotations.TryGetValue(move.Replay.Ply, out StoredMoveAnnotation? annotation))
        {
            return move;
        }

        return move with
        {
            MistakeTag = annotation.Tag ?? move.MistakeTag,
            Explanation = annotation.Explanation ?? move.Explanation
        };
    }

    private static SelectedMistake NormalizeMistake(
        SelectedMistake mistake,
        IReadOnlyDictionary<int, MoveAnalysisResult> normalizedMoves,
        IReadOnlyDictionary<int, StoredMoveAnnotation> annotations)
    {
        IReadOnlyList<MoveAnalysisResult> moves = mistake.Moves
            .Select(move => normalizedMoves.TryGetValue(move.Replay.Ply, out MoveAnalysisResult? normalized)
                ? normalized
                : NormalizeMove(move, annotations))
            .ToList();
        MoveAnalysisResult? lead = moves
            .OrderByDescending(move => move.Quality)
            .ThenByDescending(move => move.CentipawnLoss ?? 0)
            .FirstOrDefault();

        return mistake with
        {
            Moves = moves,
            Tag = lead?.MistakeTag ?? mistake.Tag,
            Explanation = lead?.Explanation ?? mistake.Explanation
        };
    }

    private static IReadOnlyDictionary<int, StoredMoveAnnotation> LoadMoveAnnotations(
        SqliteDatabase database,
        GameAnalysisCacheKey key)
    {
        Dictionary<int, StoredMoveAnnotation> annotations = [];
        using SqliteStatement statement = database.Prepare("""
            SELECT
                analysis_moves.ply,
                coalesce(latest_feedback.corrected_label, analysis_moves.mistake_label),
                analysis_moves.mistake_confidence,
                analysis_moves.evidence_json,
                analysis_moves.short_explanation,
                analysis_moves.detailed_explanation,
                analysis_moves.training_hint
            FROM analysis_moves
            LEFT JOIN move_advice_feedbacks AS latest_feedback ON latest_feedback.feedback_id = (
                SELECT feedback_id
                FROM move_advice_feedbacks
                WHERE move_advice_feedbacks.game_fingerprint = analysis_moves.game_fingerprint
                  AND move_advice_feedbacks.analyzed_side = analysis_moves.analyzed_side
                  AND move_advice_feedbacks.depth = analysis_moves.depth
                  AND move_advice_feedbacks.multi_pv = analysis_moves.multi_pv
                  AND move_advice_feedbacks.move_time_ms = analysis_moves.move_time_ms
                  AND move_advice_feedbacks.ply = analysis_moves.ply
                ORDER BY timestamp_utc DESC, feedback_id DESC
                LIMIT 1
            )
            WHERE analysis_moves.game_fingerprint = ?1
              AND analysis_moves.analyzed_side = ?2
              AND analysis_moves.depth = ?3
              AND analysis_moves.multi_pv = ?4
              AND analysis_moves.move_time_ms = ?5;
            """);

        statement.BindText(1, key.GameFingerprint);
        statement.BindInt(2, (int)key.Side);
        statement.BindInt(3, key.Depth);
        statement.BindInt(4, key.MultiPv);
        statement.BindInt(5, NormalizeMoveTime(key.MoveTimeMs));

        while (statement.Step() == SqliteRow)
        {
            MistakeTag? tag = null;
            string? label = statement.GetText(1);
            if (!string.IsNullOrWhiteSpace(label))
            {
                tag = new MistakeTag(
                    label,
                    ParseNullableDouble(statement.GetText(2)) ?? 0,
                    DeserializeEvidence(statement.GetText(3)));
            }

            MoveExplanation? explanation = null;
            string? shortExplanation = statement.GetText(4);
            string? trainingHint = statement.GetText(6);
            if (!string.IsNullOrWhiteSpace(shortExplanation)
                || !string.IsNullOrWhiteSpace(trainingHint))
            {
                explanation = new MoveExplanation(
                    shortExplanation ?? string.Empty,
                    trainingHint ?? string.Empty,
                    statement.GetText(5) ?? string.Empty);
            }

            annotations[statement.GetInt(0)] = new StoredMoveAnnotation(tag, explanation);
        }

        return annotations;
    }

    private static int NormalizeMoveTime(int? moveTimeMs) => moveTimeMs ?? NoMoveTimeMs;

    private static int? ReadMoveTime(int rawMoveTime) => rawMoveTime == NoMoveTimeMs ? null : rawMoveTime;

    private static GameTimeControlCategory ParseTimeControlCategory(int? storedValue, string? timeControl)
    {
        if (storedValue.HasValue
            && Enum.IsDefined(typeof(GameTimeControlCategory), storedValue.Value))
        {
            return (GameTimeControlCategory)storedValue.Value;
        }

        return PgnGameParser.ClassifyTimeControl(timeControl);
    }

    private static DateTime ParseUtc(string? value)
    {
        return DateTime.TryParse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal,
            out DateTime parsed)
            ? parsed
            : DateTime.MinValue;
    }

    private static DateTime? ParseNullableUtc(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return ParseUtc(value);
    }

    private static AdviceFeedbackKind? ParseNullableFeedbackKind(string? value)
    {
        return Enum.TryParse(value, ignoreCase: true, out AdviceFeedbackKind parsed)
            ? parsed
            : null;
    }

    private static IReadOnlyList<string> DeserializeEvidence(string? payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<IReadOnlyList<string>>(payload, JsonOptions) ?? [];
        }
        catch
        {
            return [];
        }
    }

    private static double? ParseNullableDouble(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed)
            ? parsed
            : null;
    }

    private static string? FormatNullableDouble(double? value)
    {
        return value.HasValue
            ? value.Value.ToString("0.####", CultureInfo.InvariantCulture)
            : null;
    }

    private static string SerializeEvidence(IReadOnlyList<string>? evidence)
    {
        return JsonSerializer.Serialize(evidence ?? [], JsonOptions);
    }

    private static void ReplaceMoveAnalyses(
        SqliteDatabase database,
        GameAnalysisCacheKey key,
        GameAnalysisResult result,
        DateTime analysisUpdatedUtc)
    {
        database.ExecuteNonQuery(
            """
            DELETE FROM analysis_moves
            WHERE game_fingerprint = ?1
              AND analyzed_side = ?2
              AND depth = ?3
              AND multi_pv = ?4
              AND move_time_ms = ?5;
            """,
            statement =>
            {
                statement.BindText(1, key.GameFingerprint);
                statement.BindInt(2, (int)key.Side);
                statement.BindInt(3, key.Depth);
                statement.BindInt(4, key.MultiPv);
                statement.BindInt(5, NormalizeMoveTime(key.MoveTimeMs));
            });

        foreach (StoredMoveAnalysis move in StoredMoveAnalysisMapper.FromAnalysisResult(key, result, analysisUpdatedUtc))
        {
            database.ExecuteNonQuery(
                """
                INSERT INTO analysis_moves (
                    game_fingerprint,
                    analyzed_side,
                    depth,
                    multi_pv,
                    move_time_ms,
                    ply,
                    move_number,
                    san,
                    move_uci,
                    fen_before,
                    fen_after,
                    phase,
                    eval_before_cp,
                    eval_after_cp,
                    best_mate_in,
                    played_mate_in,
                    centipawn_loss,
                    quality,
                    material_delta_cp,
                    best_move_uci,
                    mistake_label,
                    mistake_confidence,
                    evidence_json,
                    short_explanation,
                    detailed_explanation,
                    training_hint,
                    is_highlighted)
                VALUES (?1, ?2, ?3, ?4, ?5, ?6, ?7, ?8, ?9, ?10, ?11, ?12, ?13, ?14, ?15, ?16, ?17, ?18, ?19, ?20, ?21, ?22, ?23, ?24, ?25, ?26, ?27);
                """,
                statement =>
                {
                    statement.BindText(1, move.GameFingerprint);
                    statement.BindInt(2, (int)move.AnalyzedSide);
                    statement.BindInt(3, move.Depth);
                    statement.BindInt(4, move.MultiPv);
                    statement.BindInt(5, NormalizeMoveTime(move.MoveTimeMs));
                    statement.BindInt(6, move.Ply);
                    statement.BindInt(7, move.MoveNumber);
                    statement.BindText(8, move.San);
                    statement.BindText(9, move.Uci);
                    statement.BindText(10, move.FenBefore);
                    statement.BindText(11, move.FenAfter);
                    statement.BindInt(12, (int)move.Phase);
                    statement.BindNullableInt(13, move.EvalBeforeCp);
                    statement.BindNullableInt(14, move.EvalAfterCp);
                    statement.BindNullableInt(15, move.BestMateIn);
                    statement.BindNullableInt(16, move.PlayedMateIn);
                    statement.BindNullableInt(17, move.CentipawnLoss);
                    statement.BindInt(18, (int)move.Quality);
                    statement.BindInt(19, move.MaterialDeltaCp);
                    statement.BindNullableText(20, move.BestMoveUci);
                    statement.BindNullableText(21, move.MistakeLabel);
                    statement.BindNullableText(22, FormatNullableDouble(move.MistakeConfidence));
                    statement.BindText(23, SerializeEvidence(move.Evidence));
                    statement.BindNullableText(24, move.ShortExplanation);
                    statement.BindNullableText(25, move.DetailedExplanation);
                    statement.BindNullableText(26, move.TrainingHint);
                    statement.BindInt(27, move.IsHighlighted ? 1 : 0);
                });
        }
    }

    private sealed record StoredMoveAnnotation(MistakeTag? Tag, MoveExplanation? Explanation);
}
