namespace MoveMentorChess.Persistence;

internal static class SqliteAnalysisMetadataStore
{
    private const int SqliteRow = SqliteResult.Row;

    public static void ClearDerivedAnalysisData(SqliteDatabase database)
    {
        database.ExecuteNonQuery("DELETE FROM analysis_moves;");
        database.ExecuteNonQuery("DELETE FROM analysis_results;");
    }

    public static string? GetMetadataValue(SqliteDatabase database, string key)
    {
        using SqliteStatement statement = database.Prepare("""
            SELECT value
            FROM app_metadata
            WHERE key = ?1
            LIMIT 1;
            """);

        statement.BindText(1, key);
        return statement.Step() == SqliteRow ? statement.GetText(0) : null;
    }

    public static void SetMetadataValue(SqliteDatabase database, string key, string value)
    {
        database.ExecuteNonQuery(
            """
            INSERT INTO app_metadata (key, value)
            VALUES (?1, ?2)
            ON CONFLICT (key)
            DO UPDATE SET value = excluded.value;
            """,
            statement =>
            {
                statement.BindText(1, key);
                statement.BindText(2, value);
            });
    }
}
