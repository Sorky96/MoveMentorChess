using System.Text.Json;

namespace MoveMentorChess.Persistence;

internal static class SqliteOpeningTrainingTelemetryStore
{
    private const int SqliteRow = SqliteResult.Row;

    public static void SaveTelemetryEvent(SqliteDatabase database, OpeningTrainingTelemetryEvent telemetryEvent)
    {
        string eventId = BuildTelemetryEventId(telemetryEvent);
        database.ExecuteNonQuery(
            """
            INSERT INTO opening_training_telemetry_events (
                event_id,
                event_name,
                occurred_utc,
                player_key,
                line_key,
                opening_key,
                session_id,
                recommendation_id,
                special_mode,
                properties_json)
            VALUES (?1, ?2, ?3, ?4, ?5, ?6, ?7, ?8, ?9, ?10)
            ON CONFLICT (event_id)
            DO UPDATE SET
                event_name = excluded.event_name,
                occurred_utc = excluded.occurred_utc,
                player_key = excluded.player_key,
                line_key = excluded.line_key,
                opening_key = excluded.opening_key,
                session_id = excluded.session_id,
                recommendation_id = excluded.recommendation_id,
                special_mode = excluded.special_mode,
                properties_json = excluded.properties_json;
            """,
            statement =>
            {
                statement.BindText(1, eventId);
                statement.BindText(2, telemetryEvent.EventName);
                statement.BindText(3, SqliteOpeningTrainingDataConverters.FormatUtc(telemetryEvent.CreatedUtc));
                statement.BindNullableText(4, SqliteOpeningTrainingDataConverters.NormalizeNullablePlayerKey(telemetryEvent.PlayerKey));
                statement.BindNullableText(5, telemetryEvent.LineKey?.Value);
                statement.BindNullableText(6, telemetryEvent.OpeningKey?.Value);
                statement.BindNullableText(7, telemetryEvent.SessionId);
                statement.BindNullableText(8, telemetryEvent.RecommendationId);
                statement.BindNullableText(9, telemetryEvent.SpecialMode?.ToString());
                statement.BindText(10, JsonSerializer.Serialize(
                    telemetryEvent.Properties ?? new Dictionary<string, string>(),
                    SqliteOpeningTrainingDataConverters.JsonOptions));
            });
    }

    public static IReadOnlyList<OpeningTrainingTelemetryEvent> ListTelemetryEvents(
        SqliteDatabase database,
        string? playerKey = null,
        DateTime? fromUtc = null,
        DateTime? toUtc = null,
        int limit = 500)
    {
        string normalizedPlayerKey = SqliteOpeningTrainingDataConverters.NormalizeNullablePlayerKey(playerKey) ?? string.Empty;
        int safeLimit = Math.Clamp(limit, 1, 5000);
        List<OpeningTrainingTelemetryEvent> events = [];

        using SqliteStatement statement = database.Prepare($"""
            SELECT event_name, occurred_utc, player_key, line_key, opening_key, session_id, recommendation_id, special_mode, properties_json
            FROM opening_training_telemetry_events
            WHERE (?1 = '' OR player_key = ?1)
              AND (?2 IS NULL OR occurred_utc >= ?2)
              AND (?3 IS NULL OR occurred_utc <= ?3)
            ORDER BY occurred_utc DESC
            LIMIT {safeLimit};
            """);

        statement.BindText(1, normalizedPlayerKey);
        if (fromUtc.HasValue)
        {
            statement.BindText(2, SqliteOpeningTrainingDataConverters.FormatUtc(fromUtc.Value));
        }
        else
        {
            statement.BindNull(2);
        }

        if (toUtc.HasValue)
        {
            statement.BindText(3, SqliteOpeningTrainingDataConverters.FormatUtc(toUtc.Value));
        }
        else
        {
            statement.BindNull(3);
        }

        while (statement.Step() == SqliteRow)
        {
            events.Add(ReadTelemetryEvent(statement));
        }

        return events;
    }

    private static OpeningTrainingTelemetryEvent ReadTelemetryEvent(SqliteStatement statement)
    {
        string? lineKey = statement.GetText(3);
        string? openingKey = statement.GetText(4);
        string? specialModeText = statement.GetText(7);
        IReadOnlyDictionary<string, string> properties = DeserializeTelemetryProperties(statement.GetText(8));

        return new OpeningTrainingTelemetryEvent(
            statement.GetText(0) ?? string.Empty,
            SqliteOpeningTrainingDataConverters.ParseUtc(statement.GetText(1)),
            statement.GetText(2),
            string.IsNullOrWhiteSpace(lineKey) ? null : new OpeningLineKey(lineKey),
            string.IsNullOrWhiteSpace(openingKey) ? null : new OpeningKey(openingKey),
            statement.GetText(5),
            statement.GetText(6),
            Enum.TryParse(specialModeText, out SpecialTrainingModeKind specialMode) ? (SpecialTrainingModeKind?)specialMode : null,
            properties);
    }

    private static IReadOnlyDictionary<string, string> DeserializeTelemetryProperties(string? payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            return new Dictionary<string, string>();
        }

        return JsonSerializer.Deserialize<Dictionary<string, string>>(
                payload,
                SqliteOpeningTrainingDataConverters.JsonOptions)
            ?? new Dictionary<string, string>();
    }

    private static string BuildTelemetryEventId(OpeningTrainingTelemetryEvent telemetryEvent)
    {
        return Guid.NewGuid().ToString("N");
    }
}
