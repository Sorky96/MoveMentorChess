namespace MoveMentorChess.Persistence;

internal static class SqliteOpeningTheoryStore
{
    private const char CompositeKeySeparator = '|';
    private const int SqliteRow = SqliteResult.Row;

    public static bool TryGetOpeningPositionByKey(
        SqliteDatabase database,
        string positionKey,
        out OpeningTheoryPosition? position)
    {
        using SqliteStatement statement = database.Prepare("""
            SELECT
                opening_position_nodes.id,
                opening_position_nodes.position_key,
                opening_position_nodes.fen,
                opening_position_nodes.ply,
                opening_position_nodes.move_number,
                opening_position_nodes.side_to_move,
                opening_position_nodes.occurrence_count,
                opening_position_nodes.distinct_game_count,
                coalesce(opening_node_tags.eco, ''),
                coalesce(opening_node_tags.opening_name, ''),
                coalesce(opening_node_tags.variation_name, '')
            FROM opening_position_nodes
            LEFT JOIN opening_node_tags ON opening_node_tags.node_id = opening_position_nodes.id
            WHERE opening_position_nodes.position_key = ?1
            ORDER BY opening_node_tags.source_kind = 'pgn' DESC
            LIMIT 1;
            """);

        statement.BindText(1, positionKey);
        if (statement.Step() != SqliteRow)
        {
            position = null;
            return false;
        }

        position = ReadOpeningTheoryPosition(statement);
        return true;
    }

    public static IReadOnlyList<OpeningTheoryMove> GetOpeningMovesByPositionKey(
        SqliteDatabase database,
        string positionKey,
        int limit = 10,
        bool playableOnly = false)
    {
        int safeLimit = Math.Clamp(limit, 1, 100);
        List<OpeningTheoryMove> moves = new();

        using SqliteStatement statement = database.Prepare($"""
            SELECT
                opening_move_edges.id,
                opening_move_edges.from_node_id,
                opening_move_edges.to_node_id,
                opening_move_edges.move_uci,
                opening_move_edges.move_san,
                opening_move_edges.occurrence_count,
                opening_move_edges.distinct_game_count,
                opening_move_edges.is_main_move,
                opening_move_edges.is_playable_move,
                opening_move_edges.rank_within_position,
                to_nodes.position_key,
                to_nodes.fen,
                coalesce(opening_node_tags.eco, ''),
                coalesce(opening_node_tags.opening_name, ''),
                coalesce(opening_node_tags.variation_name, '')
            FROM opening_move_edges
            INNER JOIN opening_position_nodes AS from_nodes
                ON from_nodes.id = opening_move_edges.from_node_id
            INNER JOIN opening_position_nodes AS to_nodes
                ON to_nodes.id = opening_move_edges.to_node_id
            LEFT JOIN opening_node_tags
                ON opening_node_tags.node_id = to_nodes.id
            WHERE from_nodes.position_key = ?1
              {(playableOnly ? "AND opening_move_edges.is_playable_move = 1" : string.Empty)}
            ORDER BY
                opening_move_edges.rank_within_position = 0 ASC,
                opening_move_edges.rank_within_position ASC,
                opening_move_edges.occurrence_count DESC,
                opening_move_edges.move_san ASC
            LIMIT {safeLimit};
            """);

        statement.BindText(1, positionKey);
        while (statement.Step() == SqliteRow)
        {
            moves.Add(ReadOpeningTheoryMove(statement));
        }

        return moves;
    }

    public static IReadOnlyList<OpeningLineCatalogItem> ListOpeningLines(
        SqliteDatabase database,
        string? filterText = null,
        RepertoireSide? repertoireSide = null,
        int limit = 100)
    {
        int safeLimit = Math.Clamp(limit, 1, 500);
        List<OpeningLineCatalogItem> items = [];

        using SqliteStatement statement = database.Prepare($"""
            SELECT
                coalesce(tags.eco, ''),
                coalesce(tags.opening_name, ''),
                coalesce(tags.variation_name, ''),
                nodes.position_key,
                nodes.fen,
                nodes.side_to_move,
                nodes.distinct_game_count,
                (
                    SELECT COUNT(*)
                    FROM opening_move_edges edges
                    WHERE edges.from_node_id = nodes.id
                ) AS branch_count
            FROM opening_position_nodes nodes
            INNER JOIN opening_node_tags tags ON tags.node_id = nodes.id
            WHERE nodes.ply <= 12
            ORDER BY nodes.distinct_game_count DESC, tags.eco ASC, tags.opening_name ASC, tags.variation_name ASC
            LIMIT {safeLimit * 4};
            """);

        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);
        while (statement.Step() == SqliteRow)
        {
            string eco = statement.GetText(0) ?? string.Empty;
            string openingName = statement.GetText(1) ?? string.Empty;
            string variationName = statement.GetText(2) ?? string.Empty;
            OpeningPositionKey rootPositionKey = new(statement.GetText(3) ?? string.Empty);
            string fen = statement.GetText(4) ?? string.Empty;
            RepertoireSide side = ParseRepertoireSide(statement.GetText(5));
            int gameCount = statement.GetInt(6);
            int branchCount = statement.GetInt(7);

            if (repertoireSide.HasValue
                && repertoireSide.Value != RepertoireSide.Both
                && side != repertoireSide.Value)
            {
                continue;
            }

            string displayName = BuildDisplayName(eco, openingName, variationName);
            if (!string.IsNullOrWhiteSpace(filterText)
                && displayName.Contains(filterText, StringComparison.OrdinalIgnoreCase) == false
                && eco.Contains(filterText, StringComparison.OrdinalIgnoreCase) == false)
            {
                continue;
            }

            string dedupeKey = $"{eco}|{openingName}|{variationName}|{side}";
            if (!seen.Add(dedupeKey))
            {
                continue;
            }

            OpeningKey openingKey = new(BuildOpeningKey(eco, openingName));
            OpeningLineKey lineKey = new(BuildOpeningLineKey(eco, openingName, variationName, side, rootPositionKey.Value));
            items.Add(new OpeningLineCatalogItem(
                openingKey,
                lineKey,
                side,
                eco,
                openingName,
                variationName,
                displayName,
                rootPositionKey,
                fen,
                gameCount,
                branchCount));

            if (items.Count >= safeLimit)
            {
                break;
            }
        }

        return items;
    }

    public static IReadOnlyList<string> GetOpeningValidationMoves(SqliteDatabase database, OpeningPositionKey rootPositionKey)
    {
        List<string> pathMoves = BuildPathMovesToPosition(database, rootPositionKey);
        if (pathMoves.Count >= 4)
        {
            return pathMoves;
        }

        IReadOnlyList<string> continuationMoves = BuildPrimaryContinuationMoves(database, rootPositionKey, 4 - pathMoves.Count);
        return pathMoves.Concat(continuationMoves).ToList();
    }

    public static IReadOnlyList<OpeningLineMove> GetOpeningPathLineMoves(
        SqliteDatabase database,
        OpeningPositionKey rootPositionKey)
    {
        List<OpeningLineMove> reversedMoves = [];
        string? currentNodeId = LoadOpeningNodeId(database, rootPositionKey.Value);
        int guard = 0;

        while (!string.IsNullOrWhiteSpace(currentNodeId) && guard++ < 16)
        {
            using SqliteStatement statement = database.Prepare("""
                SELECT
                    edges.from_node_id,
                    edges.move_san,
                    edges.move_uci,
                    from_nodes.position_key,
                    to_nodes.position_key,
                    to_nodes.ply,
                    to_nodes.move_number,
                    to_nodes.side_to_move,
                    edges.is_main_move
                FROM opening_move_edges edges
                INNER JOIN opening_position_nodes from_nodes ON from_nodes.id = edges.from_node_id
                INNER JOIN opening_position_nodes to_nodes ON to_nodes.id = edges.to_node_id
                WHERE edges.to_node_id = ?1
                ORDER BY edges.occurrence_count DESC, edges.rank_within_position ASC, edges.move_san ASC
                LIMIT 1;
                """);

            statement.BindText(1, currentNodeId);
            if (statement.Step() != SqliteRow)
            {
                break;
            }

            currentNodeId = statement.GetText(0);
            string moveSan = statement.GetText(1) ?? string.Empty;
            string? moveUci = statement.GetText(2);
            OpeningPositionKey fromPositionKey = new(statement.GetText(3) ?? string.Empty);
            OpeningPositionKey toPositionKey = new(statement.GetText(4) ?? string.Empty);
            PlayerSide side = ParsePlayerSide(statement.GetText(7)) == PlayerSide.White ? PlayerSide.Black : PlayerSide.White;

            if (!string.IsNullOrWhiteSpace(moveSan))
            {
                reversedMoves.Add(new OpeningLineMove(
                    statement.GetInt(5),
                    statement.GetInt(6),
                    side,
                    moveSan,
                    moveUci,
                    fromPositionKey,
                    toPositionKey,
                    statement.GetInt(8) != 0));
            }

            if (fromPositionKey.Value == "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq -")
            {
                break;
            }
        }

        reversedMoves.Reverse();
        return reversedMoves;
    }

    private static List<string> BuildPathMovesToPosition(SqliteDatabase database, OpeningPositionKey rootPositionKey)
    {
        List<string> reversedMoves = [];
        string? currentNodeId = LoadOpeningNodeId(database, rootPositionKey.Value);
        int guard = 0;

        while (!string.IsNullOrWhiteSpace(currentNodeId) && guard++ < 16)
        {
            using SqliteStatement statement = database.Prepare("""
                SELECT
                    edges.from_node_id,
                    edges.move_san,
                    from_nodes.ply
                FROM opening_move_edges edges
                INNER JOIN opening_position_nodes from_nodes ON from_nodes.id = edges.from_node_id
                WHERE edges.to_node_id = ?1
                ORDER BY edges.occurrence_count DESC, edges.rank_within_position ASC, edges.move_san ASC
                LIMIT 1;
                """);

            statement.BindText(1, currentNodeId);
            if (statement.Step() != SqliteRow)
            {
                break;
            }

            currentNodeId = statement.GetText(0);
            string moveSan = statement.GetText(1) ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(moveSan))
            {
                reversedMoves.Add(moveSan);
            }

            if (statement.GetInt(2) == 0)
            {
                break;
            }
        }

        reversedMoves.Reverse();
        return reversedMoves;
    }

    private static IReadOnlyList<string> BuildPrimaryContinuationMoves(
        SqliteDatabase database,
        OpeningPositionKey rootPositionKey,
        int maxPly)
    {
        List<string> moves = [];
        string? currentNodeId = LoadOpeningNodeId(database, rootPositionKey.Value);

        for (int ply = 0; ply < maxPly && !string.IsNullOrWhiteSpace(currentNodeId); ply++)
        {
            using SqliteStatement statement = database.Prepare("""
                SELECT
                    edges.to_node_id,
                    edges.move_san
                FROM opening_move_edges edges
                WHERE edges.from_node_id = ?1
                ORDER BY edges.rank_within_position ASC, edges.occurrence_count DESC, edges.move_san ASC
                LIMIT 1;
                """);

            statement.BindText(1, currentNodeId);
            if (statement.Step() != SqliteRow)
            {
                break;
            }

            currentNodeId = statement.GetText(0);
            string moveSan = statement.GetText(1) ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(moveSan))
            {
                moves.Add(moveSan);
            }
        }

        return moves;
    }

    private static string? LoadOpeningNodeId(SqliteDatabase database, string positionKey)
    {
        using SqliteStatement statement = database.Prepare("""
            SELECT id
            FROM opening_position_nodes
            WHERE position_key = ?1
            LIMIT 1;
            """);

        statement.BindText(1, positionKey);
        return statement.Step() == SqliteRow ? statement.GetText(0) : null;
    }

    private static OpeningTheoryPosition ReadOpeningTheoryPosition(SqliteStatement statement)
    {
        return new OpeningTheoryPosition(
            ParseGuid(statement.GetText(0)),
            statement.GetText(1) ?? string.Empty,
            new OpeningPositionKey(statement.GetText(1) ?? string.Empty),
            statement.GetText(2) ?? string.Empty,
            statement.GetInt(3),
            statement.GetInt(4),
            statement.GetText(5) ?? string.Empty,
            statement.GetInt(6),
            statement.GetInt(7),
            new OpeningGameMetadata(
                statement.GetText(8) ?? string.Empty,
                statement.GetText(9) ?? string.Empty,
                statement.GetText(10) ?? string.Empty));
    }

    private static OpeningTheoryMove ReadOpeningTheoryMove(SqliteStatement statement)
    {
        string moveSan = statement.GetText(4) ?? string.Empty;
        bool isMainMove = statement.GetInt(7) != 0;
        return new OpeningTheoryMove(
            ParseGuid(statement.GetText(0)),
            ParseGuid(statement.GetText(1)),
            ParseGuid(statement.GetText(2)),
            statement.GetText(3) ?? string.Empty,
            moveSan,
            statement.GetInt(5),
            statement.GetInt(6),
            isMainMove,
            statement.GetInt(8) != 0,
            statement.GetInt(9),
            statement.GetText(10) ?? string.Empty,
            new OpeningPositionKey(statement.GetText(10) ?? string.Empty),
            statement.GetText(11) ?? string.Empty,
            new OpeningGameMetadata(
                statement.GetText(12) ?? string.Empty,
                statement.GetText(13) ?? string.Empty,
                statement.GetText(14) ?? string.Empty),
            "opening_book");
    }

    private static RepertoireSide ParseRepertoireSide(string? sideToMove)
    {
        return string.Equals(sideToMove, "Black", StringComparison.OrdinalIgnoreCase)
            || string.Equals(sideToMove, "b", StringComparison.OrdinalIgnoreCase)
            ? RepertoireSide.Black
            : RepertoireSide.White;
    }

    private static PlayerSide ParsePlayerSide(string? sideToMove)
    {
        return string.Equals(sideToMove, "Black", StringComparison.OrdinalIgnoreCase)
            || string.Equals(sideToMove, "b", StringComparison.OrdinalIgnoreCase)
            ? PlayerSide.Black
            : PlayerSide.White;
    }

    private static string BuildDisplayName(string eco, string openingName, string variationName)
    {
        string opening = string.IsNullOrWhiteSpace(openingName) ? OpeningCatalog.GetName(eco) : openingName;
        return string.IsNullOrWhiteSpace(variationName)
            ? $"{opening} ({eco})"
            : $"{opening}: {variationName} ({eco})";
    }

    private static string BuildOpeningKey(string eco, string openingName)
    {
        return $"{SanitizeKeyPart(eco)}{CompositeKeySeparator}{SanitizeKeyPart(openingName)}";
    }

    private static string BuildOpeningLineKey(string eco, string openingName, string variationName, RepertoireSide side, string positionKey)
    {
        return string.Join(
            CompositeKeySeparator,
            SanitizeKeyPart(eco),
            SanitizeKeyPart(openingName),
            SanitizeKeyPart(variationName),
            side.ToString(),
            SanitizeKeyPart(positionKey));
    }

    private static string SanitizeKeyPart(string? value)
    {
        return (value ?? string.Empty).Replace("\\", "\\\\", StringComparison.Ordinal).Replace("|", "\\|", StringComparison.Ordinal);
    }

    private static Guid ParseGuid(string? value)
    {
        return Guid.TryParse(value, out Guid parsed) ? parsed : Guid.Empty;
    }
}
