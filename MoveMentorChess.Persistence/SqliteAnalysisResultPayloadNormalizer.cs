namespace MoveMentorChess.Persistence;

internal static class SqliteAnalysisResultPayloadNormalizer
{
    private const int SqliteRow = SqliteResult.Row;

    public static GameAnalysisResult NormalizeLoadedResult(
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
        statement.BindInt(5, SqliteAnalysisDataConverters.NormalizeMoveTime(key.MoveTimeMs));

        while (statement.Step() == SqliteRow)
        {
            MistakeTag? tag = null;
            string? label = statement.GetText(1);
            if (!string.IsNullOrWhiteSpace(label))
            {
                tag = new MistakeTag(
                    label,
                    SqliteAnalysisDataConverters.ParseNullableDouble(statement.GetText(2)) ?? 0,
                    SqliteAnalysisDataConverters.DeserializeEvidence(statement.GetText(3)));
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

    private sealed record StoredMoveAnnotation(MistakeTag? Tag, MoveExplanation? Explanation);
}
