using System.Text.Json;

namespace MoveMentorChess.Persistence;

internal static class SqliteOpeningTrainingStore
{
    private const int SqliteRow = SqliteResult.Row;

    public static void SaveSessionResult(SqliteDatabase database, OpeningTrainingSessionResult result)
    {
        string payload = JsonSerializer.Serialize(result, SqliteOpeningTrainingDataConverters.JsonOptions);
        using SqliteStatement statement = database.Prepare("""
            INSERT INTO opening_training_session_results (
                session_id,
                player_key,
                display_name,
                created_utc,
                completed_utc,
                outcome,
                position_count,
                attempt_count,
                correct_count,
                playable_count,
                wrong_count,
                related_openings_json,
                theme_labels_json,
                payload_json)
            VALUES (?1, ?2, ?3, ?4, ?5, ?6, ?7, ?8, ?9, ?10, ?11, ?12, ?13, ?14)
            ON CONFLICT (session_id)
            DO UPDATE SET
                player_key = excluded.player_key,
                display_name = excluded.display_name,
                created_utc = excluded.created_utc,
                completed_utc = excluded.completed_utc,
                outcome = excluded.outcome,
                position_count = excluded.position_count,
                attempt_count = excluded.attempt_count,
                correct_count = excluded.correct_count,
                playable_count = excluded.playable_count,
                wrong_count = excluded.wrong_count,
                related_openings_json = excluded.related_openings_json,
                theme_labels_json = excluded.theme_labels_json,
                payload_json = excluded.payload_json;
            """);

        statement.BindText(1, result.SessionId);
        statement.BindText(2, SqliteOpeningTrainingDataConverters.NormalizePlayerKey(result.PlayerKey));
        statement.BindText(3, result.DisplayName);
        statement.BindText(4, SqliteOpeningTrainingDataConverters.FormatUtc(result.CreatedUtc));
        statement.BindText(5, SqliteOpeningTrainingDataConverters.FormatUtc(result.CompletedUtc));
        statement.BindInt(6, (int)result.Outcome);
        statement.BindInt(7, result.PositionCount);
        statement.BindInt(8, result.AttemptCount);
        statement.BindInt(9, result.CorrectCount);
        statement.BindInt(10, result.PlayableCount);
        statement.BindInt(11, result.WrongCount);
        statement.BindText(12, JsonSerializer.Serialize(result.RelatedOpenings, SqliteOpeningTrainingDataConverters.JsonOptions));
        statement.BindText(13, JsonSerializer.Serialize(result.ThemeLabels, SqliteOpeningTrainingDataConverters.JsonOptions));
        statement.BindText(14, payload);
        statement.StepUntilDone();
    }

    public static IReadOnlyList<OpeningTrainingSessionResult> ListSessionResults(
        SqliteDatabase database,
        string? playerKey = null,
        int limit = 200)
    {
        string normalizedPlayerKey = SqliteOpeningTrainingDataConverters.NormalizePlayerKey(playerKey);
        int safeLimit = Math.Clamp(limit, 1, 1000);
        List<OpeningTrainingSessionResult> results = [];

        using SqliteStatement statement = database.Prepare($"""
            SELECT payload_json
            FROM opening_training_session_results
            {(string.IsNullOrWhiteSpace(normalizedPlayerKey) ? string.Empty : "WHERE player_key = ?1")}
            ORDER BY completed_utc DESC
            LIMIT {safeLimit};
            """);

        if (!string.IsNullOrWhiteSpace(normalizedPlayerKey))
        {
            statement.BindText(1, normalizedPlayerKey);
        }

        while (statement.Step() == SqliteRow)
        {
            string? payload = statement.GetText(0);
            if (string.IsNullOrWhiteSpace(payload))
            {
                continue;
            }

            OpeningTrainingSessionResult? result = JsonSerializer.Deserialize<OpeningTrainingSessionResult>(
                payload,
                SqliteOpeningTrainingDataConverters.JsonOptions);
            if (result is not null)
            {
                results.Add(result);
            }
        }

        return results;
    }

    public static void SaveReviewItems(SqliteDatabase database, string playerKey, IReadOnlyList<OpeningReviewItem> items)
    {
        string normalizedPlayerKey = SqliteOpeningTrainingDataConverters.NormalizePlayerKey(playerKey);
        foreach (OpeningReviewItem item in items)
        {
            string branchKey = item.BranchKey.Value;
            string positionKey = item.PositionKey.Value;
            database.ExecuteNonQuery(
                """
                INSERT INTO opening_review_items (
                    player_key,
                    branch_key,
                    position_key,
                    last_reviewed_utc,
                    next_review_utc,
                    ease,
                    correct_streak,
                    wrong_streak,
                    total_attempts,
                    opening_key,
                    opening_line_key)
                VALUES (?1, ?2, ?3, ?4, ?5, ?6, ?7, ?8, ?9, ?10, ?11)
                ON CONFLICT (player_key, branch_key, position_key)
                DO UPDATE SET
                    opening_key = coalesce(excluded.opening_key, opening_review_items.opening_key),
                    opening_line_key = coalesce(excluded.opening_line_key, opening_review_items.opening_line_key),
                    last_reviewed_utc = CASE
                        WHEN excluded.last_reviewed_utc IS NULL THEN opening_review_items.last_reviewed_utc
                        WHEN opening_review_items.last_reviewed_utc IS NULL THEN excluded.last_reviewed_utc
                        WHEN excluded.last_reviewed_utc > opening_review_items.last_reviewed_utc THEN excluded.last_reviewed_utc
                        ELSE opening_review_items.last_reviewed_utc
                    END,
                    next_review_utc = excluded.next_review_utc,
                    ease = excluded.ease,
                    correct_streak = CASE
                        WHEN excluded.correct_streak > 0 THEN opening_review_items.correct_streak + excluded.correct_streak
                        ELSE 0
                    END,
                    wrong_streak = CASE
                        WHEN excluded.wrong_streak > 0 THEN opening_review_items.wrong_streak + excluded.wrong_streak
                        ELSE 0
                    END,
                    total_attempts = opening_review_items.total_attempts + excluded.total_attempts;
                """,
                statement =>
                {
                    statement.BindText(1, normalizedPlayerKey);
                    statement.BindText(2, branchKey);
                    statement.BindText(3, positionKey);
                    statement.BindNullableText(4, SqliteOpeningTrainingDataConverters.FormatNullableUtc(item.LastReviewedUtc));
                    statement.BindText(5, SqliteOpeningTrainingDataConverters.FormatUtc(item.NextReviewUtc));
                    statement.BindText(6, SqliteOpeningTrainingDataConverters.FormatDouble(item.Ease));
                    statement.BindInt(7, item.CorrectStreak);
                    statement.BindInt(8, item.WrongStreak);
                    statement.BindInt(9, item.TotalAttempts);
                    statement.BindNullableText(10, item.OpeningKey?.Value);
                    statement.BindNullableText(11, item.OpeningLineKey?.Value);
                });
        }
    }

    public static IReadOnlyList<OpeningReviewItem> ListReviewItems(
        SqliteDatabase database,
        string? playerKey = null,
        int limit = 1000)
    {
        string normalizedPlayerKey = SqliteOpeningTrainingDataConverters.NormalizePlayerKey(playerKey);
        int safeLimit = Math.Clamp(limit, 1, 5000);
        List<OpeningReviewItem> items = [];

        using SqliteStatement statement = database.Prepare($"""
            SELECT branch_key, position_key, last_reviewed_utc, next_review_utc, ease, correct_streak, wrong_streak, total_attempts, opening_key, opening_line_key
            FROM opening_review_items
            {(string.IsNullOrWhiteSpace(normalizedPlayerKey) ? string.Empty : "WHERE player_key = ?1")}
            ORDER BY next_review_utc ASC
            LIMIT {safeLimit};
            """);

        if (!string.IsNullOrWhiteSpace(normalizedPlayerKey))
        {
            statement.BindText(1, normalizedPlayerKey);
        }

        while (statement.Step() == SqliteRow)
        {
            items.Add(new OpeningReviewItem(
                new OpeningBranchKey(statement.GetText(0) ?? string.Empty),
                new OpeningPositionKey(statement.GetText(1) ?? string.Empty),
                SqliteOpeningTrainingDataConverters.ParseNullableUtc(statement.GetText(2)),
                SqliteOpeningTrainingDataConverters.ParseUtc(statement.GetText(3)),
                SqliteOpeningTrainingDataConverters.ParseDouble(statement.GetText(4)),
                statement.GetInt(5),
                statement.GetInt(6),
                statement.GetInt(7),
                string.IsNullOrWhiteSpace(statement.GetText(8)) ? null : new OpeningKey(statement.GetText(8)!),
                string.IsNullOrWhiteSpace(statement.GetText(9)) ? null : new OpeningLineKey(statement.GetText(9)!)));
        }

        return items;
    }

    public static void SaveScheduledActions(
        SqliteDatabase database,
        string playerKey,
        IReadOnlyList<OpeningTrainingScheduledAction> actions)
    {
        string normalizedPlayerKey = SqliteOpeningTrainingDataConverters.NormalizePlayerKey(playerKey);
        foreach (OpeningTrainingScheduledAction action in actions)
        {
            database.ExecuteNonQuery(
                """
                INSERT INTO opening_training_scheduled_actions (
                    id,
                    player_key,
                    session_id,
                    kind,
                    line_key,
                    branch_key,
                    position_key,
                    created_utc,
                    due_utc,
                    status,
                    completed_utc,
                    priority,
                    source_action_id)
                VALUES (?1, ?2, ?3, ?4, ?5, ?6, ?7, ?8, ?9, ?10, ?11, ?12, ?13)
                ON CONFLICT (id)
                DO UPDATE SET
                    player_key = excluded.player_key,
                    session_id = excluded.session_id,
                    kind = excluded.kind,
                    line_key = excluded.line_key,
                    branch_key = excluded.branch_key,
                    position_key = excluded.position_key,
                    created_utc = excluded.created_utc,
                    due_utc = excluded.due_utc,
                    status = excluded.status,
                    completed_utc = excluded.completed_utc,
                    priority = excluded.priority,
                    source_action_id = excluded.source_action_id;
                """,
                statement =>
                {
                    statement.BindText(1, action.Id);
                    statement.BindText(2, normalizedPlayerKey);
                    statement.BindText(3, action.SessionId);
                    statement.BindInt(4, (int)action.Kind);
                    statement.BindNullableText(5, action.LineKey?.Value);
                    statement.BindNullableText(6, action.BranchKey?.Value);
                    statement.BindNullableText(7, action.PositionKey?.Value);
                    statement.BindText(8, SqliteOpeningTrainingDataConverters.FormatUtc(action.CreatedUtc));
                    statement.BindText(9, SqliteOpeningTrainingDataConverters.FormatUtc(action.DueUtc));
                    statement.BindInt(10, (int)action.Status);
                    statement.BindNullableText(11, SqliteOpeningTrainingDataConverters.FormatNullableUtc(action.CompletedUtc));
                    statement.BindInt(12, action.Priority);
                    statement.BindNullableText(13, action.SourceActionId);
                });
        }
    }

    public static IReadOnlyList<OpeningTrainingScheduledAction> ListDueScheduledActions(
        SqliteDatabase database,
        string? playerKey,
        DateTime nowUtc,
        int limit = 50)
    {
        string normalizedPlayerKey = SqliteOpeningTrainingDataConverters.NormalizePlayerKey(playerKey);
        int safeLimit = Math.Clamp(limit, 1, 500);
        List<OpeningTrainingScheduledAction> actions = [];

        using SqliteStatement statement = database.Prepare($"""
            SELECT id, player_key, session_id, kind, line_key, branch_key, position_key, created_utc, due_utc, status, completed_utc, priority, source_action_id
            FROM opening_training_scheduled_actions
            WHERE status = ?1
              AND due_utc <= ?2
              {(string.IsNullOrWhiteSpace(normalizedPlayerKey) ? string.Empty : "AND player_key = ?3")}
            ORDER BY priority DESC, due_utc ASC
            LIMIT {safeLimit};
            """);

        statement.BindInt(1, (int)OpeningTrainingScheduledActionStatus.Pending);
        statement.BindText(2, SqliteOpeningTrainingDataConverters.FormatUtc(nowUtc));
        if (!string.IsNullOrWhiteSpace(normalizedPlayerKey))
        {
            statement.BindText(3, normalizedPlayerKey);
        }

        while (statement.Step() == SqliteRow)
        {
            actions.Add(ReadScheduledAction(statement));
        }

        return actions;
    }

    public static void MarkScheduledActionCompleted(
        SqliteDatabase database,
        string playerKey,
        string actionId,
        DateTime completedUtc)
    {
        database.ExecuteNonQuery(
            """
            UPDATE opening_training_scheduled_actions
            SET status = ?1,
                completed_utc = ?2
            WHERE player_key = ?3
              AND id = ?4;
            """,
            statement =>
            {
                statement.BindInt(1, (int)OpeningTrainingScheduledActionStatus.Completed);
                statement.BindText(2, SqliteOpeningTrainingDataConverters.FormatUtc(completedUtc));
                statement.BindText(3, SqliteOpeningTrainingDataConverters.NormalizePlayerKey(playerKey));
                statement.BindText(4, actionId);
            });
    }

    private static OpeningTrainingScheduledAction ReadScheduledAction(SqliteStatement statement)
    {
        string? lineKey = statement.GetText(4);
        string? branchKey = statement.GetText(5);
        string? positionKey = statement.GetText(6);
        return new OpeningTrainingScheduledAction(
            statement.GetText(0) ?? string.Empty,
            statement.GetText(1) ?? string.Empty,
            statement.GetText(2) ?? string.Empty,
            (TrainingNextActionKind)statement.GetInt(3),
            string.IsNullOrWhiteSpace(lineKey) ? null : new OpeningLineKey(lineKey),
            string.IsNullOrWhiteSpace(branchKey) ? null : new OpeningBranchKey(branchKey),
            string.IsNullOrWhiteSpace(positionKey) ? null : new OpeningPositionKey(positionKey),
            SqliteOpeningTrainingDataConverters.ParseUtc(statement.GetText(7)),
            SqliteOpeningTrainingDataConverters.ParseUtc(statement.GetText(8)),
            (OpeningTrainingScheduledActionStatus)statement.GetInt(9),
            SqliteOpeningTrainingDataConverters.ParseNullableUtc(statement.GetText(10)),
            statement.GetInt(11),
            statement.GetText(12));
    }
}
