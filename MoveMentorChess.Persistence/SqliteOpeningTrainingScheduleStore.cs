namespace MoveMentorChess.Persistence;

internal static class SqliteOpeningTrainingScheduleStore
{
    private const int SqliteRow = SqliteResult.Row;

    public static void SaveScheduledActions(
        SqliteDatabase database,
        string playerKey,
        IReadOnlyList<OpeningTrainingScheduledAction> actions)
    {
        string normalizedPlayerKey = SqliteOpeningTrainingDataConverters.NormalizePlayerKey(playerKey);
        SqliteTransaction.RunImmediate(database, () =>
        {
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
        });
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
