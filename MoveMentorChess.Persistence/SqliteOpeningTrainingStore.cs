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
}
