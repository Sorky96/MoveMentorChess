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
    public void AnalysisWindowDataServiceFindsFeedbackUsingInjectedCacheKey()
    {
        RecordingAnalysisResultCache cache = new();
        ImportedGame game = CreateGame();
        EngineAnalysisOptions options = new(Depth: 8, MultiPv: 2);
        GameAnalysisCacheKey expectedKey = new(
            GameFingerprint.Compute(game.PgnText),
            PlayerSide.White,
            options.Depth,
            options.MultiPv,
            options.MoveTimeMs);
        MoveAnalysisResult lead = CreateMoveAnalysisResult(ply: 3);
        MoveAdviceFeedback expected = CreateFeedback(expectedKey, lead);
        FakeAnalysisStore store = new(deleteImportedGameResult: false, [expected]);
        DefaultAnalysisWindowDataService dataService = new(() => store, SystemClock.Instance, cache);

        MoveAdviceFeedback? actual = dataService.FindLatestFeedback(game, PlayerSide.White, options, lead);

        Assert.Same(expected, actual);
        Assert.Equal(1, cache.CreateKeyCallCount);
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

    private static MoveAnalysisResult CreateMoveAnalysisResult(int ply)
    {
        ReplayPly replay = new(
            Ply: ply,
            MoveNumber: 2,
            Side: PlayerSide.White,
            San: "Nf3",
            NormalizedSan: "Nf3",
            Uci: "g1f3",
            FenBefore: "8/8/8/8/8/8/8/8 w - - 0 1",
            FenAfter: "8/8/8/8/8/8/8/8 b - - 0 1",
            PlacementFenBefore: "8/8/8/8/8/8/8/8 w - - 0 1",
            PlacementFenAfter: "8/8/8/8/8/8/8/8 b - - 0 1",
            Phase: GamePhase.Opening,
            MovingPiece: "N",
            PromotionPiece: null,
            FromSquare: "g1",
            ToSquare: "f3",
            IsCapture: false,
            IsEnPassant: false,
            IsCastle: false);
        EngineAnalysis before = new(replay.FenBefore, [new EngineLine("g1f3", 40, null, ["g1f3"])], "g1f3");
        EngineAnalysis after = new(replay.FenAfter, [new EngineLine("g1f3", 10, null, ["g1f3"])], "g1f3");

        return new MoveAnalysisResult(
            replay,
            before,
            after,
            EvalBeforeCp: 40,
            EvalAfterCp: 10,
            BestMateIn: null,
            PlayedMateIn: null,
            CentipawnLoss: 30,
            MoveQualityBucket.Inaccuracy,
            MaterialDeltaCp: 0,
            MistakeTag: new MistakeTag("piece_activity", 0.8, []),
            Explanation: null);
    }

    private static MoveAdviceFeedback CreateFeedback(GameAnalysisCacheKey key, MoveAnalysisResult lead)
        => new(
            "feedback-1",
            DateTime.UtcNow,
            key.GameFingerprint,
            key.Side,
            key.Depth,
            key.MultiPv,
            key.MoveTimeMs,
            lead.Replay.Ply,
            lead.Replay.MoveNumber,
            lead.Replay.San,
            lead.Replay.Uci,
            lead.Replay.FenBefore,
            lead.Replay.FenAfter,
            lead.EvalBeforeCp,
            lead.EvalAfterCp,
            lead.BeforeAnalysis.BestMoveUci,
            "piece_activity",
            0.8,
            [],
            lead.Quality,
            lead.CentipawnLoss,
            AdviceFeedbackKind.Correct,
            CorrectedLabel: null,
            Comment: null,
            "analysis-window");

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

    private sealed class FakeAnalysisStore(
        bool deleteImportedGameResult,
        IReadOnlyList<MoveAdviceFeedback>? feedback = null) : IAnalysisStore
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

        public IReadOnlyList<MoveAdviceFeedback> ListMoveAdviceFeedback(string? filterText = null, int limit = 5000)
            => feedback ?? [];
    }
}
