namespace MoveMentorChess.Training;

internal sealed class OpeningTrainingOpeningWeaknessPositionBuilder
{
    public List<OpeningTrainingPosition> Build(
        OpeningWeaknessReport weaknessReport,
        IReadOnlyDictionary<SnapshotKey, OpeningTrainerSnapshot> snapshotIndex,
        List<SavedOpeningReplay> savedReplays,
        Dictionary<string, OpeningTrainingLine> linesById,
        OpeningTrainingSessionOptions options,
        OpeningTheoryQueryService? openingTheory)
    {
        List<(OpeningWeaknessEntry Entry, BranchRoot root)> roots = weaknessReport.WeakOpenings
            .SelectMany(entry => OpeningTrainingSessionBuilder.BuildBranchRoots(entry, snapshotIndex, savedReplays))
            .OrderByDescending(item => item.root.Priority)
            .ThenBy(item => item.root.AnchorPly)
            .Take(options.MaxPositionsPerSource)
            .ToList();
        List<OpeningTrainingPosition> positions = [];

        foreach ((OpeningWeaknessEntry entry, BranchRoot root) in roots)
        {
            List<OpeningTrainingBranch> theoryBranches = OpeningTheoryBranchBuilder.BuildBranches(root.RootFen, openingTheory);
            if (theoryBranches.Count == 0)
            {
                continue;
            }

            OpeningTrainingMoveOption? primaryRecommendedResponse = theoryBranches
                .Select(branch => branch.RecommendedResponse)
                .FirstOrDefault(option => option is not null);
            List<OpeningTrainingMoveOption> candidateMoves = theoryBranches
                .Select(branch => new OpeningTrainingMoveOption(
                    branch.OpponentMove,
                    branch.OpponentMoveUci,
                    OpeningTrainingMoveRole.Alternative,
                    false,
                    branch.SourceSummary,
                    OpeningLineRecallReferenceKind.ReferenceLine,
                    OpeningTrainingMoveSourceKind.OpeningBook,
                    branch.RecommendedResponse?.Idea,
                    branch.ResultingPositionKey))
                .ToList();
            string branchSelectionSummary = OpeningTheoryBranchBuilder.BuildSelectionSummary(theoryBranches);
            IReadOnlyList<OpeningTrainingMove> primaryContinuation = theoryBranches[0].Continuation;
            OpeningIssue? issue = root.FirstIssue;
            string lineId = OpeningTrainingSessionBuilder.BuildLineId(OpeningTrainingSourceKind.OpeningWeakness, root.Snapshot.GameFingerprint, root.AnchorPly);
            linesById[lineId] = OpeningTrainingSessionBuilder.CreateLine(
                lineId,
                OpeningTrainingSourceKind.OpeningWeakness,
                root.Snapshot,
                root.AnchorMove,
                "Review the opponent branches that show up most often after your chosen setup move.",
                root.SampleLine,
                issue);

            positions.Add(new OpeningTrainingPosition(
                $"weakness:{root.Snapshot.GameFingerprint}:{root.AnchorPly}",
                OpeningTrainingSessionBuilder.BuildOpeningKey(entry.Eco, entry.OpeningName),
                OpeningTrainingSessionBuilder.BuildOpeningLineKey(entry.Eco, entry.OpeningName, lineId),
                null,
                OpeningPositionKeyBuilder.BuildKey(root.RootFen),
                OpeningTrainingMode.BranchAwareness,
                OpeningTrainingSourceKind.OpeningWeakness,
                entry.Eco,
                entry.OpeningDisplayName,
                root.RootFen,
                root.AnchorPly + 1,
                root.AnchorMove.MoveNumber,
                OpeningTrainingSessionBuilder.Opponent(root.Snapshot.Side),
                "Review the typical opponent replies from imported opening theory and keep one theory-backed reaction ready.",
                "Use the imported opening tree as the source of truth for the opponent branches in this position.",
                root.Priority,
                OpeningTrainingSessionBuilder.ToRepertoireSide(root.Snapshot.Side),
                options.Strictness,
                root.ThemeLabel,
                root.AnchorMove.San,
                primaryRecommendedResponse?.DisplayText,
                primaryRecommendedResponse?.Note,
                OpeningTrainingSessionBuilder.BuildTags(entry.Eco, root.ThemeLabel, "opening-weakness", "branch-awareness"),
                candidateMoves,
                primaryContinuation,
                OpeningTrainingSessionBuilder.CreateReference(root.Snapshot, "Opening weakness", issue),
                lineId,
                theoryBranches,
                branchSelectionSummary,
                OpeningTheoryBranchBuilder.BuildCoverageSummary(theoryBranches),
                OpeningTheoryBranchBuilder.BuildOpponentReplyProfile(
                    OpeningTrainingSessionBuilder.BuildOpeningLineKey(entry.Eco, entry.OpeningName, lineId),
                    OpeningTrainingSessionBuilder.ToRepertoireSide(root.Snapshot.Side),
                    theoryBranches)));
        }

        return positions;
    }
}
