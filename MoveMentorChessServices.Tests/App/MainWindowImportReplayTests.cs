using MoveMentorChess.App.Composition;
using MoveMentorChess.App.ViewModels;
using MoveMentorChess.Analysis;
using MoveMentorChess.Engine;
using MoveMentorChess.Opening;
using MoveMentorChess.Persistence;
using Xunit;

namespace MoveMentorChessServices.Tests.App;

public sealed class MainWindowImportReplayTests
{
    private const string FourPlyPgn = """
[Event "Import replay test"]
[Site "Local"]
[Date "2026.06.14"]
[White "Alice"]
[Black "Bob"]
[Result "*"]
[ECO "C20"]

1. e4 e5 2. Nf3 Nc6 *
""";

    private const string QueenPawnPgn = """
[Event "Import replay test 2"]
[Site "Local"]
[Date "2026.06.15"]
[White "Carol"]
[Black "Drew"]
[Result "*"]
[ECO "D06"]

1. d4 d5 2. c4 e6 *
""";

    [Fact]
    public void ImportPgn_LoadsReplayAndSavesGame()
    {
        RecordingMainWindowAnalysisDataService dataService = new();
        MainWindowViewModel viewModel = CreateViewModel(dataService);

        viewModel.ImportPgn(FourPlyPgn);

        Assert.True(viewModel.HasImportedGame);
        Assert.Equal(4, viewModel.ImportedMoves.Count);
        Assert.Equal("Imported 4 plies from PGN.", viewModel.StatusMessage);
        Assert.Contains("Alice", viewModel.ImportedGameSummary, StringComparison.Ordinal);
        Assert.Contains("Bob", viewModel.ImportedGameSummary, StringComparison.Ordinal);
        Assert.Single(dataService.SavedGames);
    }

    [Fact]
    public void ImportPgnGames_SkipsUnreplayableGamesAndShowsFirstReplayableGame()
    {
        RecordingMainWindowAnalysisDataService dataService = new();
        MainWindowViewModel viewModel = CreateViewModel(dataService);
        ImportedGame zeroPlyGame = CreateZeroPlyGame();
        ImportedGame invalidGame = CreateInvalidReplayGame();
        ImportedGame validGame = PgnGameParser.Parse(FourPlyPgn);
        PgnBatchParseResult parseResult = new(
            [zeroPlyGame, invalidGame, validGame],
            [new PgnBatchParseError(3, "Bad SAN")]);

        PgnFileImportResult result = viewModel.ImportPgnGames(parseResult);

        Assert.Equal(3, result.ImportedGames);
        Assert.Equal(3, result.SkippedGames);
        Assert.Same(parseResult.Games, result.Games);
        Assert.Equal(4, viewModel.ImportedMoves.Count);
        Assert.Contains("Skipped 3", viewModel.StatusMessage, StringComparison.Ordinal);
        Assert.Single(dataService.SavedGameBatches);
        Assert.Equal(3, dataService.SavedGameBatches[0].Count);
    }

    [Fact]
    public void ImportPgnGames_ClearsCachedAnalysisWhenBatchLoadsAnotherGame()
    {
        MainWindowViewModel viewModel = CreateViewModel(new RecordingMainWindowAnalysisDataService());
        ImportedGame firstGame = PgnGameParser.Parse(FourPlyPgn);
        ImportedGame secondGame = PgnGameParser.Parse(QueenPawnPgn);
        viewModel.ImportPgn(FourPlyPgn);
        viewModel.LoadAnalysisResult(CreateAnalysisResultWithBlunder(firstGame));

        Assert.Single(viewModel.AnalysisMistakes);

        viewModel.ImportPgnGames(new PgnBatchParseResult([secondGame], []));
        viewModel.SelectedAnalysisFilter = "Blunder";

        Assert.Empty(viewModel.AnalysisMistakes);
        Assert.All(viewModel.ImportedMoves, item => Assert.False(item.HasAnalysisLabel));
    }

    [Fact]
    public void ImportPgn_WhenReplayFailsReportsParseFailureAndDoesNotReplaceState()
    {
        RecordingMainWindowAnalysisDataService dataService = new();
        MainWindowViewModel viewModel = CreateViewModel(dataService);

        viewModel.ImportPgn("1. e5 *");

        Assert.False(viewModel.HasImportedGame);
        Assert.Empty(viewModel.ImportedMoves);
        Assert.StartsWith("Could not parse PGN:", viewModel.StatusMessage, StringComparison.Ordinal);
        Assert.Empty(dataService.SavedGames);
    }

    [Fact]
    public void ReplayCommandsAdvanceBoardSelectionAndCanExecuteState()
    {
        MainWindowViewModel viewModel = CreateViewModel(new RecordingMainWindowAnalysisDataService());
        ImportedGame game = PgnGameParser.Parse(FourPlyPgn);
        IReadOnlyList<ReplayPly> replay = new GameReplayService().Replay(game);
        viewModel.ImportPgn(FourPlyPgn);

        Assert.True(viewModel.ApplyNextImportedMoveCommand.CanExecute(null));
        Assert.False(viewModel.ApplySelectedImportedMoveCommand.CanExecute(null));

        viewModel.ApplyNextImportedMoveCommand.Execute(null);

        Assert.Equal(replay[0].FenAfter, viewModel.BoardFen);
        Assert.Equal(viewModel.ImportedMoves[0], viewModel.SelectedImportedMove);
        Assert.True(viewModel.ApplySelectedImportedMoveCommand.CanExecute(null));

        viewModel.ApplyNextImportedMoveCommand.Execute(null);
        viewModel.ApplyNextImportedMoveCommand.Execute(null);
        viewModel.ApplyNextImportedMoveCommand.Execute(null);

        Assert.Equal(replay[3].FenAfter, viewModel.BoardFen);
        Assert.Equal(viewModel.ImportedMoves[3], viewModel.SelectedImportedMove);
        Assert.False(viewModel.ApplyNextImportedMoveCommand.CanExecute(null));
    }

    [Fact]
    public void ApplySelectedImportedMoveProjectsSelectedReplayPlyToBoard()
    {
        MainWindowViewModel viewModel = CreateViewModel(new RecordingMainWindowAnalysisDataService());
        ImportedGame game = PgnGameParser.Parse(FourPlyPgn);
        IReadOnlyList<ReplayPly> replay = new GameReplayService().Replay(game);
        viewModel.ImportPgn(FourPlyPgn);

        viewModel.SelectedImportedMove = viewModel.ImportedMoves[2];
        viewModel.ApplySelectedImportedMoveCommand.Execute(null);

        Assert.Equal(replay[2].FenAfter, viewModel.BoardFen);
        Assert.Equal(viewModel.ImportedMoves[2], viewModel.SelectedImportedMove);
        Assert.Contains("Moved board to", viewModel.StatusMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void ImportPgn_StillSucceedsWhenProductionPersistenceSaveFails()
    {
        ThrowingAnalysisStore store = new();
        DefaultMainWindowAnalysisDataService dataService = new(() => store, new RecordingAnalysisResultCache());
        MainWindowViewModel viewModel = CreateViewModel(dataService);

        viewModel.ImportPgn(FourPlyPgn);

        Assert.True(viewModel.HasImportedGame);
        Assert.Equal(4, viewModel.ImportedMoves.Count);
        Assert.Equal("Imported 4 plies from PGN.", viewModel.StatusMessage);
        Assert.Equal(1, store.SaveImportedGameCallCount);
    }

    [Fact]
    public async Task AnalyzeImportedGamesAsync_UsesInjectedEngineSessionAndAnalysisWorkflow()
    {
        RecordingMainWindowAnalysisDataService dataService = new();
        RecordingEngineAnalyzer engineAnalyzer = new();
        RecordingMainWindowAnalysisWorkflow analysisWorkflow = new();
        MainWindowViewModel viewModel = new(
            new RecordingMainWindowEngineSession(engineAnalyzer),
            dataService,
            analysisWorkflow);
        ImportedGame game = PgnGameParser.Parse(FourPlyPgn);

        BulkPgnAnalysisResult result = await viewModel.AnalyzeImportedGamesAsync([game, game]);

        Assert.Equal(2, result.AnalyzedGames);
        Assert.Same(engineAnalyzer, analysisWorkflow.LastEngineAnalyzer);
        Assert.Equal(1, analysisWorkflow.RunCreationCount);
        Assert.Equal([game, game], analysisWorkflow.AnalyzedGames);
        Assert.All(analysisWorkflow.UsedOptions, options => Assert.Equal(analysisWorkflow.BulkOptions, options));
        Assert.Equal(2, dataService.StoredResults.Count);
        Assert.Equal(game, dataService.StoredResults[0].Game);
        Assert.Equal(PlayerSide.White, dataService.StoredResults[0].Side);
    }

    private static MainWindowViewModel CreateViewModel(IMainWindowAnalysisDataService dataService)
        => new(new MissingStockfishPathResolver(), dataService);

    private static ImportedGame CreateInvalidReplayGame()
        => new(
            PgnText: "1. e5 *",
            SanMoves: ["e5"],
            WhitePlayer: "Invalid",
            BlackPlayer: "Replay",
            WhiteElo: null,
            BlackElo: null,
            DateText: null,
            Result: "*",
            Eco: null,
            Site: null);

    private static ImportedGame CreateZeroPlyGame()
        => new(
            PgnText: """
[Event "No moves"]
[White "No"]
[Black "Moves"]
[Result "*"]

*
""",
            SanMoves: [],
            WhitePlayer: "No",
            BlackPlayer: "Moves",
            WhiteElo: null,
            BlackElo: null,
            DateText: null,
            Result: "*",
            Eco: null,
            Site: null);

    private static GameAnalysisResult CreateAnalysisResultWithBlunder(ImportedGame game, PlayerSide side = PlayerSide.White)
    {
        ReplayPly replay = new GameReplayService().Replay(game)[0];
        EngineAnalysis before = new(
            replay.FenBefore,
            [new EngineLine(replay.Uci, 40, null, [replay.Uci])],
            replay.Uci);
        EngineAnalysis after = new(
            replay.FenAfter,
            [new EngineLine(replay.Uci, -220, null, [replay.Uci])],
            replay.Uci);
        MoveAnalysisResult move = new(
            replay,
            before,
            after,
            EvalBeforeCp: 40,
            EvalAfterCp: -220,
            BestMateIn: null,
            PlayedMateIn: null,
            CentipawnLoss: 260,
            MoveQualityBucket.Blunder,
            MaterialDeltaCp: 0,
            MistakeTag: new MistakeTag("hanging_piece", 0.9, []),
            Explanation: new MoveExplanation("short", "hint"));
        SelectedMistake mistake = new(
            [move],
            MoveQualityBucket.Blunder,
            move.MistakeTag,
            move.Explanation!);

        return new GameAnalysisResult(game, side, [replay], [move], [mistake]);
    }

    private sealed class MissingStockfishPathResolver : IStockfishPathResolver
    {
        public string? Resolve() => null;
    }

    private sealed class RecordingMainWindowAnalysisDataService : IMainWindowAnalysisDataService
    {
        public List<ImportedGame> SavedGames { get; } = [];

        public List<IReadOnlyList<ImportedGame>> SavedGameBatches { get; } = [];

        public List<(ImportedGame Game, PlayerSide Side, EngineAnalysisOptions Options, GameAnalysisResult Result)> StoredResults { get; } = [];

        public void SaveImportedGame(ImportedGame game)
        {
            SavedGames.Add(game);
        }

        public void SaveImportedGames(IReadOnlyList<ImportedGame> games)
        {
            SavedGameBatches.Add(games);
        }

        public bool TryLoadImportedGame(string gameFingerprint, out ImportedGame? game)
        {
            game = null;
            return false;
        }

        public bool TryGetCachedAnalysis(ImportedGame game, PlayerSide side, EngineAnalysisOptions options, out GameAnalysisResult? result)
        {
            result = null;
            return false;
        }

        public void StoreAnalysisResult(ImportedGame game, PlayerSide side, EngineAnalysisOptions options, GameAnalysisResult result)
        {
            StoredResults.Add((game, side, options, result));
        }

        public IPlayerMistakeProfileSource? CreatePlayerMistakeProfileSource() => null;

        public OpeningTheoryQueryService? CreateOpeningTheory() => null;
    }

    private sealed class RecordingMainWindowEngineSession(IEngineAnalyzer analyzer) : IMainWindowEngineSession
    {
        public bool IsAvailable => true;

        public IEngineAnalyzer? Analyzer => analyzer;

        public string Reload() => "Engine ready.";

        public MainWindowEngineSummary RefreshSummary(string fen)
            => new([], null);

        public EngineAnalysis AnalyzePosition(string fen, EngineAnalysisOptions options)
            => analyzer.AnalyzePosition(fen, options);

        public void Dispose()
        {
        }
    }

    private sealed class RecordingMainWindowAnalysisWorkflow : IMainWindowAnalysisWorkflow
    {
        public EngineAnalysisOptions BulkOptions { get; } = new(Depth: 9, MultiPv: 2, MoveTimeMs: 120);

        public IEngineAnalyzer? LastEngineAnalyzer { get; private set; }

        public int RunCreationCount { get; private set; }

        public List<ImportedGame> AnalyzedGames { get; } = [];

        public List<EngineAnalysisOptions> UsedOptions { get; } = [];

        public EngineAnalysisOptions CreateDefaultAnalysisOptions()
            => new();

        public EngineAnalysisOptions CreateBulkAnalysisOptions()
            => BulkOptions;

        public IMainWindowAnalysisRun CreateAnalysisRun(IEngineAnalyzer engineAnalyzer)
        {
            LastEngineAnalyzer = engineAnalyzer;
            RunCreationCount++;
            return new RecordingMainWindowAnalysisRun(this);
        }

        private sealed class RecordingMainWindowAnalysisRun(RecordingMainWindowAnalysisWorkflow owner) : IMainWindowAnalysisRun
        {
            public Task<GameAnalysisResult> AnalyzeGameAsync(
                ImportedGame game,
                PlayerSide side,
                EngineAnalysisOptions options,
                IProgress<GameAnalysisProgress>? progress,
                CancellationToken cancellationToken = default)
            {
                owner.AnalyzedGames.Add(game);
                owner.UsedOptions.Add(options);
                return Task.FromResult(CreateAnalysisResultWithBlunder(game, side));
            }

            public void Dispose()
            {
            }
        }
    }

    private sealed class RecordingEngineAnalyzer : IEngineAnalyzer
    {
        public EngineAnalysis AnalyzePosition(string fen, EngineAnalysisOptions options)
            => new(fen, [new EngineLine("e2e4", 20, null, ["e2e4"])], "e2e4");
    }

    private sealed class RecordingAnalysisResultCache : IAnalysisResultCache
    {
        public GameAnalysisCacheKey CreateKey(ImportedGame game, PlayerSide side, EngineAnalysisOptions options)
            => new(GameFingerprint.Compute(game.PgnText), side, options.Depth, options.MultiPv, options.MoveTimeMs);

        public bool TryGetResult(GameAnalysisCacheKey key, out GameAnalysisResult? result)
        {
            result = null;
            return false;
        }

        public void StoreResult(GameAnalysisCacheKey key, GameAnalysisResult result)
        {
        }

        public void RemoveGame(string gameFingerprint)
        {
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

    private sealed class ThrowingAnalysisStore : IAnalysisStore
    {
        public int SaveImportedGameCallCount { get; private set; }

        public void SaveImportedGame(ImportedGame game)
        {
            SaveImportedGameCallCount++;
            throw new InvalidOperationException("Storage is unavailable.");
        }

        public void SaveImportedGames(IReadOnlyList<ImportedGame> games)
        {
            throw new InvalidOperationException("Storage is unavailable.");
        }

        public bool TryLoadImportedGame(string gameFingerprint, out ImportedGame? game)
        {
            game = null;
            return false;
        }

        public bool DeleteImportedGame(string gameFingerprint) => false;

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

        public IReadOnlyList<MoveAdviceFeedback> ListMoveAdviceFeedback(string? filterText = null, int limit = 5000)
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
