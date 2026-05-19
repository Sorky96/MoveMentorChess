namespace MoveMentorChess.Persistence;

internal static class SqliteOpeningTreeStore
{
    private const int SqliteRow = SqliteResult.Row;
    private const string OpeningSeedVersionKey = "opening_tree_seed_version";

    public static void SaveOpeningTree(SqliteDatabase database, OpeningTreeBuildResult tree)
    {
        Dictionary<Guid, string> persistedNodeIds = new();

        foreach (OpeningPositionNode node in tree.Nodes)
        {
            string nodeId = LoadOpeningNodeId(database, node.PositionKey) ?? node.Id.ToString("D");
            UpsertOpeningNode(database, node, nodeId);
            persistedNodeIds[node.Id] = nodeId;
        }

        foreach (OpeningMoveEdge edge in tree.Edges)
        {
            if (!persistedNodeIds.TryGetValue(edge.FromNodeId, out string? fromNodeId)
                || !persistedNodeIds.TryGetValue(edge.ToNodeId, out string? toNodeId))
            {
                throw new InvalidOperationException("Opening edge references a node that was not saved.");
            }

            string edgeId = LoadOpeningEdgeId(database, fromNodeId, edge.MoveUci, toNodeId)
                ?? edge.Id.ToString("D");
            UpsertOpeningEdge(database, edge, edgeId, fromNodeId, toNodeId);
        }

        foreach (string persistedNodeId in persistedNodeIds.Values)
        {
            DeleteOpeningNodeTags(database, persistedNodeId);
        }

        foreach (OpeningNodeTag tag in tree.Tags)
        {
            if (!persistedNodeIds.TryGetValue(tag.NodeId, out string? nodeId))
            {
                throw new InvalidOperationException("Opening tag references a node that was not saved.");
            }

            UpsertOpeningNodeTag(database, tag, nodeId);
        }
    }

    public static void ReplaceOpeningTree(SqliteDatabase database, OpeningTreeBuildResult tree)
    {
        database.ExecuteNonQuery("DELETE FROM opening_node_tags;");
        database.ExecuteNonQuery("DELETE FROM opening_move_edges;");
        database.ExecuteNonQuery("DELETE FROM opening_position_nodes;");
        SaveOpeningTree(database, tree);
    }

    public static OpeningTreeBuildResult LoadOpeningTree(SqliteDatabase database)
    {
        Dictionary<string, Guid> nodeIdMap = new(StringComparer.OrdinalIgnoreCase);
        List<OpeningPositionNode> nodes = new();
        using (SqliteStatement statement = database.Prepare("""
            SELECT id, position_key, fen, ply, move_number, side_to_move, occurrence_count, distinct_game_count
            FROM opening_position_nodes
            ORDER BY ply ASC, position_key ASC;
            """))
        {
            while (statement.Step() == SqliteRow)
            {
                OpeningPositionNode node = new()
                {
                    Id = ParseGuid(statement.GetText(0)),
                    PositionKey = statement.GetText(1) ?? string.Empty,
                    Fen = statement.GetText(2) ?? string.Empty,
                    Ply = statement.GetInt(3),
                    MoveNumber = statement.GetInt(4),
                    SideToMove = statement.GetText(5) ?? string.Empty,
                    OccurrenceCount = statement.GetInt(6),
                    DistinctGameCount = statement.GetInt(7)
                };
                nodes.Add(node);
                nodeIdMap[statement.GetText(0) ?? string.Empty] = node.Id;
            }
        }

        List<OpeningMoveEdge> edges = new();
        using (SqliteStatement statement = database.Prepare("""
            SELECT id, from_node_id, to_node_id, move_uci, move_san, occurrence_count, distinct_game_count, is_main_move, is_playable_move, rank_within_position
            FROM opening_move_edges
            ORDER BY rank_within_position ASC, occurrence_count DESC, move_san ASC;
            """))
        {
            while (statement.Step() == SqliteRow)
            {
                string fromNodeId = statement.GetText(1) ?? string.Empty;
                string toNodeId = statement.GetText(2) ?? string.Empty;
                edges.Add(new OpeningMoveEdge
                {
                    Id = ParseGuid(statement.GetText(0)),
                    FromNodeId = nodeIdMap.TryGetValue(fromNodeId, out Guid fromGuid) ? fromGuid : Guid.Empty,
                    ToNodeId = nodeIdMap.TryGetValue(toNodeId, out Guid toGuid) ? toGuid : Guid.Empty,
                    MoveUci = statement.GetText(3) ?? string.Empty,
                    MoveSan = statement.GetText(4) ?? string.Empty,
                    OccurrenceCount = statement.GetInt(5),
                    DistinctGameCount = statement.GetInt(6),
                    IsMainMove = statement.GetInt(7) != 0,
                    IsPlayableMove = statement.GetInt(8) != 0,
                    RankWithinPosition = statement.GetInt(9)
                });
            }
        }

        List<OpeningNodeTag> tags = new();
        using (SqliteStatement statement = database.Prepare("""
            SELECT id, node_id, eco, opening_name, variation_name, source_kind
            FROM opening_node_tags
            ORDER BY node_id ASC, eco ASC, opening_name ASC, variation_name ASC;
            """))
        {
            while (statement.Step() == SqliteRow)
            {
                string nodeId = statement.GetText(1) ?? string.Empty;
                tags.Add(new OpeningNodeTag
                {
                    Id = ParseGuid(statement.GetText(0)),
                    NodeId = nodeIdMap.TryGetValue(nodeId, out Guid nodeGuid) ? nodeGuid : Guid.Empty,
                    Eco = statement.GetText(2) ?? string.Empty,
                    OpeningName = statement.GetText(3) ?? string.Empty,
                    VariationName = statement.GetText(4) ?? string.Empty,
                    SourceKind = statement.GetText(5) ?? string.Empty
                });
            }
        }

        return new OpeningTreeBuildResult(nodes, edges, tags);
    }

    public static string? GetOpeningSeedVersion(SqliteDatabase database)
    {
        using SqliteStatement statement = database.Prepare("""
            SELECT value
            FROM app_metadata
            WHERE key = ?1
            LIMIT 1;
            """);

        statement.BindText(1, OpeningSeedVersionKey);
        return statement.Step() == SqliteRow ? statement.GetText(0) : null;
    }

    public static void SetOpeningSeedVersion(SqliteDatabase database, string version)
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
                statement.BindText(1, OpeningSeedVersionKey);
                statement.BindText(2, version);
            });
    }

    public static OpeningTreeStoreSummary GetOpeningTreeSummary(SqliteDatabase database)
    {
        return new OpeningTreeStoreSummary(
            CountRows(database, "opening_position_nodes"),
            CountRows(database, "opening_move_edges"),
            CountRows(database, "opening_node_tags"));
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

    private static string? LoadOpeningEdgeId(SqliteDatabase database, string fromNodeId, string moveUci, string toNodeId)
    {
        using SqliteStatement statement = database.Prepare("""
            SELECT id
            FROM opening_move_edges
            WHERE from_node_id = ?1
              AND move_uci = ?2
              AND to_node_id = ?3
            LIMIT 1;
            """);

        statement.BindText(1, fromNodeId);
        statement.BindText(2, moveUci);
        statement.BindText(3, toNodeId);
        return statement.Step() == SqliteRow ? statement.GetText(0) : null;
    }

    private static void UpsertOpeningNode(SqliteDatabase database, OpeningPositionNode node, string nodeId)
    {
        database.ExecuteNonQuery(
            """
            INSERT INTO opening_position_nodes (
                id,
                position_key,
                fen,
                ply,
                move_number,
                side_to_move,
                occurrence_count,
                distinct_game_count)
            VALUES (?1, ?2, ?3, ?4, ?5, ?6, ?7, ?8)
            ON CONFLICT (position_key)
            DO UPDATE SET
                fen = excluded.fen,
                ply = excluded.ply,
                move_number = excluded.move_number,
                side_to_move = excluded.side_to_move,
                occurrence_count = excluded.occurrence_count,
                distinct_game_count = excluded.distinct_game_count;
            """,
            statement =>
            {
                statement.BindText(1, nodeId);
                statement.BindText(2, node.PositionKey);
                statement.BindText(3, node.Fen);
                statement.BindInt(4, node.Ply);
                statement.BindInt(5, node.MoveNumber);
                statement.BindText(6, node.SideToMove);
                statement.BindInt(7, node.OccurrenceCount);
                statement.BindInt(8, node.DistinctGameCount);
            });
    }

    private static void UpsertOpeningEdge(
        SqliteDatabase database,
        OpeningMoveEdge edge,
        string edgeId,
        string fromNodeId,
        string toNodeId)
    {
        database.ExecuteNonQuery(
            """
            INSERT INTO opening_move_edges (
                id,
                from_node_id,
                to_node_id,
                move_uci,
                move_san,
                occurrence_count,
                distinct_game_count,
                is_main_move,
                is_playable_move,
                rank_within_position)
            VALUES (?1, ?2, ?3, ?4, ?5, ?6, ?7, ?8, ?9, ?10)
            ON CONFLICT (from_node_id, move_uci, to_node_id)
            DO UPDATE SET
                move_san = excluded.move_san,
                occurrence_count = excluded.occurrence_count,
                distinct_game_count = excluded.distinct_game_count,
                is_main_move = excluded.is_main_move,
                is_playable_move = excluded.is_playable_move,
                rank_within_position = excluded.rank_within_position;
            """,
            statement =>
            {
                statement.BindText(1, edgeId);
                statement.BindText(2, fromNodeId);
                statement.BindText(3, toNodeId);
                statement.BindText(4, edge.MoveUci);
                statement.BindText(5, edge.MoveSan);
                statement.BindInt(6, edge.OccurrenceCount);
                statement.BindInt(7, edge.DistinctGameCount);
                statement.BindInt(8, edge.IsMainMove ? 1 : 0);
                statement.BindInt(9, edge.IsPlayableMove ? 1 : 0);
                statement.BindInt(10, edge.RankWithinPosition);
            });
    }

    private static void DeleteOpeningNodeTags(SqliteDatabase database, string nodeId)
    {
        database.ExecuteNonQuery(
            """
            DELETE FROM opening_node_tags
            WHERE node_id = ?1;
            """,
            statement => statement.BindText(1, nodeId));
    }

    private static void UpsertOpeningNodeTag(SqliteDatabase database, OpeningNodeTag tag, string nodeId)
    {
        database.ExecuteNonQuery(
            """
            INSERT INTO opening_node_tags (
                id,
                node_id,
                eco,
                opening_name,
                variation_name,
                source_kind)
            VALUES (?1, ?2, ?3, ?4, ?5, ?6)
            ON CONFLICT (node_id, eco, opening_name, variation_name, source_kind)
            DO UPDATE SET
                eco = excluded.eco,
                opening_name = excluded.opening_name,
                variation_name = excluded.variation_name,
                source_kind = excluded.source_kind;
            """,
            statement =>
            {
                statement.BindText(1, tag.Id.ToString("D"));
                statement.BindText(2, nodeId);
                statement.BindText(3, tag.Eco);
                statement.BindText(4, tag.OpeningName);
                statement.BindText(5, tag.VariationName);
                statement.BindText(6, tag.SourceKind);
            });
    }

    private static int CountRows(SqliteDatabase database, string tableName)
    {
        using SqliteStatement statement = database.Prepare($"SELECT COUNT(*) FROM {tableName};");
        return statement.Step() == SqliteRow ? statement.GetInt(0) : 0;
    }

    private static Guid ParseGuid(string? value)
    {
        return Guid.TryParse(value, out Guid parsed) ? parsed : Guid.Empty;
    }
}
