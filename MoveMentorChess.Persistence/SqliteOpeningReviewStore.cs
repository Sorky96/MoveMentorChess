namespace MoveMentorChess.Persistence;

internal static class SqliteOpeningReviewStore
{
    private const int SqliteRow = SqliteResult.Row;

    public static void SaveReviewItems(SqliteDatabase database, string playerKey, IReadOnlyList<OpeningReviewItem> items)
    {
        string normalizedPlayerKey = SqliteOpeningTrainingDataConverters.NormalizePlayerKey(playerKey);
        SqliteTransaction.RunImmediate(database, () =>
        {
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
        });
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
}
