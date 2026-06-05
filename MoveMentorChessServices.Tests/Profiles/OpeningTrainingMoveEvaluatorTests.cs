using Xunit;

namespace MoveMentorChessServices.Tests.Profiles;

public sealed class OpeningTrainingMoveEvaluatorTests
{
    private static readonly OpeningTrainingMoveEvaluator Evaluator = new();

    [Fact]
    public void EvaluateMove_AcceptsPreferredUciMove()
    {
        OpeningTrainingPosition position = CreatePosition(
            OpeningTrainingMode.LineRecall,
            [
                new OpeningTrainingMoveOption("e4", "e2e4", OpeningTrainingMoveRole.Expected, true),
                new OpeningTrainingMoveOption("d4", "d2d4", OpeningTrainingMoveRole.Alternative, false)
            ]);

        OpeningTrainingAttemptResult result = Evaluator.EvaluateMove(position, "e2e4");

        Assert.Equal(OpeningTrainingScore.Correct, result.Score);
        Assert.Equal("e4", result.ResolvedSan);
        Assert.Equal("e2e4", result.ResolvedUci);
        Assert.NotEmpty(result.PreferredReferences);
        Assert.Contains("Accepted as correct", result.ShortExplanation);
    }

    [Fact]
    public void EvaluateMove_RejectsEmptyMoveWithExpectedReferences()
    {
        OpeningTrainingPosition position = CreatePosition(
            OpeningTrainingMode.LineRecall,
            [new OpeningTrainingMoveOption("e4", "e2e4", OpeningTrainingMoveRole.Expected, true)]);

        OpeningTrainingAttemptResult result = Evaluator.EvaluateMove(position, " ");

        Assert.Equal(OpeningTrainingScore.Wrong, result.Score);
        Assert.Null(result.ResolvedSan);
        Assert.Equal("Move cannot be empty.", result.ShortExplanation);
        Assert.Single(result.ExpectedMoves);
    }

    [Fact]
    public void EvaluateMove_UsesHighestFrequencyBranchAsPreferredReference()
    {
        OpeningTrainingPosition position = CreatePosition(
            OpeningTrainingMode.BranchAwareness,
            [
                new OpeningTrainingMoveOption("c5", "c7c5", OpeningTrainingMoveRole.Alternative, false),
                new OpeningTrainingMoveOption("e5", "e7e5", OpeningTrainingMoveRole.Alternative, false)
            ],
            sideToMove: PlayerSide.Black,
            branches:
            [
                new OpeningTrainingBranch("c5", "c7c5", 3, "Book", null, [], []),
                new OpeningTrainingBranch("e5", "e7e5", 9, "Book", null, [], [])
            ]);

        OpeningTrainingAttemptResult result = Evaluator.EvaluateMove(position, "e7e5");

        Assert.Equal(OpeningTrainingScore.Correct, result.Score);
        OpeningTrainingMoveOption preferred = Assert.Single(result.PreferredReferences);
        Assert.Equal("e7e5", preferred.Uci);
        Assert.Equal(2, result.ResolvedPosition?.MoveNumber);
        Assert.Contains("Correct branch", result.ShortExplanation);
    }

    private static OpeningTrainingPosition CreatePosition(
        OpeningTrainingMode mode,
        IReadOnlyList<OpeningTrainingMoveOption> candidateMoves,
        PlayerSide sideToMove = PlayerSide.White,
        IReadOnlyList<OpeningTrainingBranch>? branches = null)
    {
        return new OpeningTrainingPosition(
            "position-1",
            mode,
            OpeningTrainingSourceKind.OpeningWeakness,
            "C20",
            "King's Pawn Game",
            sideToMove == PlayerSide.Black
                ? "rnbqkbnr/pppppppp/8/8/4P3/8/PPPP1PPP/RNBQKBNR b KQkq - 0 1"
                : "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1",
            0,
            1,
            sideToMove,
            "Find the move.",
            "Play the best move.",
            10,
            "opening_principles",
            null,
            candidateMoves.FirstOrDefault(option => option.IsPreferred)?.DisplayText,
            "Develop toward the center.",
            ["opening_principles"],
            candidateMoves,
            [],
            new OpeningTrainingReference("game-1", PlayerSide.White, "Opponent", null, null, "Test", null, null),
            branches: branches);
    }
}
