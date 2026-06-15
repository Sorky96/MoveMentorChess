namespace MoveMentorChess.Training;

internal sealed class OpeningTrainingExampleGamePositionBuilder
{
    public List<OpeningTrainingPosition> Build(
        OpeningWeaknessReport weaknessReport,
        IReadOnlyDictionary<SnapshotKey, OpeningTrainerSnapshot> snapshotIndex,
        Dictionary<string, OpeningTrainingLine> linesById,
        OpeningTrainingSessionOptions options,
        OpeningTheoryQueryService? openingTheory)
    {
        List<(OpeningWeaknessEntry Entry, OpeningExampleGame Example)> examples = weaknessReport.WeakOpenings
            .SelectMany(entry => entry.ExampleGames.Select(example => (Entry: entry, Example: example)))
            .OrderByDescending(item => item.Example.FirstMistakeCentipawnLoss ?? 0)
            .ThenBy(item => item.Example.FirstMistakePly ?? int.MaxValue)
            .Take(options.MaxPositionsPerSource)
            .ToList();
        List<OpeningTrainingPosition> positions = [];

        foreach ((OpeningWeaknessEntry entry, OpeningExampleGame example) in examples)
        {
            SnapshotKey key = new(example.GameFingerprint, example.Side);
            if (!snapshotIndex.TryGetValue(key, out OpeningTrainerSnapshot? snapshot))
            {
                continue;
            }

            OpeningIssue? firstIssue = OpeningTrainingSessionBuilder.FindFirstIssue(snapshot, example.FirstMistakePly);
            StoredMoveAnalysis anchorMove = snapshot.OpeningMoves
                .Where(move => move.Ply < (firstIssue?.Move.Ply ?? int.MaxValue) && !OpeningTrainingSessionBuilder.IsOpeningIssue(move))
                .LastOrDefault()
                ?? snapshot.OpeningMoves.Where(move => move.Ply < (firstIssue?.Move.Ply ?? int.MaxValue)).LastOrDefault()
                ?? snapshot.OpeningMoves[0];

            IReadOnlyList<StoredMoveAnalysis> lineMoves = snapshot.OpeningMoves
                .Where(move => move.Ply >= anchorMove.Ply)
                .Take(options.MaxContinuationMoves)
                .ToList();
            string lineId = OpeningTrainingSessionBuilder.BuildLineId(OpeningTrainingSourceKind.ExampleGame, snapshot.GameFingerprint, anchorMove.Ply);
            List<OpeningTrainingMoveOption> candidateMoves = OpeningTrainingSessionBuilder.BuildLineRecallOptions(anchorMove, openingTheory);
            if (!candidateMoves.Any(option => option.IsPreferred))
            {
                continue;
            }

            linesById[lineId] = OpeningTrainingSessionBuilder.CreateLine(
                lineId,
                OpeningTrainingSourceKind.ExampleGame,
                snapshot,
                anchorMove,
                "Recall the stable line from imported opening theory around this example game.",
                lineMoves,
                firstIssue);

            positions.Add(new OpeningTrainingPosition(
                $"example:{snapshot.GameFingerprint}:{anchorMove.Ply}",
                OpeningTrainingSessionBuilder.BuildOpeningKey(snapshot.Eco, snapshot.OpeningName),
                OpeningTrainingSessionBuilder.BuildOpeningLineKey(snapshot.Eco, snapshot.OpeningName, lineId),
                null,
                OpeningPositionKeyBuilder.BuildKey(anchorMove.FenBefore),
                OpeningTrainingMode.LineRecall,
                OpeningTrainingSourceKind.ExampleGame,
                snapshot.Eco,
                OpeningCatalog.Describe(snapshot.Eco),
                anchorMove.FenBefore,
                anchorMove.Ply,
                anchorMove.MoveNumber,
                snapshot.Side,
                "Recall the move that imported opening theory recommends in this example position.",
                "Use the imported opening book as the source of truth, then replay the surrounding example line for context.",
                (example.FirstMistakeCentipawnLoss ?? 0) + 25,
                OpeningTrainingSessionBuilder.ToRepertoireSide(snapshot.Side),
                options.Strictness,
                firstIssue?.Move.MistakeLabel,
                anchorMove.San,
                OpeningTrainingMoveEvaluator.GetPreferredTheoryMoveDisplay(candidateMoves),
                firstIssue is null ? null : OpeningTrainingSessionBuilder.BuildRepairReason(firstIssue.Move),
                OpeningTrainingSessionBuilder.BuildTags(snapshot.Eco, firstIssue?.Move.MistakeLabel, "example-game", "line-recall"),
                candidateMoves,
                lineMoves.Select((move, index) => OpeningTrainingSessionBuilder.ToTrainingMove(move, index == 0 ? OpeningTrainingMoveRole.Expected : OpeningTrainingMoveRole.Continuation, index == 0)).ToList(),
                OpeningTrainingSessionBuilder.CreateReference(snapshot, "Example game", firstIssue),
                lineId));
        }

        return positions;
    }
}
