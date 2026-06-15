namespace MoveMentorChess.Training;

internal sealed class OpeningTrainingFirstMistakePositionBuilder
{
    public List<OpeningTrainingPosition> Build(
        IReadOnlyList<OpeningTrainerSnapshot> snapshots,
        Dictionary<string, OpeningTrainingLine> linesById,
        OpeningTrainingSessionOptions options,
        OpeningTheoryQueryService? openingTheory)
    {
        List<(OpeningTrainerSnapshot Snapshot, OpeningIssue Issue)> issues = snapshots
            .Select(snapshot => (Snapshot: snapshot, Issue: OpeningTrainingSessionBuilder.FindFirstIssue(snapshot, null)))
            .Where(item => item.Issue is not null)
            .Select(item => (Snapshot: item.Snapshot, Issue: item.Issue!))
            .OrderByDescending(item => item.Issue.Move.CentipawnLoss ?? 0)
            .ThenBy(item => item.Issue.Move.Ply)
            .Take(options.MaxPositionsPerSource)
            .ToList();
        List<OpeningTrainingPosition> positions = [];

        foreach ((OpeningTrainerSnapshot snapshot, OpeningIssue issue) in issues)
        {
            string lineId = OpeningTrainingSessionBuilder.BuildLineId(OpeningTrainingSourceKind.FirstOpeningMistake, snapshot.GameFingerprint, issue.Move.Ply);
            List<OpeningTrainingMoveOption> candidateMoves = OpeningTrainingSessionBuilder.BuildRepairOptions(issue.Move, openingTheory);
            if (!candidateMoves.Any(option => option.Role == OpeningTrainingMoveRole.Repair))
            {
                continue;
            }

            linesById[lineId] = OpeningTrainingSessionBuilder.CreateLine(
                lineId,
                OpeningTrainingSourceKind.FirstOpeningMistake,
                snapshot,
                issue.Move,
                "Repair the first opening mistake from this game.",
                snapshot.OpeningMoves.Where(move => move.Ply >= Math.Max(1, issue.Move.Ply - 1)).Take(options.MaxContinuationMoves).ToList(),
                issue);

            positions.Add(new OpeningTrainingPosition(
                $"first-mistake:{snapshot.GameFingerprint}:{issue.Move.Ply}",
                OpeningTrainingSessionBuilder.BuildOpeningKey(snapshot.Eco, snapshot.OpeningName),
                OpeningTrainingSessionBuilder.BuildOpeningLineKey(snapshot.Eco, snapshot.OpeningName, lineId),
                null,
                OpeningPositionKeyBuilder.BuildKey(issue.Move.FenBefore),
                OpeningTrainingMode.MistakeRepair,
                OpeningTrainingSourceKind.FirstOpeningMistake,
                snapshot.Eco,
                OpeningCatalog.Describe(snapshot.Eco),
                issue.Move.FenBefore,
                issue.Move.Ply,
                issue.Move.MoveNumber,
                snapshot.Side,
                "Repair the first opening mistake from this game before it repeats again.",
                "Replace the played move with the stronger repair move from imported opening theory and use the label as the study theme.",
                (issue.Move.CentipawnLoss ?? 0) + 100,
                OpeningTrainingSessionBuilder.ToRepertoireSide(snapshot.Side),
                options.Strictness,
                issue.Move.MistakeLabel,
                issue.Move.San,
                OpeningTrainingMoveEvaluator.GetPreferredTheoryMoveDisplay(candidateMoves),
                OpeningTrainingSessionBuilder.BuildRepairReason(issue.Move),
                OpeningTrainingSessionBuilder.BuildTags(snapshot.Eco, issue.Move.MistakeLabel, "first-opening-mistake", "mistake-repair"),
                candidateMoves,
                [OpeningTrainingSessionBuilder.ToTrainingMove(issue.Move, OpeningTrainingMoveRole.Alternative, false)],
                OpeningTrainingSessionBuilder.CreateReference(snapshot, "First opening mistake", issue),
                lineId));
        }

        return positions;
    }
}
