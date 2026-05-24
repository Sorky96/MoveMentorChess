using MoveMentorChess.Profiles;
using Xunit;

namespace MoveMentorChessServices.Tests;

public sealed class PlayerRatingTrendAnalyzerTests
{
    [Fact]
    public void Build_OrdersSnapshotsAndComputesScoreAndWeeklyQualityTrends()
    {
        RecordingStrengthEstimator estimator = new();
        PlayerRatingTrendAnalyzer analyzer = new(estimator);
        PlayerProfileSnapshot later = CreateSnapshot(
            "later",
            new DateTime(2026, 5, 20),
            PlayerSide.Black,
            "0-1",
            GameTimeControlCategory.Blitz,
            1510,
            1450,
            [
                CreateMove(MoveQualityBucket.Mistake, 100),
                CreateMove(MoveQualityBucket.Best, 0)
            ]);
        PlayerProfileSnapshot earlier = CreateSnapshot(
            "earlier",
            new DateTime(2026, 5, 18),
            PlayerSide.White,
            "1-0",
            GameTimeControlCategory.Blitz,
            1500,
            1400,
            [
                CreateMove(MoveQualityBucket.Blunder, 200)
            ]);

        PlayerRatingTrendReport report = analyzer.Build([later, earlier], null);

        Assert.Equal(2, report.GamesAnalyzed);
        Assert.Equal(1510, report.CurrentImportedRating);
        Assert.Equal(["earlier", "later"], report.RatingPoints.Select(point => point.GameFingerprint).ToArray());
        Assert.Equal(1.0, report.RatingPoints[0].ActualScore);
        Assert.Equal(1.0, report.RatingPoints[1].ActualScore);
        Assert.InRange(report.RatingPoints[0].ExpectedScore!.Value, 0.63, 0.65);
        Assert.Equal("later", report.CurrentStrength!.GameFingerprint);

        ProfileMonthlyTrend cplTrend = Assert.Single(report.AverageCentipawnLossTrend);
        Assert.Equal("2026-05-18", cplTrend.MonthKey);
        Assert.Equal(2, cplTrend.GamesAnalyzed);
        Assert.Equal(100, cplTrend.AverageCentipawnLoss);

        ProfileMoveQualityTrend qualityTrend = Assert.Single(report.MoveQualityTrend);
        Assert.Equal("2026-05-18", qualityTrend.PeriodKey);
        Assert.Equal(0.5, qualityTrend.BlundersPerGame);
        Assert.Equal(0.5, qualityTrend.MistakesPerGame);
        Assert.Equal(0.0, qualityTrend.InaccuraciesPerGame);
        Assert.Equal(0.5, qualityTrend.BrilliantGreatBestPerGame);
        Assert.All(estimator.Inputs, input => Assert.Equal(2, input.SameTimeControlSampleSize));
    }

    [Fact]
    public void BuildByTimeControl_ExcludesUnknownAndUsesPerCategorySampleSizes()
    {
        RecordingStrengthEstimator estimator = new();
        PlayerRatingTrendAnalyzer analyzer = new(estimator);

        List<PlayerRatingTrendReport> reports = analyzer.BuildByTimeControl(
        [
            CreateSnapshot("blitz-1", new DateTime(2026, 5, 1), PlayerSide.White, "1-0", GameTimeControlCategory.Blitz, 1500, 1400),
            CreateSnapshot("rapid-1", new DateTime(2026, 5, 2), PlayerSide.White, "1/2-1/2", GameTimeControlCategory.Rapid, 1520, 1520),
            CreateSnapshot("unknown-1", new DateTime(2026, 5, 3), PlayerSide.White, "0-1", GameTimeControlCategory.Unknown, 1530, 1540),
            CreateSnapshot("blitz-2", new DateTime(2026, 5, 4), PlayerSide.Black, "0-1", GameTimeControlCategory.Blitz, 1540, 1500)
        ]);

        Assert.Equal([GameTimeControlCategory.Blitz, GameTimeControlCategory.Rapid], reports.Select(report => report.TimeControlCategory).ToArray());
        Assert.Equal(2, reports[0].GamesAnalyzed);
        Assert.Equal(1, reports[1].GamesAnalyzed);
        Assert.DoesNotContain(reports, report => report.TimeControlCategory == GameTimeControlCategory.Unknown);

        Dictionary<string, int> sampleSizeByGame = estimator.Inputs.ToDictionary(input => input.GameFingerprint, input => input.SameTimeControlSampleSize);
        Assert.Equal(2, sampleSizeByGame["blitz-1"]);
        Assert.Equal(2, sampleSizeByGame["blitz-2"]);
        Assert.Equal(1, sampleSizeByGame["rapid-1"]);
        Assert.False(sampleSizeByGame.ContainsKey("unknown-1"));
    }

    private static PlayerProfileSnapshot CreateSnapshot(
        string gameFingerprint,
        DateTime gameDate,
        PlayerSide side,
        string result,
        GameTimeControlCategory timeControlCategory,
        int? playerRating,
        int? opponentRating,
        IReadOnlyList<StoredMoveAnalysis>? moves = null)
    {
        return new PlayerProfileSnapshot(
            gameFingerprint,
            "alpha",
            "Alpha",
            side,
            gameDate,
            gameDate.ToString("yyyy-MM", System.Globalization.CultureInfo.InvariantCulture),
            $"{gameDate.Year}-Q{((gameDate.Month - 1) / 3) + 1}",
            "C20",
            18,
            1,
            null,
            gameDate,
            playerRating,
            opponentRating,
            result,
            timeControlCategory,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            moves ?? []);
    }

    private static StoredMoveAnalysis CreateMove(MoveQualityBucket quality, int centipawnLoss)
    {
        return new StoredMoveAnalysis(
            new StoredGameContext("game", "Alpha", "Beta", "2026.05.18", "1-0", "C20", null),
            new StoredAnalysisRunContext(PlayerSide.White, 18, 1, null, new DateTime(2026, 5, 18)),
            new StoredMoveContext(
                1,
                1,
                "e4",
                "e2e4",
                "start",
                "after",
                GamePhase.Opening,
                20,
                -centipawnLoss,
                null,
                null,
                centipawnLoss,
                quality,
                0,
                "e2e4"),
            new StoredMoveAdviceContext("opening_principles", 0.8, ["evidence"], "Short", "Detailed", "Hint", quality.IsProblem()));
    }

    private sealed class RecordingStrengthEstimator : IPlayerStrengthEstimator
    {
        public List<PlayerStrengthEstimateInput> Inputs { get; } = [];

        public MoveMentorStrengthPoint Estimate(PlayerStrengthEstimateInput input)
        {
            Inputs.Add(input);
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
