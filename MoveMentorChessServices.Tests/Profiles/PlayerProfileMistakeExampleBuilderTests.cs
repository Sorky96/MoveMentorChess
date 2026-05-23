using MoveMentorChess.Profiles;
using Xunit;

namespace MoveMentorChessServices.Tests;

public sealed class PlayerProfileMistakeExampleBuilderTests
{
    private const string StartFen = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";
    private const string AfterE4Fen = "rnbqkbnr/pppppppp/8/8/4P3/8/PPPP1PPP/RNBQKBNR b KQkq - 0 1";

    [Fact]
    public void BuildRecommendationContext_UsesHighlightedGroupsBeforeRawMoveFallback()
    {
        PlayerProfileSnapshot highlightedOpening = CreateSnapshot(
            "highlighted-opening",
            PlayerSide.White,
            "C20",
            [
                CreateMove("hanging_piece", GamePhase.Opening, MoveQualityBucket.Mistake, 120, 1, isHighlighted: true),
                CreateMove("hanging_piece", GamePhase.Opening, MoveQualityBucket.Mistake, 100, 3, isHighlighted: true)
            ]);
        PlayerProfileSnapshot rawMiddlegame = CreateSnapshot(
            "raw-middlegame",
            PlayerSide.Black,
            "B01",
            [
                CreateMove("hanging_piece", GamePhase.Middlegame, MoveQualityBucket.Blunder, 260, 5, isHighlighted: false)
            ]);

        RecommendationContext context = PlayerProfileMistakeExampleBuilder.BuildRecommendationContext(
            [highlightedOpening, rawMiddlegame],
            "hanging_piece");

        Assert.Equal(GamePhase.Opening, context.DominantPhase);
        Assert.Equal(PlayerSide.White, context.DominantSide);
        Assert.Equal(["B01", "C20"], context.TopOpenings);
    }

    [Fact]
    public void BuildRecommendationContext_FallsBackToRawMovesWhenNoHighlightedPhaseExists()
    {
        PlayerProfileSnapshot snapshot = CreateSnapshot(
            "raw-only",
            PlayerSide.Black,
            "B12",
            [
                CreateMove("missed_tactic", GamePhase.Middlegame, MoveQualityBucket.Mistake, 180, 9, isHighlighted: false),
                CreateMove("missed_tactic", GamePhase.Middlegame, MoveQualityBucket.Mistake, 160, 11, isHighlighted: false)
            ]);

        RecommendationContext context = PlayerProfileMistakeExampleBuilder.BuildRecommendationContext([snapshot], "missed_tactic");

        Assert.Equal(GamePhase.Middlegame, context.DominantPhase);
        Assert.Equal(PlayerSide.Black, context.DominantSide);
        Assert.Equal(["B12"], context.TopOpenings);
    }

    [Fact]
    public void Build_ReturnsRankedExamplesAcrossTopLabelsWithinTotalLimit()
    {
        IReadOnlyList<PlayerProfileSnapshot> snapshots =
        [
            CreateSnapshot(
                "game-1",
                PlayerSide.White,
                "C20",
                [
                    CreateMove("hanging_piece", GamePhase.Opening, MoveQualityBucket.Mistake, 120, 1, isHighlighted: true, bestMoveUci: "e2e4"),
                    CreateMove("missed_tactic", GamePhase.Middlegame, MoveQualityBucket.Blunder, 260, 5, isHighlighted: true, bestMoveUci: "g1f3")
                ]),
            CreateSnapshot(
                "game-2",
                PlayerSide.White,
                "C20",
                [
                    CreateMove("hanging_piece", GamePhase.Opening, MoveQualityBucket.Blunder, 240, 7, isHighlighted: true, bestMoveUci: "d2d4")
                ]),
            CreateSnapshot(
                "game-3",
                PlayerSide.Black,
                "B01",
                [
                    CreateMove("missed_tactic", GamePhase.Middlegame, MoveQualityBucket.Mistake, 170, 9, isHighlighted: false, bestMoveUci: "g1f3")
                ])
        ];
        IReadOnlyList<ProfileLabelStat> topLabels =
        [
            new("hanging_piece", 2, 0.8),
            new("missed_tactic", 2, 0.8)
        ];

        List<ProfileMistakeExample> examples = PlayerProfileMistakeExampleBuilder.Build(snapshots, topLabels, maxTotal: 4);

        Assert.Equal(4, examples.Count);
        Assert.Equal([260, 240, 170, 120], examples.Select(example => example.CentipawnLoss).ToArray());
        Assert.Contains(examples, example => example.Label == "hanging_piece" && example.Rank == ProfileMistakeExampleRank.MostFrequent);
        Assert.Contains(examples, example => example.Label == "hanging_piece" && example.Rank == ProfileMistakeExampleRank.MostCostly);
        Assert.Contains(examples, example => example.Label == "missed_tactic" && example.Rank == ProfileMistakeExampleRank.MostFrequent);
        Assert.Contains(examples, example => example.Label == "missed_tactic" && example.Rank == ProfileMistakeExampleRank.MostCostly);
        Assert.All(examples, example => Assert.False(string.IsNullOrWhiteSpace(example.BetterMove)));
    }

    private static PlayerProfileSnapshot CreateSnapshot(
        string gameFingerprint,
        PlayerSide side,
        string eco,
        IReadOnlyList<StoredMoveAnalysis> moves)
    {
        return new PlayerProfileSnapshot(
            gameFingerprint,
            "alpha",
            "Alpha",
            side,
            new DateTime(2026, 5, 18),
            "2026-05",
            "2026-Q2",
            eco,
            18,
            1,
            null,
            new DateTime(2026, 5, 18),
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
        string bestMoveUci = "e2e4")
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
}
