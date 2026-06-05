using MoveMentorChess.Profiles;
using Xunit;

namespace MoveMentorChessServices.Tests.Profiles;

public sealed class PlayerProfileReportBuilderTests
{
    private const string StartFen = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";
    private const string AfterE4Fen = "rnbqkbnr/pppppppp/8/8/4P3/8/PPPP1PPP/RNBQKBNR b KQkq - 0 1";

    [Fact]
    public void Build_AssemblesFinalReportTrainingPlanAndMistakeExamples()
    {
        PlayerProfileReportBuilder builder = new(new PlayerRatingTrendAnalyzer(new RecordingStrengthEstimator()));
        List<PlayerProfileSnapshot> snapshots =
        [
            CreateSnapshot(
                "game-1",
                "Alpha",
                PlayerSide.White,
                "C20",
                new DateTime(2026, 5, 18),
                [
                    CreateMove("hanging_piece", GamePhase.Middlegame, MoveQualityBucket.Blunder, 260, 5, isHighlighted: true, bestMoveUci: "g1f3"),
                    CreateMove("opening_principles", GamePhase.Opening, MoveQualityBucket.Mistake, 140, 1, isHighlighted: true, bestMoveUci: "e2e4")
                ]),
            CreateSnapshot(
                "game-2",
                "Alpha",
                PlayerSide.Black,
                "B01",
                new DateTime(2026, 5, 20),
                [
                    CreateMove("hanging_piece", GamePhase.Middlegame, MoveQualityBucket.Mistake, 180, 7, isHighlighted: true, bestMoveUci: "d2d4")
                ])
        ];

        PlayerProfileReport report = builder.Build(snapshots, openingReport: null, trainingHistory: []);

        Assert.Equal("Alpha", report.DisplayName);
        Assert.Equal(2, report.GamesAnalyzed);
        Assert.Equal(3, report.TotalAnalyzedMoves);
        Assert.Equal(3, report.HighlightedMistakes);
        Assert.Equal(193, report.AverageCentipawnLoss);
        Assert.Equal("hanging_piece", report.TopMistakeLabels[0].Label);
        Assert.Equal(2, report.TopMistakeLabels[0].Count);
        Assert.Equal("hanging_piece", report.CostliestMistakeLabels[0].Label);
        Assert.Equal(440, report.CostliestMistakeLabels[0].TotalCentipawnLoss);
        Assert.NotEmpty(report.Recommendations);
        Assert.Equal(report.Recommendations, report.TrainingPlan.Recommendations);
        Assert.Equal("Alpha Weekly Training Plan", report.WeeklyPlan.Title);
        Assert.Equal(report.WeeklyPlan, report.TrainingPlan.WeeklyPlan);
        Assert.NotEmpty(report.MistakeExamples);
        Assert.Contains(report.MistakeExamples, example => example.Label == "hanging_piece");
        Assert.Equal("game-2", report.RatingTrend.CurrentStrength!.GameFingerprint);
    }

    private static PlayerProfileSnapshot CreateSnapshot(
        string gameFingerprint,
        string displayName,
        PlayerSide side,
        string eco,
        DateTime gameDate,
        IReadOnlyList<StoredMoveAnalysis> moves)
    {
        return new PlayerProfileSnapshot(
            gameFingerprint,
            displayName.ToLowerInvariant(),
            displayName,
            side,
            gameDate,
            gameDate.ToString("yyyy-MM", System.Globalization.CultureInfo.InvariantCulture),
            $"{gameDate.Year}-Q{((gameDate.Month - 1) / 3) + 1}",
            eco,
            18,
            1,
            null,
            gameDate,
            1500,
            1450,
            side == PlayerSide.White ? "1-0" : "0-1",
            GameTimeControlCategory.Blitz,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            moves);
    }

    private static StoredMoveAnalysis CreateMove(
        string label,
        GamePhase phase,
        MoveQualityBucket quality,
        int centipawnLoss,
        int ply,
        bool isHighlighted,
        string bestMoveUci)
    {
        int moveNumber = (ply + 1) / 2;
        return new StoredMoveAnalysis(
            new StoredGameContext("game", "Alpha", "Beta", "2026.05.18", "1-0", "C20", null),
            new StoredAnalysisRunContext(PlayerSide.White, 18, 1, null, new DateTime(2026, 5, 18)),
            new StoredMoveContext(
                ply,
                moveNumber,
                "Qh5",
                "d1h5",
                StartFen,
                AfterE4Fen,
                phase,
                20,
                -centipawnLoss,
                null,
                null,
                centipawnLoss,
                quality,
                0,
                bestMoveUci),
            new StoredMoveAdviceContext(label, 0.8, ["evidence"], "Short", "Detailed", "Hint", isHighlighted));
    }

    private sealed class RecordingStrengthEstimator : IPlayerStrengthEstimator
    {
        public MoveMentorStrengthPoint Estimate(PlayerStrengthEstimateInput input)
        {
            int estimate = input.PlayerRating ?? 1200;
            return new MoveMentorStrengthPoint(
                input.GameFingerprint,
                input.GameDate,
                input.TimeControlCategory,
                estimate,
                estimate - 50,
                estimate + 50,
                MoveMentorStrengthConfidence.Medium,
                MoveMentorStrengthEstimatorKind.HeuristicV1,
                "test");
        }
    }
}
