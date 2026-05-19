namespace MoveMentorChess.Persistence;

internal static class SqliteTransaction
{
    public static void RunImmediate(SqliteDatabase database, Action action)
    {
        ArgumentNullException.ThrowIfNull(action);

        database.ExecuteNonQuery("BEGIN IMMEDIATE;");
        try
        {
            action();
            database.ExecuteNonQuery("COMMIT;");
        }
        catch
        {
            database.ExecuteNonQuery("ROLLBACK;");
            throw;
        }
    }
}
