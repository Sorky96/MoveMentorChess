namespace MoveMentorChess.Persistence;

internal static class SqliteAnalysisWindowStateStore
{
    private const int SqliteRow = SqliteResult.Row;

    public static bool TryLoadWindowState(SqliteDatabase database, string gameFingerprint, out AnalysisWindowState? state)
    {
        using SqliteStatement statement = database.Prepare("""
            SELECT selected_side, quality_filter_index, explanation_level_index
            FROM analysis_window_states
            WHERE game_fingerprint = ?1
            LIMIT 1;
            """);

        statement.BindText(1, gameFingerprint);
        int stepResult = statement.Step();
        if (stepResult != SqliteRow)
        {
            state = null;
            return false;
        }

        state = new AnalysisWindowState(
            (PlayerSide)statement.GetInt(0),
            statement.GetInt(1),
            statement.GetInt(2));
        return true;
    }

    public static void SaveWindowState(
        SqliteDatabase database,
        string gameFingerprint,
        AnalysisWindowState state,
        DateTime timestampUtc)
    {
        using SqliteStatement statement = database.Prepare("""
            INSERT INTO analysis_window_states (
                game_fingerprint,
                selected_side,
                quality_filter_index,
                explanation_level_index,
                updated_utc)
            VALUES (?1, ?2, ?3, ?4, ?5)
            ON CONFLICT (game_fingerprint)
            DO UPDATE SET
                selected_side = excluded.selected_side,
                quality_filter_index = excluded.quality_filter_index,
                explanation_level_index = excluded.explanation_level_index,
                updated_utc = excluded.updated_utc;
            """);

        statement.BindText(1, gameFingerprint);
        statement.BindInt(2, (int)state.SelectedSide);
        statement.BindInt(3, state.QualityFilterIndex);
        statement.BindInt(4, state.ExplanationLevelIndex);
        statement.BindText(5, timestampUtc.ToUniversalTime().ToString("O"));
        statement.StepUntilDone();
    }
}
