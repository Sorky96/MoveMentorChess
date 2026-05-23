namespace MoveMentorChess.Training;

public static class OpeningTheoryBranchBuilder
{
    public static IReadOnlyList<OpeningTheoryMove> GetTheoryMoves(
        OpeningTheoryQueryService? openingTheory,
        string fen,
        int limit = 6)
    {
        if (openingTheory is null || string.IsNullOrWhiteSpace(fen))
        {
            return [];
        }

        IReadOnlyList<OpeningTheoryMove> playableMoves = openingTheory.GetPlayableMovesForFen(fen, limit);
        return playableMoves.Count > 0
            ? playableMoves
            : openingTheory.GetTopMovesForFen(fen, limit);
    }

    public static List<OpeningTrainingBranch> BuildBranches(
        string rootFen,
        OpeningTheoryQueryService? openingTheory)
    {
        IReadOnlyList<OpeningTheoryMove> opponentMoves = GetTheoryMoves(openingTheory, rootFen, limit: 3);
        if (opponentMoves.Count == 0)
        {
            return [];
        }

        bool hasMainMove = opponentMoves.Any(move => move.IsMainMove);
        PlayerSide firstMoveSide = GetSideToMove(rootFen);
        PlayerSide responseSide = Opposite(firstMoveSide);

        return opponentMoves
            .Select((move, index) =>
            {
                IReadOnlyList<OpeningTheoryMove> replyMoves = GetTheoryMoves(openingTheory, move.ToFen, limit: 1);
                OpeningTheoryMove? reply = replyMoves.Count > 0 ? replyMoves[0] : null;
                OpeningTrainingMoveOption? recommendedResponse = reply is null
                    ? null
                    : new OpeningTrainingMoveOption(
                        reply.MoveSan,
                        reply.MoveUci,
                        OpeningTrainingMoveRole.Expected,
                        reply.IsMainMove || (!replyMoves.Any(item => item.IsMainMove) && replyMoves.Count == 1),
                        "Recommended response from imported opening theory",
                        reply.IsMainMove
                            ? OpeningLineRecallReferenceKind.ReferenceLine
                            : OpeningLineRecallReferenceKind.BetterMove,
                        OpeningTrainingMoveSourceKind.OpeningBook,
                        reply.Idea,
                        reply.ToOpeningPositionKey);
                List<OpeningTrainingMove> continuation =
                [
                    new OpeningTrainingMove(
                        0,
                        0,
                        firstMoveSide,
                        move.MoveSan,
                        move.MoveUci,
                        OpeningTrainingMoveRole.Continuation,
                        false)
                ];
                if (recommendedResponse is not null)
                {
                    continuation.Add(new OpeningTrainingMove(
                        0,
                        0,
                        responseSide,
                        recommendedResponse.DisplayText,
                        recommendedResponse.Uci,
                        OpeningTrainingMoveRole.Expected,
                        true,
                        recommendedResponse.Note));
                }

                bool isPreferred = move.IsMainMove || (!hasMainMove && index == 0);
                return new OpeningTrainingBranch(
                    new OpeningBranchKey($"theory:{move.MoveUci}:{move.ToPositionKey}"),
                    move.MoveSan,
                    move.MoveUci,
                    Math.Max(1, move.DistinctGameCount),
                    isPreferred
                        ? $"Main imported branch | games: {Math.Max(1, move.DistinctGameCount)} | occurrences: {Math.Max(1, move.OccurrenceCount)}"
                        : $"Imported branch | games: {Math.Max(1, move.DistinctGameCount)} | occurrences: {Math.Max(1, move.OccurrenceCount)}",
                    recommendedResponse,
                    continuation,
                    [],
                    move.ToOpeningPositionKey);
            })
            .ToList();
    }

    private static PlayerSide GetSideToMove(string fen)
    {
        string[] fields = fen.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return fields.Length > 1 && fields[1].Equals("b", StringComparison.OrdinalIgnoreCase)
            ? PlayerSide.Black
            : PlayerSide.White;
    }

    private static PlayerSide Opposite(PlayerSide side)
    {
        return side == PlayerSide.White ? PlayerSide.Black : PlayerSide.White;
    }

    public static string BuildSelectionSummary(IReadOnlyList<OpeningTrainingBranch> branches)
    {
        return branches.Count == 0
            ? "No imported theory branches were found for this setup."
            : $"Showing {branches.Count} imported opponent branch(es). Ordered by imported-game frequency.";
    }

    public static OpeningCoverageSummary BuildCoverageSummary(IReadOnlyList<OpeningTrainingBranch> branches)
    {
        int totalBranches = branches.Count;
        return new OpeningCoverageSummary(
            totalBranches,
            0,
            totalBranches,
            totalBranches,
            0,
            0,
            0,
            0);
    }

    public static OpponentReplyProfile BuildOpponentReplyProfile(
        OpeningLineKey lineKey,
        RepertoireSide side,
        IReadOnlyList<OpeningTrainingBranch> branches)
    {
        return new OpponentReplyProfile(
            lineKey,
            side,
            branches.Select(branch => new OpponentMoveFrequency(
                branch.OpponentMove,
                branch.OpponentMoveUci,
                branch.Frequency,
                branch.Frequency,
                0,
                0,
                false,
                OpponentMoveFrequencySourceKind.BookFrequency,
                branch.SourceSummary)).ToList(),
            branches.Count == 0
                ? "No opponent branches available."
                : $"Prepared {branches.Count} opponent branch(es) from theory.");
    }
}
