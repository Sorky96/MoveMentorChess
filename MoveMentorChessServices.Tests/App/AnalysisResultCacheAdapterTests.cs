using MoveMentorChess.App.ViewModels;
using MoveMentorChess.Persistence;
using Xunit;

namespace MoveMentorChessServices.Tests.App;

public sealed class AnalysisResultCacheAdapterTests
{
    [Fact]
    public void MainWindowAnalysisDataServiceUsesInjectedCache()
    {
        RecordingAnalysisResultCache cache = new();
        DefaultMainWindowAnalysisDataService dataService = new(() => null, cache);
        ImportedGame game = CreateGame();
        EngineAnalysisOptions options = new(Depth: 8, MultiPv: 2);
        GameAnalysisResult expected = CreateResult(game);

        dataService.StoreAnalysisResult(game, PlayerSide.White, options, expected);
        bool found = dataService.TryGetCachedAnalysis(game, PlayerSide.White, options, out GameAnalysisResult? actual);

        Assert.True(found);
        Assert.Same(expected, actual);
        Assert.Equal(2, cache.CreateKeyCallCount);
        Assert.Equal(1, cache.StoreResultCallCount);
        Assert.Equal(1, cache.TryGetResultCallCount);
    }

    private static ImportedGame CreateGame()
    {
        return new ImportedGame(
            PgnText: "1. e4 e5 2. Nf3 Nc6",
            SanMoves: ["e4", "e5", "Nf3", "Nc6"],
            WhitePlayer: "White",
            BlackPlayer: "Black",
            WhiteElo: null,
            BlackElo: null,
            DateText: null,
            Result: null,
            Eco: null,
            Site: null);
    }

    private static GameAnalysisResult CreateResult(ImportedGame game)
        => new(game, PlayerSide.White, [], [], []);

    private sealed class RecordingAnalysisResultCache : IAnalysisResultCache
    {
        private readonly Dictionary<GameAnalysisCacheKey, GameAnalysisResult> results = [];

        public int CreateKeyCallCount { get; private set; }

        public int StoreResultCallCount { get; private set; }

        public int TryGetResultCallCount { get; private set; }

        public GameAnalysisCacheKey CreateKey(ImportedGame game, PlayerSide side, EngineAnalysisOptions options)
        {
            CreateKeyCallCount++;
            return new GameAnalysisCacheKey(
                GameFingerprint.Compute(game.PgnText),
                side,
                options.Depth,
                options.MultiPv,
                options.MoveTimeMs);
        }

        public bool TryGetResult(GameAnalysisCacheKey key, out GameAnalysisResult? result)
        {
            TryGetResultCallCount++;
            return results.TryGetValue(key, out result);
        }

        public void StoreResult(GameAnalysisCacheKey key, GameAnalysisResult result)
        {
            StoreResultCallCount++;
            results[key] = result;
        }

        public bool TryGetWindowState(ImportedGame importedGame, out AnalysisWindowState? state)
        {
            state = null;
            return false;
        }

        public void StoreWindowState(ImportedGame importedGame, AnalysisWindowState state)
        {
        }
    }
}
