using System.Globalization;

namespace MoveMentorChess.Profiles;

internal sealed class PlayerProfileSnapshotLoader
{
    private readonly ProfileAnalysisDataSource analysisDataSource;

    public PlayerProfileSnapshotLoader(ProfileAnalysisDataSource analysisDataSource)
    {
        this.analysisDataSource = analysisDataSource ?? throw new ArgumentNullException(nameof(analysisDataSource));
    }

    public List<PlayerProfileSnapshot> LoadSnapshots(string? filterText, int limit)
    {
        ProfileAnalysisDataSet dataSet = analysisDataSource.Load(filterText, limit);

        List<PlayerProfileSnapshot> mergedSnapshots = BuildSnapshotsFromMoves(dataSet.StoredMoves);
        mergedSnapshots.AddRange(BuildSnapshotsFromResults(dataSet.Results));

        return mergedSnapshots
            .GroupBy(snapshot => new SnapshotSelectionKey(snapshot.GameFingerprint, snapshot.Side))
            .Select(group => group
                .OrderByDescending(snapshot => snapshot.AnalysisUpdatedUtc)
                .ThenByDescending(snapshot => snapshot.Depth)
                .ThenByDescending(snapshot => snapshot.MultiPv)
                .ThenByDescending(snapshot => snapshot.MoveTimeMs ?? -1)
                .First())
            .Take(limit)
            .ToList();
    }

    private static List<PlayerProfileSnapshot> BuildSnapshotsFromMoves(IReadOnlyList<StoredMoveAnalysis> storedMoves)
    {
        return storedMoves
            .GroupBy(move => new AnalysisVariantKey(
                move.Game.GameFingerprint,
                move.Analysis.AnalyzedSide,
                move.Analysis.Depth,
                move.Analysis.MultiPv,
                move.Analysis.MoveTimeMs))
            .Select(CreateSnapshotFromMoves)
            .Where(snapshot => snapshot is not null)
            .Select(snapshot => snapshot!)
            .ToList();
    }

    private static List<PlayerProfileSnapshot> BuildSnapshotsFromResults(IReadOnlyList<GameAnalysisResult> results)
    {
        return results
            .Select(CreateSnapshotFromResult)
            .Where(snapshot => snapshot is not null)
            .Select(snapshot => snapshot!)
            .ToList();
    }

    private static PlayerProfileSnapshot? CreateSnapshotFromMoves(IGrouping<AnalysisVariantKey, StoredMoveAnalysis> group)
    {
        List<StoredMoveAnalysis> moves = group
            .OrderBy(move => move.Move.Ply)
            .ToList();

        StoredMoveAnalysis first = moves[0];
        StoredGameContext game = first.Game;
        StoredAnalysisRunContext analysis = first.Analysis;
        string? playerName = analysis.AnalyzedSide == PlayerSide.White
            ? game.WhitePlayer
            : game.BlackPlayer;

        if (string.IsNullOrWhiteSpace(playerName))
        {
            return null;
        }

        return new PlayerProfileSnapshot(
            game.GameFingerprint,
            NormalizePlayerKey(playerName),
            playerName.Trim(),
            analysis.AnalyzedSide,
            ParseGameDate(game.DateText),
            ParseMonthKey(game.DateText),
            ParseQuarterKey(game.DateText),
            string.IsNullOrWhiteSpace(game.Eco) ? "Unknown" : game.Eco!,
            analysis.Depth,
            analysis.MultiPv,
            analysis.MoveTimeMs,
            analysis.AnalysisUpdatedUtc,
            analysis.AnalyzedSide == PlayerSide.White ? game.WhiteElo : game.BlackElo,
            analysis.AnalyzedSide == PlayerSide.White ? game.BlackElo : game.WhiteElo,
            game.Result,
            game.TimeControlCategory,
            game.TimeControl,
            game.UtcDate,
            game.UtcTime,
            game.EndDate,
            game.EndTime,
            game.Termination,
            game.Link,
            moves);
    }

    private static PlayerProfileSnapshot? CreateSnapshotFromResult(GameAnalysisResult result)
    {
        string? playerName = result.AnalyzedSide == PlayerSide.White
            ? result.Game.WhitePlayer
            : result.Game.BlackPlayer;

        if (string.IsNullOrWhiteSpace(playerName))
        {
            return null;
        }

        IReadOnlyList<StoredMoveAnalysis> moves = StoredMoveAnalysisMapper
            .FromAnalysisResult(
                new GameAnalysisCacheKey(GameFingerprint.Compute(result.Game.PgnText), result.AnalyzedSide, 0, 0, null),
                result,
                DateTime.MinValue)
            .ToList();

        return new PlayerProfileSnapshot(
            GameFingerprint.Compute(result.Game.PgnText),
            NormalizePlayerKey(playerName),
            playerName.Trim(),
            result.AnalyzedSide,
            ParseGameDate(result.Game.DateText),
            ParseMonthKey(result.Game.DateText),
            ParseQuarterKey(result.Game.DateText),
            string.IsNullOrWhiteSpace(result.Game.Eco) ? "Unknown" : result.Game.Eco!,
            0,
            0,
            null,
            DateTime.MinValue,
            result.AnalyzedSide == PlayerSide.White ? result.Game.WhiteElo : result.Game.BlackElo,
            result.AnalyzedSide == PlayerSide.White ? result.Game.BlackElo : result.Game.WhiteElo,
            result.Game.Result,
            result.Game.Metadata?.TimeControlCategory ?? GameTimeControlCategory.Unknown,
            result.Game.Metadata?.TimeControl,
            result.Game.Metadata?.UtcDate,
            result.Game.Metadata?.UtcTime,
            result.Game.Metadata?.EndDate,
            result.Game.Metadata?.EndTime,
            result.Game.Metadata?.Termination,
            result.Game.Metadata?.Link,
            moves);
    }

    public static string NormalizePlayerKey(string playerName)
    {
        return playerName.Trim().ToLowerInvariant();
    }

    public static DateTime? ParseGameDate(string? dateText)
    {
        if (string.IsNullOrWhiteSpace(dateText))
        {
            return null;
        }

        string[] formats =
        [
            "yyyy.MM.dd",
            "yyyy-MM-dd",
            "yyyy/MM/dd",
            "yyyy.MM",
            "yyyy-MM",
            "yyyy/MM"
        ];

        return DateTime.TryParseExact(dateText, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsed)
            ? parsed
            : null;
    }

    public static string? ParseMonthKey(string? dateText)
    {
        DateTime? parsed = ParseGameDate(dateText);
        return parsed?.ToString("yyyy-MM", CultureInfo.InvariantCulture);
    }

    public static string? ParseQuarterKey(string? dateText)
    {
        DateTime? parsed = ParseGameDate(dateText);
        if (parsed.HasValue)
        {
            int quarter = ((parsed.Value.Month - 1) / 3) + 1;
            return $"{parsed.Value:yyyy}-Q{quarter}";
        }

        return null;
    }
}
