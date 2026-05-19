namespace MoveMentorChess.Persistence;

internal static class SqliteImportedGameStore
{
    private const int SqliteRow = SqliteResult.Row;

    public static void SaveImportedGames(SqliteDatabase database, IReadOnlyList<ImportedGame> games, DateTime timestampUtc)
    {
        string timestamp = timestampUtc.ToUniversalTime().ToString("O");
        using SqliteStatement statement = database.Prepare("""
            INSERT INTO imported_games (
                game_fingerprint,
                pgn_text,
                white_player,
                black_player,
                white_elo,
                black_elo,
                date_text,
                result_text,
                eco,
                site,
                round_text,
                current_position,
                timezone,
                eco_url,
                utc_date,
                utc_time,
                time_control,
                time_control_category,
                termination,
                start_time,
                end_date,
                end_time,
                link,
                updated_utc)
            VALUES (?1, ?2, ?3, ?4, ?5, ?6, ?7, ?8, ?9, ?10, ?11, ?12, ?13, ?14, ?15, ?16, ?17, ?18, ?19, ?20, ?21, ?22, ?23, ?24)
            ON CONFLICT (game_fingerprint)
            DO UPDATE SET
                pgn_text = excluded.pgn_text,
                white_player = excluded.white_player,
                black_player = excluded.black_player,
                white_elo = excluded.white_elo,
                black_elo = excluded.black_elo,
                date_text = excluded.date_text,
                result_text = excluded.result_text,
                eco = excluded.eco,
                site = excluded.site,
                round_text = excluded.round_text,
                current_position = excluded.current_position,
                timezone = excluded.timezone,
                eco_url = excluded.eco_url,
                utc_date = excluded.utc_date,
                utc_time = excluded.utc_time,
                time_control = excluded.time_control,
                time_control_category = excluded.time_control_category,
                termination = excluded.termination,
                start_time = excluded.start_time,
                end_date = excluded.end_date,
                end_time = excluded.end_time,
                link = excluded.link,
                updated_utc = excluded.updated_utc;
            """);

        foreach (ImportedGame game in games)
        {
            string gameFingerprint = GameFingerprint.Compute(game.PgnText);
            statement.Reset();
            statement.BindText(1, gameFingerprint);
            statement.BindText(2, game.PgnText);
            statement.BindNullableText(3, game.WhitePlayer);
            statement.BindNullableText(4, game.BlackPlayer);
            statement.BindNullableInt(5, game.WhiteElo);
            statement.BindNullableInt(6, game.BlackElo);
            statement.BindNullableText(7, game.DateText);
            statement.BindNullableText(8, game.Result);
            statement.BindNullableText(9, game.Eco);
            statement.BindNullableText(10, game.Site);
            statement.BindNullableText(11, game.Metadata?.Round);
            statement.BindNullableText(12, game.Metadata?.CurrentPosition);
            statement.BindNullableText(13, game.Metadata?.Timezone);
            statement.BindNullableText(14, game.Metadata?.EcoUrl);
            statement.BindNullableText(15, game.Metadata?.UtcDate);
            statement.BindNullableText(16, game.Metadata?.UtcTime);
            statement.BindNullableText(17, game.Metadata?.TimeControl);
            statement.BindInt(18, (int)(game.Metadata?.TimeControlCategory ?? GameTimeControlCategory.Unknown));
            statement.BindNullableText(19, game.Metadata?.Termination);
            statement.BindNullableText(20, game.Metadata?.StartTime);
            statement.BindNullableText(21, game.Metadata?.EndDate);
            statement.BindNullableText(22, game.Metadata?.EndTime);
            statement.BindNullableText(23, game.Metadata?.Link);
            statement.BindText(24, timestamp);
            statement.StepUntilDone();
        }
    }

    public static bool TryLoadImportedGame(SqliteDatabase database, string gameFingerprint, out ImportedGame? game)
    {
        using SqliteStatement statement = database.Prepare("""
            SELECT pgn_text
            FROM imported_games
            WHERE game_fingerprint = ?1
            LIMIT 1;
            """);

        statement.BindText(1, gameFingerprint);
        int stepResult = statement.Step();
        if (stepResult != SqliteRow)
        {
            game = null;
            return false;
        }

        string? pgnText = statement.GetText(0);
        if (string.IsNullOrWhiteSpace(pgnText))
        {
            game = null;
            return false;
        }

        game = PgnGameParser.Parse(pgnText);
        return true;
    }

    public static bool DeleteImportedGame(SqliteDatabase database, string gameFingerprint)
    {
        bool exists = database.Exists(
            """
            SELECT 1
            FROM imported_games
            WHERE game_fingerprint = ?1
            LIMIT 1;
            """,
            statement => statement.BindText(1, gameFingerprint));

        database.ExecuteNonQuery(
            """
            DELETE FROM analysis_moves
            WHERE game_fingerprint = ?1;
            """,
            statement => statement.BindText(1, gameFingerprint));
        database.ExecuteNonQuery(
            """
            DELETE FROM analysis_results
            WHERE game_fingerprint = ?1;
            """,
            statement => statement.BindText(1, gameFingerprint));
        database.ExecuteNonQuery(
            """
            DELETE FROM analysis_window_states
            WHERE game_fingerprint = ?1;
            """,
            statement => statement.BindText(1, gameFingerprint));
        database.ExecuteNonQuery(
            """
            DELETE FROM move_advice_feedbacks
            WHERE game_fingerprint = ?1;
            """,
            statement => statement.BindText(1, gameFingerprint));
        database.ExecuteNonQuery(
            """
            DELETE FROM imported_games
            WHERE game_fingerprint = ?1;
            """,
            statement => statement.BindText(1, gameFingerprint));

        return exists;
    }

    public static void ClearImportedAnalysisData(SqliteDatabase database)
    {
        database.ExecuteNonQuery("DELETE FROM move_advice_feedbacks;");
        database.ExecuteNonQuery("DELETE FROM analysis_window_states;");
        database.ExecuteNonQuery("DELETE FROM analysis_moves;");
        database.ExecuteNonQuery("DELETE FROM analysis_results;");
        database.ExecuteNonQuery("DELETE FROM imported_games;");
    }

    public static IReadOnlyList<SavedImportedGameSummary> ListImportedGames(
        SqliteDatabase database,
        string? filterText = null,
        int limit = 200)
    {
        string normalizedFilter = filterText?.Trim().ToLowerInvariant() ?? string.Empty;
        int safeLimit = Math.Clamp(limit, 1, 1000);
        List<SavedImportedGameSummary> items = new();

        using SqliteStatement statement = database.Prepare($"""
            SELECT game_fingerprint, white_player, black_player, date_text, result_text, eco, site,
                   white_elo, black_elo, time_control, time_control_category, updated_utc
            FROM imported_games
            {(string.IsNullOrWhiteSpace(normalizedFilter)
                ? string.Empty
                : "WHERE lower(coalesce(white_player, '')) LIKE ?1 OR lower(coalesce(black_player, '')) LIKE ?1 OR lower(coalesce(date_text, '')) LIKE ?1 OR lower(coalesce(result_text, '')) LIKE ?1 OR lower(coalesce(eco, '')) LIKE ?1 OR lower(coalesce(site, '')) LIKE ?1 OR lower(coalesce(time_control, '')) LIKE ?1")}
            ORDER BY updated_utc DESC
            LIMIT {safeLimit};
            """);

        if (!string.IsNullOrWhiteSpace(normalizedFilter))
        {
            statement.BindText(1, $"%{normalizedFilter}%");
        }

        while (statement.Step() == SqliteRow)
        {
            string fingerprint = statement.GetText(0) ?? string.Empty;
            string? white = statement.GetText(1);
            string? black = statement.GetText(2);
            string? dateText = statement.GetText(3);
            string? result = statement.GetText(4);
            string? eco = statement.GetText(5);
            string? site = statement.GetText(6);
            int? whiteElo = statement.GetNullableInt(7);
            int? blackElo = statement.GetNullableInt(8);
            string? timeControl = statement.GetText(9);
            GameTimeControlCategory category = ParseTimeControlCategory(statement.GetNullableInt(10), timeControl);
            string? updatedUtcText = statement.GetText(11);
            DateTime.TryParse(updatedUtcText, out DateTime updatedUtc);

            items.Add(new SavedImportedGameSummary(
                fingerprint,
                BuildDisplayTitle(white, black, dateText, result, eco),
                white,
                black,
                dateText,
                result,
                eco,
                site,
                whiteElo,
                blackElo,
                timeControl,
                category,
                updatedUtc));
        }

        return items;
    }

    private static GameTimeControlCategory ParseTimeControlCategory(int? storedValue, string? timeControl)
    {
        if (storedValue.HasValue
            && Enum.IsDefined(typeof(GameTimeControlCategory), storedValue.Value))
        {
            return (GameTimeControlCategory)storedValue.Value;
        }

        return PgnGameParser.ClassifyTimeControl(timeControl);
    }

    private static string BuildDisplayTitle(string? whitePlayer, string? blackPlayer, string? dateText, string? result, string? eco)
    {
        string players = $"{whitePlayer ?? "White"} vs {blackPlayer ?? "Black"}";
        string datePart = string.IsNullOrWhiteSpace(dateText) ? string.Empty : $" | {dateText}";
        string resultPart = string.IsNullOrWhiteSpace(result) ? string.Empty : $" | {result}";
        string ecoPart = string.IsNullOrWhiteSpace(eco) ? string.Empty : $" | {OpeningCatalog.Describe(eco)}";
        return players + datePart + resultPart + ecoPart;
    }
}
