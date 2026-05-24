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

    [Fact]
    public void DefaultSavedLibraryDataServiceRemovesCachedAnalysisThroughInjectedCache()
    {
        RecordingAnalysisResultCache cache = new();
        FakeAnalysisStore store = new(deleteImportedGameResult: true);
        DefaultSavedLibraryDataService dataService = new(() => store, cache);

        bool deleted = dataService.DeleteGameAndCachedAnalysis("game-1");

        Assert.True(deleted);
        Assert.Equal("game-1", store.DeletedGameFingerprint);
        Assert.Equal("game-1", cache.RemovedGameFingerprint);
        Assert.Equal(1, cache.RemoveGameCallCount);
    }

    [Fact]
    public void StoreBackedSavedLibraryDataServiceDoesNotRemoveCacheWhenDeleteMisses()
    {
        RecordingAnalysisResultCache cache = new();
        FakeAnalysisStore store = new(deleteImportedGameResult: false);
        StoreBackedSavedLibraryDataService dataService = new(store, cache);

        bool deleted = dataService.DeleteGameAndCachedAnalysis("game-1");

        Assert.False(deleted);
        Assert.Equal("game-1", store.DeletedGameFingerprint);
        Assert.Null(cache.RemovedGameFingerprint);
        Assert.Equal(0, cache.RemoveGameCallCount);
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

        public int RemoveGameCallCount { get; private set; }

        public string? RemovedGameFingerprint { get; private set; }

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

        public void RemoveGame(string gameFingerprint)
        {
            RemoveGameCallCount++;
            RemovedGameFingerprint = gameFingerprint;
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

    private sealed class FakeAnalysisStore(bool deleteImportedGameResult) : IAnalysisStore
    {
        public string? DeletedGameFingerprint { get; private set; }

        public void SaveImportedGame(ImportedGame game)
        {
        }

        public void SaveImportedGames(IReadOnlyList<ImportedGame> games)
        {
        }

        public bool TryLoadImportedGame(string gameFingerprint, out ImportedGame? game)
        {
            game = null;
            return false;
        }

        public bool DeleteImportedGame(string gameFingerprint)
        {
            DeletedGameFingerprint = gameFingerprint;
            return deleteImportedGameResult;
        }

        public IReadOnlyList<SavedImportedGameSummary> ListImportedGames(string? filterText = null, int limit = 200)
            => [];

        public IReadOnlyList<GameAnalysisResult> ListResults(string? filterText = null, int limit = 500)
            => [];

        public bool TryLoadResult(GameAnalysisCacheKey key, out GameAnalysisResult? result)
        {
            result = null;
            return false;
        }

        public void SaveResult(GameAnalysisCacheKey key, GameAnalysisResult result)
        {
        }

        public IReadOnlyList<StoredMoveAnalysis> ListMoveAnalyses(string? filterText = null, int limit = 5000)
            => [];

        public bool TryLoadWindowState(string gameFingerprint, out AnalysisWindowState? state)
        {
            state = null;
            return false;
        }

        public void SaveWindowState(string gameFingerprint, AnalysisWindowState state)
        {
        }
    }
}
