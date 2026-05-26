using System.IO;
using MoveMentorChess.Analysis;
using MoveMentorChess.Domain;
using Xunit;

namespace MoveMentorChessServices.Tests;

public sealed class PlayerMistakeProfileSourceTests
{
    [Fact]
    public void StoreBackedSource_BuildsProfileFromInjectedStore()
    {
        InMemoryAnalysisResultStore store = new(
        [
            CreateResult("Alpha", PlayerSide.White, "tactics", 30, GamePhase.Middlegame, MoveQualityBucket.Mistake),
            CreateResult("Alpha", PlayerSide.White, "tactics", 90, GamePhase.Endgame, MoveQualityBucket.Blunder),
            CreateResult("Beta", PlayerSide.White, "opening", 10, GamePhase.Opening, MoveQualityBucket.Mistake)
        ]);
        StoreBackedPlayerMistakeProfileSource source = new(() => store);

        PlayerMistakeProfile? profile = source.TryBuild(" Alpha ");

        Assert.NotNull(profile);
        Assert.Equal("Alpha", profile.PlayerName);
        Assert.Equal(2, profile.GamesAnalyzed);
        Assert.Equal(60, profile.AverageCentipawnLoss);
        PlayerMistakePatternEntry pattern = Assert.Single(profile.TopPatterns);
        Assert.Equal("tactics", pattern.Label);
        Assert.Equal(2, pattern.Count);
        Assert.Equal(GamePhase.Middlegame, profile.WeakestPhase);
    }

    [Fact]
    public void StoreBackedSource_ReturnsNullWhenStoreProviderFallsBack()
    {
        StoreBackedPlayerMistakeProfileSource source = new(() => throw new IOException("store unavailable"));

        PlayerMistakeProfile? profile = source.TryBuild("Alpha");

        Assert.Null(profile);
    }

    private static GameAnalysisResult CreateResult(
        string playerName,
        PlayerSide analyzedSide,
        string tagLabel,
        int centipawnLoss,
        GamePhase phase,
        MoveQualityBucket quality)
    {
        ImportedGame game = new(
            PgnText: $"[White \"{playerName}\"]\n[Black \"Opponent\"]\n\n1. e4 e5",
            SanMoves: ["e4", "e5"],
            WhitePlayer: playerName,
            BlackPlayer: "Opponent",
            WhiteElo: null,
            BlackElo: null,
            DateText: null,
            Result: null,
            Eco: null,
            Site: null);
        MoveAnalysisResult move = CreateMove(centipawnLoss, phase, quality);
        SelectedMistake selectedMistake = new(
            [move],
            quality,
            new MistakeTag(tagLabel, 0.9, ["fixture"]),
            new MoveExplanation("short", "hint"));

        return new GameAnalysisResult(
            game,
            analyzedSide,
            [move.Replay],
            [move],
            [selectedMistake]);
    }

    private static MoveAnalysisResult CreateMove(int centipawnLoss, GamePhase phase, MoveQualityBucket quality)
    {
        ReplayPly replay = new(
            Ply: 1,
            MoveNumber: 1,
            Side: PlayerSide.White,
            San: "e4",
            NormalizedSan: "e4",
            Uci: "e2e4",
            FenBefore: "before",
            FenAfter: "after",
            PlacementFenBefore: "before",
            PlacementFenAfter: "after",
            Phase: phase,
            MovingPiece: "P",
            PromotionPiece: null,
            FromSquare: "e2",
            ToSquare: "e4",
            IsCapture: false,
            IsEnPassant: false,
            IsCastle: false);
        EngineAnalysis before = new("before", [], null);
        EngineAnalysis after = new("after", [], null);

        return new MoveAnalysisResult(
            replay,
            before,
            after,
            EvalBeforeCp: null,
            EvalAfterCp: null,
            BestMateIn: null,
            PlayedMateIn: null,
            CentipawnLoss: centipawnLoss,
            Quality: quality,
            MaterialDeltaCp: 0,
            MistakeTag: null,
            Explanation: null);
    }

    private sealed class InMemoryAnalysisResultStore : IAnalysisResultStore
    {
        private readonly IReadOnlyList<GameAnalysisResult> results;

        public InMemoryAnalysisResultStore(IReadOnlyList<GameAnalysisResult> results)
        {
            this.results = results;
        }

        public IReadOnlyList<GameAnalysisResult> ListResults(string? filterText = null, int limit = 500)
            => results.Take(limit).ToList();

        public bool TryLoadResult(GameAnalysisCacheKey key, out GameAnalysisResult? result)
        {
            result = null;
            return false;
        }

        public void SaveResult(GameAnalysisCacheKey key, GameAnalysisResult result)
        {
        }
    }
}
