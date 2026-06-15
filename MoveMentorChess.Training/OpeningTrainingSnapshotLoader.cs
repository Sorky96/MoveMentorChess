namespace MoveMentorChess.Training;

internal sealed class OpeningTrainingSnapshotLoader
{
    private readonly TrainingAnalysisDataSource analysisDataSource;

    public OpeningTrainingSnapshotLoader(TrainingAnalysisDataSource analysisDataSource)
    {
        this.analysisDataSource = analysisDataSource ?? throw new ArgumentNullException(nameof(analysisDataSource));
    }

    public List<OpeningTrainerSnapshot> Load(string? filterText, int limit)
    {
        TrainingAnalysisDataSet dataSet = analysisDataSource.Load(filterText, limit);

        List<OpeningTrainerSnapshot> mergedSnapshots = BuildSnapshotsFromMoves(dataSet.StoredMoves);
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

    private static List<OpeningTrainerSnapshot> BuildSnapshotsFromMoves(IReadOnlyList<StoredMoveAnalysis> storedMoves)
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

    private static List<OpeningTrainerSnapshot> BuildSnapshotsFromResults(IReadOnlyList<GameAnalysisResult> results)
    {
        return results
            .Select(CreateSnapshotFromResult)
            .Where(snapshot => snapshot is not null)
            .Select(snapshot => snapshot!)
            .ToList();
    }

    private static OpeningTrainerSnapshot? CreateSnapshotFromMoves(IGrouping<AnalysisVariantKey, StoredMoveAnalysis> group)
    {
        List<StoredMoveAnalysis> openingMoves = group
            .Where(move => move.Move.Phase == GamePhase.Opening)
            .OrderBy(move => move.Move.Ply)
            .ToList();
        if (openingMoves.Count == 0)
        {
            return null;
        }

        StoredMoveAnalysis first = openingMoves[0];
        StoredGameContext game = first.Game;
        StoredAnalysisRunContext analysis = first.Analysis;
        string? playerName = analysis.AnalyzedSide == PlayerSide.White
            ? game.WhitePlayer
            : game.BlackPlayer;
        if (string.IsNullOrWhiteSpace(playerName))
        {
            return null;
        }

        return new OpeningTrainerSnapshot(
            game.GameFingerprint,
            OpeningTrainingSessionBuilder.NormalizePlayerKey(playerName),
            playerName.Trim(),
            analysis.AnalyzedSide,
            OpeningTrainingSessionBuilder.GetOpponentName(first),
            game.DateText,
            game.Result,
            OpeningTrainingSessionBuilder.NormalizeEco(game.Eco),
            OpeningCatalog.GetName(game.Eco),
            analysis.Depth,
            analysis.MultiPv,
            analysis.MoveTimeMs,
            analysis.AnalysisUpdatedUtc,
            openingMoves);
    }

    private static OpeningTrainerSnapshot? CreateSnapshotFromResult(GameAnalysisResult result)
    {
        string? playerName = result.AnalyzedSide == PlayerSide.White
            ? result.Game.WhitePlayer
            : result.Game.BlackPlayer;
        if (string.IsNullOrWhiteSpace(playerName))
        {
            return null;
        }

        List<StoredMoveAnalysis> openingMoves = StoredMoveAnalysisMapper
            .FromAnalysisResult(
                new GameAnalysisCacheKey(GameFingerprint.Compute(result.Game.PgnText), result.AnalyzedSide, 0, 0, null),
                result,
                DateTime.MinValue)
            .Where(move => move.Move.Phase == GamePhase.Opening)
            .OrderBy(move => move.Move.Ply)
            .ToList();

        if (openingMoves.Count == 0)
        {
            return null;
        }

        return new OpeningTrainerSnapshot(
            GameFingerprint.Compute(result.Game.PgnText),
            OpeningTrainingSessionBuilder.NormalizePlayerKey(playerName),
            playerName.Trim(),
            result.AnalyzedSide,
            OpeningTrainingSessionBuilder.GetOpponentName(result.Game, result.AnalyzedSide),
            result.Game.DateText,
            result.Game.Result,
            OpeningTrainingSessionBuilder.NormalizeEco(result.Game.Eco),
            OpeningCatalog.GetName(result.Game.Eco),
            0,
            0,
            null,
            DateTime.MinValue,
            openingMoves);
    }

    private readonly record struct AnalysisVariantKey(
        string GameFingerprint,
        PlayerSide Side,
        int Depth,
        int MultiPv,
        int? MoveTimeMs);

    private readonly record struct SnapshotSelectionKey(
        string GameFingerprint,
        PlayerSide Side);
}
