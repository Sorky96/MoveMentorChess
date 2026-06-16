using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using Avalonia.Media;
using MoveMentorChess.App.Composition;
using MoveMentorChess.Localization;
using MoveMentorChess.Opening;
using MoveMentorChess.Persistence;

namespace MoveMentorChess.App.ViewModels;

public sealed class MainWindowViewModel : ViewModelBase, IDisposable
{
    private static readonly IReadOnlyList<string> AnalysisFilterOptions = ["All", "Blunder", "Mistake", "Inaccuracy"];
    private static readonly IReadOnlyList<PlayerSide> AnalysisSides = [PlayerSide.White, PlayerSide.Black];

    private readonly IMainWindowEngineSession engineSession;
    private readonly IMainWindowAnalysisDataService analysisDataService;
    private readonly IMainWindowAnalysisWorkflow analysisWorkflow;
    private readonly ChessGame chessGame = new();
    private readonly Stack<string> undoFenStack = new();
    private readonly HashSet<string> availableTargets = new(StringComparer.Ordinal);
    private readonly ImportedGameReplayController importedGameReplay = new();

    private readonly Dictionary<PlayerSide, GameAnalysisResult> cachedAnalysisResultsBySide = new();
    private GameAnalysisResult? cachedAnalysisResult;
    private Func<IReadOnlyList<LegalMoveInfo>, Task<LegalMoveInfo?>>? promotionMoveSelector;
    private string? selectedSquare;
    private string? previewTargetSquare;
    private string statusMessage = Localizer.Text(LocalizedStrings.MainReady);
    private string importedGameSummary = Localizer.Text(LocalizedStrings.MainImportedMovesNone);
    private string suggestionText = Localizer.Text(LocalizedStrings.MainEngineSuggestionsUnavailable);
    private string evaluationText = Localizer.Text(LocalizedStrings.MainEvaluationUnavailable);
    private string analysisDetails = Localizer.Text(LocalizedStrings.MainAnalysisPlaceholder);
    private string pieceMoveOptionsHeader = Localizer.Text(LocalizedStrings.MainSelectedPieceNone);
    private PieceMoveOptionViewModel? selectedPieceMoveOption;
    private bool rotateBoard;
    private bool isBusy;
    private CancellationTokenSource? importCancellationTokenSource;
    private int evaluationBarValue = 50;
    private string selectedAnalysisFilter = AnalysisFilterOptions[0];
    private PlayerSide selectedAnalysisSide = PlayerSide.White;
    private AnalysisMistakeItemViewModel? selectedAnalysisMistake;

    public MainWindowViewModel()
        : this(
            new DefaultMainWindowEngineSession(new DefaultStockfishPathResolver()),
            new DefaultMainWindowAnalysisDataService(() => null))
    {
    }

    public MainWindowViewModel(
        IStockfishPathResolver stockfishPathResolver,
        Func<IAnalysisStore?> analysisStoreProvider)
        : this(
            new DefaultMainWindowEngineSession(stockfishPathResolver),
            new DefaultMainWindowAnalysisDataService(analysisStoreProvider))
    {
    }

    internal MainWindowViewModel(
        IStockfishPathResolver stockfishPathResolver,
        IMainWindowAnalysisDataService analysisDataService)
        : this(new DefaultMainWindowEngineSession(stockfishPathResolver), analysisDataService)
    {
    }

    internal MainWindowViewModel(
        IMainWindowEngineSession engineSession,
        IMainWindowAnalysisDataService analysisDataService)
        : this(engineSession, analysisDataService, new DefaultMainWindowAnalysisWorkflow(analysisDataService))
    {
    }

    internal MainWindowViewModel(
        IMainWindowEngineSession engineSession,
        IMainWindowAnalysisDataService analysisDataService,
        IMainWindowAnalysisWorkflow analysisWorkflow)
    {
        this.engineSession = engineSession ?? throw new ArgumentNullException(nameof(engineSession));
        this.analysisDataService = analysisDataService ?? throw new ArgumentNullException(nameof(analysisDataService));
        this.analysisWorkflow = analysisWorkflow ?? throw new ArgumentNullException(nameof(analysisWorkflow));
        UndoCommand = new RelayCommand(UndoLastMove, () => undoFenStack.Count > 0 && !IsBusy);
        RotateBoardCommand = new RelayCommand(ToggleBoardRotation, () => !IsBusy);
        ApplyNextImportedMoveCommand = new RelayCommand(ApplyNextImportedMove, () => !IsBusy && importedGameReplay.CanApplyNextMove);
        ApplySelectedImportedMoveCommand = new RelayCommand(ApplySelectedImportedMove, () => !IsBusy && SelectedImportedMove is not null);
        AnalyzeImportedGameCommand = new RelayCommand(async () => await AnalyzeImportedGameAsync(), () => !IsBusy && importedGameReplay.Game is not null && engineSession.IsAvailable);
        StopImportCommand = new RelayCommand(StopImport, () => IsImportCancellationAvailable);
        ShowSelectedMistakeOnBoardCommand = new RelayCommand(ShowSelectedMistakeOnBoard, () => !IsBusy && SelectedAnalysisMistake?.LeadMove is not null);

        TryInitializeEngine();
        ClearPieceMoveOptions();
        RefreshBoard();
        RefreshEngineSummary();
    }

    public ObservableCollection<ImportedMoveItemViewModel> ImportedMoves => importedGameReplay.ImportedMoves;

    public ObservableCollection<AnalysisMistakeItemViewModel> AnalysisMistakes { get; } = [];

    public ObservableCollection<PieceMoveOptionViewModel> PieceMoveOptions { get; } = [];

    public IReadOnlyList<string> AvailableAnalysisFilters => AnalysisFilterOptions;

    public IReadOnlyList<PlayerSide> AvailableAnalysisSides => AnalysisSides;

    public RelayCommand UndoCommand { get; }

    public RelayCommand RotateBoardCommand { get; }

    public RelayCommand ApplyNextImportedMoveCommand { get; }

    public RelayCommand ApplySelectedImportedMoveCommand { get; }

    public RelayCommand AnalyzeImportedGameCommand { get; }

    public RelayCommand StopImportCommand { get; }

    public RelayCommand ShowSelectedMistakeOnBoardCommand { get; }

    public string StatusMessage
    {
        get => statusMessage;
        private set => SetProperty(ref statusMessage, value);
    }

    public string ImportedGameSummary
    {
        get => importedGameSummary;
        private set => SetProperty(ref importedGameSummary, value);
    }

    public string SuggestionText
    {
        get => suggestionText;
        private set => SetProperty(ref suggestionText, value);
    }

    public string EvaluationText
    {
        get => evaluationText;
        private set => SetProperty(ref evaluationText, value);
    }

    public string AnalysisDetails
    {
        get => analysisDetails;
        private set => SetProperty(ref analysisDetails, value);
    }

    public string PieceMoveOptionsHeader
    {
        get => pieceMoveOptionsHeader;
        private set => SetProperty(ref pieceMoveOptionsHeader, value);
    }

    public string BoardFen => chessGame.GetFen();

    public IReadOnlyList<string> AvailableMoveSquares => availableTargets.ToList();

    public IReadOnlyList<BoardArrowViewModel> BestMoveArrows { get; private set; } = [];

    public string? SelectedSquareName => selectedSquare;

    public string? PreviewTargetSquare
    {
        get => previewTargetSquare;
        private set => SetProperty(ref previewTargetSquare, value);
    }

    public bool RotateBoard
    {
        get => rotateBoard;
        private set => SetProperty(ref rotateBoard, value);
    }

    public bool IsBusy
    {
        get => isBusy;
        private set
        {
            if (SetProperty(ref isBusy, value))
            {
                OnPropertyChanged(nameof(CanOpenImportedAnalysis));
                OnPropertyChanged(nameof(IsImportCancellationAvailable));
                RaiseCommandStates();
            }
        }
    }

    public bool CanOpenImportedAnalysis => importedGameReplay.Game is not null && engineSession.IsAvailable && !IsBusy;

    public bool IsImportCancellationAvailable => IsBusy && importCancellationTokenSource is not null && !importCancellationTokenSource.IsCancellationRequested;

    public bool IsEngineAvailable => engineSession.IsAvailable;

    public bool IsEngineUnavailable => !engineSession.IsAvailable;

    public bool HasImportedGame => importedGameReplay.HasReplayableGame;

    public string PrimaryNextStepTitle
        => IsEngineUnavailable
            ? Localizer.Text(LocalizedStrings.MainPrimarySetupStockfish)
            : HasImportedGame
                ? Localizer.Text(LocalizedStrings.MainPrimaryRunAnalysis)
                : Localizer.Text(LocalizedStrings.MainPrimaryLoadPgn);

    public string PrimaryNextStepDescription
        => IsEngineUnavailable
            ? Localizer.Text(LocalizedStrings.MainPrimarySetupStockfishDescription)
            : HasImportedGame
                ? Localizer.Text(LocalizedStrings.MainPrimaryRunAnalysisDescription)
                : Localizer.Text(LocalizedStrings.MainPrimaryLoadPgnDescription);

    public int EvaluationBarValue
    {
        get => evaluationBarValue;
        private set => SetProperty(ref evaluationBarValue, value);
    }

    public string SelectedAnalysisFilter
    {
        get => selectedAnalysisFilter;
        set
        {
            if (SetProperty(ref selectedAnalysisFilter, value))
            {
                ApplyAnalysisFilter();
            }
        }
    }

    public PlayerSide SelectedAnalysisSide
    {
        get => selectedAnalysisSide;
        set
        {
            if (SetProperty(ref selectedAnalysisSide, value))
            {
                cachedAnalysisResultsBySide.TryGetValue(value, out cachedAnalysisResult);
                ApplyAnalysisFilter();
            }
        }
    }

    public ImportedMoveItemViewModel? SelectedImportedMove
    {
        get => importedGameReplay.SelectedMove;
        set
        {
            if (importedGameReplay.SetSelectedMove(value))
            {
                OnPropertyChanged();
                ApplySelectedImportedMoveCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public PieceMoveOptionViewModel? SelectedPieceMoveOption
    {
        get => selectedPieceMoveOption;
        set
        {
            if (SetProperty(ref selectedPieceMoveOption, value))
            {
                PreviewTargetSquare = string.IsNullOrWhiteSpace(value?.ToSquare) ? null : value.ToSquare;
                RefreshBoard();
            }
        }
    }

    public AnalysisMistakeItemViewModel? SelectedAnalysisMistake
    {
        get => selectedAnalysisMistake;
        set
        {
            if (SetProperty(ref selectedAnalysisMistake, value))
            {
                AnalysisDetails = value?.Details ?? "Select a mistake to inspect its details.";
                ShowSelectedMistakeOnBoardCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public void Dispose()
    {
        engineSession.Dispose();
    }

    public void ReloadEngineSettings()
    {
        TryInitializeEngine();
        RefreshEngineSummary();
    }

    public void SetPromotionMoveSelector(Func<IReadOnlyList<LegalMoveInfo>, Task<LegalMoveInfo?>> selector)
    {
        promotionMoveSelector = selector;
    }

    public async Task HandleSquareClickAsync(string? squareName)
    {
        if (string.IsNullOrWhiteSpace(squareName) || IsBusy)
        {
            return;
        }

        if (!TryParseSquare(squareName, out (int X, int Y) point))
        {
            return;
        }

        string? piece = GetPieceAt(point.X, point.Y);
        if (selectedSquare is null)
        {
            if (string.IsNullOrEmpty(piece))
            {
                return;
            }

            bool isWhitePiece = char.IsUpper(piece[0]);
            if (isWhitePiece != chessGame.WhiteToMove)
            {
                return;
            }

            selectedSquare = squareName;
            List<LegalMoveInfo> movesForPiece = chessGame.GetLegalMoves()
                .Where(move => move.FromSquare == squareName)
                .ToList();
            availableTargets.Clear();
            foreach (LegalMoveInfo move in movesForPiece)
            {
                availableTargets.Add(move.ToSquare);
            }

            UpdatePieceMoveOptions(squareName, piece, movesForPiece);
            RefreshBoard();
            return;
        }

        if (selectedSquare == squareName)
        {
            ClearSelection();
            return;
        }

        List<LegalMoveInfo> matchingMoves = chessGame.GetLegalMoves()
            .Where(move => move.FromSquare == selectedSquare && move.ToSquare == squareName)
            .ToList();

        if (matchingMoves.Count == 0)
        {
            ClearSelection();
            StatusMessage = $"Move {selectedSquare}-{squareName} is not legal in the current position.";
            return;
        }

        string? uci = await SelectMoveToApplyAsync(matchingMoves);
        if (string.IsNullOrWhiteSpace(uci))
        {
            StatusMessage = "Promotion was canceled.";
            RefreshBoard();
            return;
        }

        string previousFen = chessGame.GetFen();
        if (!chessGame.TryApplyUci(uci, out _, out string? error))
        {
            StatusMessage = error ?? "Could not apply the selected move.";
            ClearSelection();
            return;
        }

        undoFenStack.Push(previousFen);
        importedGameReplay.ClampCursorToReplayEnd();
        ClearSelection();
        RefreshBoard();
        RefreshEngineSummary();
        RefreshImportedSummary();
        StatusMessage = $"Applied {uci}.";
        RaiseCommandStates();
    }

    public void ImportPgn(string pgnText)
    {
        if (IsBusy || string.IsNullOrWhiteSpace(pgnText))
        {
            return;
        }

        try
        {
            ImportedGame parsedGame = PgnGameParser.Parse(pgnText);
            LoadImportedGameCore(parsedGame);
            SaveImportedGame(parsedGame);
            StatusMessage = importedGameReplay.ReplayCount == 0
                ? "PGN loaded, but no SAN moves were found."
                : $"Imported {importedGameReplay.ReplayCount} plies from PGN.";
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            StatusMessage = $"Could not parse PGN: {ex.Message}";
        }
        finally
        {
            RaiseCommandStates();
        }
    }

    public PgnFileImportResult ImportPgnGames(PgnBatchParseResult parseResult)
    {
        ArgumentNullException.ThrowIfNull(parseResult);

        if (IsBusy)
        {
            return new PgnFileImportResult(0, parseResult.Errors.Count, []);
        }

        int skippedGames = parseResult.Errors.Count;
        if (parseResult.Games.Count == 0)
        {
            StatusMessage = skippedGames == 0
                ? "PGN file did not contain any games."
                : $"PGN file did not contain any parsed games. Skipped {skippedGames}.";
            RaiseCommandStates();
            return new PgnFileImportResult(0, skippedGames, []);
        }

        SaveImportedGames(parseResult.Games);
        if (!importedGameReplay.TryLoadFirstReplayableGame(parseResult.Games, out int replaySkippedGames))
        {
            skippedGames += replaySkippedGames;
            StatusMessage = $"PGN file contained {parseResult.Games.Count} parsed games, but none could be replayed.";
            RaiseCommandStates();
            return new PgnFileImportResult(0, skippedGames, []);
        }

        AfterImportedGameLoaded();
        skippedGames += replaySkippedGames;
        StatusMessage = skippedGames == 0
            ? $"Loaded {parseResult.Games.Count} games from PGN file. Showing the first game."
            : $"Loaded {parseResult.Games.Count} games from PGN file. Skipped {skippedGames}. Showing the first replayable game.";
        return new PgnFileImportResult(parseResult.Games.Count, skippedGames, parseResult.Games);
    }

    public void LoadImportedGame(ImportedGame game)
    {
        if (IsBusy)
        {
            return;
        }

        LoadImportedGameCore(game);
        StatusMessage = importedGameReplay.ReplayCount == 0
            ? "Saved game loaded, but it does not contain SAN moves."
            : $"Loaded saved game with {importedGameReplay.ReplayCount} plies.";
        OnPropertyChanged(nameof(CanOpenImportedAnalysis));
        OnPrimaryNextStepChanged();
    }

    public void LoadAnalysisResult(GameAnalysisResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        if (!IsCurrentImportedGame(result.Game))
        {
            LoadImportedGameCore(result.Game);
        }

        cachedAnalysisResultsBySide[result.AnalyzedSide] = result;
        LoadCachedAnalysisResultsForCurrentGame(new EngineAnalysisOptions());
        SelectedAnalysisSide = result.AnalyzedSide;
        cachedAnalysisResult = cachedAnalysisResultsBySide.TryGetValue(result.AnalyzedSide, out GameAnalysisResult? selectedResult)
            ? selectedResult
            : result;
        ApplyAnalysisToImportedMoves();
        ApplyAnalysisFilter();
        StatusMessage = $"Loaded analysis for {result.AnalyzedSide}.";
    }

    public async Task<BulkPgnAnalysisResult> AnalyzeImportedGamesAsync(IReadOnlyList<ImportedGame> games)
    {
        if (IsBusy || engineSession.Analyzer is not { } engineAnalyzer || games.Count == 0)
        {
            return new BulkPgnAnalysisResult(DetectPrimaryPlayer(games), 0, 0, 0, 0, []);
        }

        string? primaryPlayer = DetectPrimaryPlayer(games);
        int analyzed = 0;
        int cached = 0;
        int failed = 0;
        int skipped = 0;
        List<string> failureMessages = [];

        try
        {
            using CancellationTokenSource cancellation = new();
            importCancellationTokenSource = cancellation;
            IsBusy = true;
            OnPropertyChanged(nameof(IsImportCancellationAvailable));
            AnalysisMistakes.Clear();
            SelectedAnalysisMistake = null;
            AnalysisDetails = string.IsNullOrWhiteSpace(primaryPlayer)
                ? "The analysis engine is reviewing the imported PGN file. This may take a while."
                : $"The analysis engine is reviewing games for {primaryPlayer}. This may take a while.";

            EngineAnalysisOptions options = analysisWorkflow.CreateBulkAnalysisOptions();
            GameAnalysisResult? lastResult = null;

            foreach (ImportedGame game in games)
            {
                cancellation.Token.ThrowIfCancellationRequested();
                PlayerSide side = ResolveAnalysisSide(game, primaryPlayer, SelectedAnalysisSide);
                if (!PlayerMatchesSide(game, primaryPlayer, side))
                {
                    skipped++;
                    continue;
                }

                SelectedAnalysisSide = side;
                string bulkStatus = BuildBulkAnalysisStatus(game, side, analyzed + cached + failed + skipped + 1, games.Count, primaryPlayer);
                StatusMessage = bulkStatus;

                if (analysisDataService.TryGetCachedAnalysis(game, side, options, out GameAnalysisResult? cachedResult) && cachedResult is not null)
                {
                    cached++;
                    lastResult = cachedResult;
                    continue;
                }

                try
                {
                    LoadImportedGameCore(game);
                    SelectedAnalysisSide = side;
                    IProgress<GameAnalysisProgress> progress = new Progress<GameAnalysisProgress>(
                        item => ShowAnalysisProgressOnBoard(item, bulkStatus));

                    GameAnalysisResult result = await analysisWorkflow.AnalyzeGameAsync(
                        engineAnalyzer,
                        game,
                        side,
                        options,
                        progress,
                        cancellation.Token);
                    analysisDataService.StoreAnalysisResult(game, side, options, result);
                    analyzed++;
                    lastResult = result;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex) when (ex is not OutOfMemoryException)
                {
                    failed++;
                    if (failureMessages.Count < 5)
                    {
                        failureMessages.Add(BuildAnalysisFailureMessage(game, side, ex));
                    }
                }
            }

            if (lastResult is not null)
            {
                LoadImportedGameCore(lastResult.Game);
                SelectedAnalysisSide = lastResult.AnalyzedSide;
                cachedAnalysisResultsBySide[lastResult.AnalyzedSide] = lastResult;
                cachedAnalysisResult = lastResult;
                ApplyAnalysisToImportedMoves();
                ApplyAnalysisFilter();
            }

            string playerText = string.IsNullOrWhiteSpace(primaryPlayer) ? string.Empty : $" for {primaryPlayer}";
            StatusMessage = $"Bulk analysis finished{playerText}. New: {analyzed}, cached: {cached}, skipped: {skipped}, failed: {failed}.";
        }
        catch (OperationCanceledException)
        {
            StatusMessage = $"Bulk analysis stopped. New: {analyzed}, cached: {cached}, skipped: {skipped}, failed: {failed}.";
        }
        finally
        {
            IsBusy = false;
            importCancellationTokenSource = null;
            OnPropertyChanged(nameof(IsImportCancellationAvailable));
            RefreshEngineSummary();
            RefreshImportedSummary();
        }

        return new BulkPgnAnalysisResult(primaryPlayer, analyzed, cached, skipped, failed, failureMessages);
    }

    public async Task NavigateToProfileExampleAsync(ProfileMistakeExample example)
    {
        if (IsBusy)
        {
            return;
        }

        if (!analysisDataService.TryLoadImportedGame(example.GameFingerprint, out ImportedGame? game) || game is null)
        {
            StatusMessage = "Could not find the selected game in local storage.";
            return;
        }

        LoadImportedGame(game);
        SelectedAnalysisSide = example.Side;
        await AnalyzeImportedGameAsync();

        AnalysisMistakeItemViewModel? matchingMistake = AnalysisMistakes.FirstOrDefault(item =>
            item.Mistake.Moves.Any(move => move.Replay.Ply == example.Ply));

        if (matchingMistake is not null)
        {
            SelectedAnalysisMistake = matchingMistake;
            ShowSelectedMistakeOnBoard();
            return;
        }

        if (!importedGameReplay.TryMoveToPositionBeforePly(example.Ply, example.FenBefore, chessGame, out string? error))
        {
            StatusMessage = error ?? "Could not open the selected example position.";
            return;
        }

        OnImportedReplayStateChanged();
        ClearSelection();
        RefreshBoard();
        RefreshEngineSummary();
        RefreshImportedSummary();
        StatusMessage = $"Opened profile example for move {example.MoveNumber}{(example.Side == PlayerSide.White ? "." : "...")} {example.PlayedSan}.";
    }

    public async Task NavigateToOpeningExampleAsync(OpeningExampleGame example)
    {
        if (!await TryLoadStoredGameForNavigationAsync(example.GameFingerprint, example.Side))
        {
            return;
        }

        if (TryFocusAnalyzedMistake(example.FirstMistakePly))
        {
            StatusMessage = example.FirstMistakePly is int ply
                ? $"Opened {example.OpeningDisplayName} example at ply {ply}."
                : $"Opened {example.OpeningDisplayName} example.";
            return;
        }

        if (TryShowPositionBeforePly(
            example.FirstMistakePly,
            null,
            $"Opened {example.OpeningDisplayName} example game against {example.OpponentName}."))
        {
            return;
        }

        StatusMessage = "Could not open the selected opening example.";
    }

    public async Task NavigateToOpeningPositionAsync(OpeningMoveRecommendation recommendation)
    {
        if (!await TryLoadStoredGameForNavigationAsync(recommendation.GameFingerprint, recommendation.Side))
        {
            return;
        }

        if (TryShowPositionBeforePly(
            recommendation.Ply,
            recommendation.FenBefore,
            $"Opened {OpeningCatalog.Describe(recommendation.Eco)} position before {recommendation.MoveNumber}{(recommendation.Side == PlayerSide.White ? "." : "...")} {recommendation.PlayedSan}."))
        {
            return;
        }

        StatusMessage = "Could not open the selected opening position.";
    }

    public AnalysisWindowRequest? CreateAnalysisWindowRequest()
    {
        ImportedGame? currentGame = importedGameReplay.Game;
        if (currentGame is null)
        {
            StatusMessage = "Import or load a game before opening analysis.";
            return null;
        }

        LoadCachedAnalysisResultsForCurrentGame(new EngineAnalysisOptions());

        if (!engineSession.IsAvailable && cachedAnalysisResultsBySide.Count == 0)
        {
            StatusMessage = "The analysis engine is unavailable, and no saved analysis was found for the imported game.";
            return null;
        }

        if (IsBusy)
        {
            return null;
        }

        return new AnalysisWindowRequest(
            currentGame,
            engineSession.Analyzer,
            NavigateToAnalysisMistakeAsync,
            ShowAnalysisProgressOnBoard,
            SelectedAnalysisSide,
            cachedAnalysisResultsBySide);
    }

    public bool HasAnalysisEngine()
    {
        return engineSession.IsAvailable;
    }

    private void UndoLastMove()
    {
        if (undoFenStack.Count == 0 || IsBusy)
        {
            return;
        }

        string previousFen = undoFenStack.Pop();
        chessGame.TryLoadFen(previousFen, out _);
        ClearSelection();
        RefreshBoard();
        RefreshEngineSummary();
        StatusMessage = "Last move has been undone.";
        RaiseCommandStates();
    }

    private void ToggleBoardRotation()
    {
        RotateBoard = !RotateBoard;
        RefreshBoard();
    }

    private void ApplyNextImportedMove()
    {
        if (IsBusy)
        {
            return;
        }

        ApplyImportedMove(importedGameReplay.TryApplyNextMove(chessGame, out string? error), error);
    }

    private void ApplySelectedImportedMove()
    {
        if (SelectedImportedMove is null || IsBusy)
        {
            return;
        }

        ApplyImportedMove(importedGameReplay.TryApplySelectedMove(chessGame, out string? error), error);
    }

    private void ApplyImportedMove(bool applied, string? error)
    {
        if (!applied)
        {
            if (!string.IsNullOrWhiteSpace(error))
            {
                StatusMessage = error;
            }

            return;
        }

        undoFenStack.Clear();
        OnImportedReplayStateChanged();
        ClearSelection();
        RefreshBoard();
        RefreshEngineSummary();
        RefreshImportedSummary();
        if (SelectedImportedMove is not null)
        {
            StatusMessage = $"Moved board to {SelectedImportedMove.DisplayText}.";
        }

        RaiseCommandStates();
    }

    private void LoadImportedGameCore(ImportedGame game)
    {
        importedGameReplay.Load(game);
        AfterImportedGameLoaded();
    }

    private void AfterImportedGameLoaded()
    {
        cachedAnalysisResultsBySide.Clear();
        cachedAnalysisResult = null;
        undoFenStack.Clear();
        chessGame.Reset();
        OnImportedReplayStateChanged();
        ClearSelection();
        RefreshBoard();
        RefreshEngineSummary();
        RefreshImportedSummary();
        AnalysisMistakes.Clear();
        AnalysisDetails = "Imported game loaded. Choose a side and run analysis.";
        OnPropertyChanged(nameof(CanOpenImportedAnalysis));
        RaiseCommandStates();
    }

    private void StopImport()
    {
        if (importCancellationTokenSource is null || importCancellationTokenSource.IsCancellationRequested)
        {
            return;
        }

        importCancellationTokenSource.Cancel();
        StatusMessage = "Stopping PGN import analysis after the current engine step...";
        OnPropertyChanged(nameof(IsImportCancellationAvailable));
        StopImportCommand.RaiseCanExecuteChanged();
    }

    private async Task AnalyzeImportedGameAsync()
    {
        ImportedGame? currentGame = importedGameReplay.Game;
        if (currentGame is null || engineSession.Analyzer is not { } engineAnalyzer || IsBusy)
        {
            return;
        }

        try
        {
            IsBusy = true;
            StatusMessage = $"Analyzing imported game for {SelectedAnalysisSide}...";
            AnalysisDetails = "The analysis engine is reviewing the imported game. This may take a moment.";
            AnalysisMistakes.Clear();
            SelectedAnalysisMistake = null;
            cachedAnalysisResultsBySide.Remove(SelectedAnalysisSide);
            ApplyAnalysisToImportedMoves();
            IProgress<GameAnalysisProgress> progress = new Progress<GameAnalysisProgress>(ShowAnalysisProgressOnBoard);

            EngineAnalysisOptions options = analysisWorkflow.CreateDefaultAnalysisOptions();
            cachedAnalysisResult = await analysisWorkflow.AnalyzeGameAsync(
                engineAnalyzer,
                currentGame,
                SelectedAnalysisSide,
                options,
                progress);
            analysisDataService.StoreAnalysisResult(currentGame, SelectedAnalysisSide, options, cachedAnalysisResult);
            cachedAnalysisResultsBySide[SelectedAnalysisSide] = cachedAnalysisResult;
            ApplyAnalysisToImportedMoves();
            ApplyAnalysisFilter();
            StatusMessage = $"Analysis finished for {SelectedAnalysisSide}.";
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            cachedAnalysisResult = null;
            cachedAnalysisResultsBySide.Remove(SelectedAnalysisSide);
            AnalysisMistakes.Clear();
            AnalysisDetails = "Analysis failed.";
            ApplyAnalysisToImportedMoves();
            StatusMessage = $"Analysis failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
            RefreshEngineSummary();
            RefreshImportedSummary();
        }
    }

    private void ShowAnalysisProgressOnBoard(GameAnalysisProgress progress)
        => ShowAnalysisProgressOnBoard(progress, bulkStatusPrefix: null);

    private void ShowAnalysisProgressOnBoard(GameAnalysisProgress progress, string? bulkStatusPrefix)
    {
        if (!importedGameReplay.TryShowAnalysisProgress(progress, chessGame))
        {
            return;
        }

        OnImportedReplayStateChanged();
        BestMoveArrows = [new BoardArrowViewModel(progress.Replay.FromSquare, progress.Replay.ToSquare, Color.Parse("#D33838"))];
        ClearSelection();
        RefreshImportedSummary();

        string positionText = progress.Stage == GameAnalysisProgressStage.BeforeMove
            ? "before"
            : "after";
        string moveStatus = $"Analyzing {progress.CurrentAnalyzedMove}/{progress.TotalAnalyzedMoves}: {positionText} {progress.Replay.MoveNumber}{(progress.Replay.Side == PlayerSide.White ? "." : "...")} {progress.Replay.San}.";
        StatusMessage = string.IsNullOrWhiteSpace(bulkStatusPrefix)
            ? moveStatus
            : $"{bulkStatusPrefix} {moveStatus}";
    }

    private void ApplyAnalysisFilter()
    {
        AnalysisMistakes.Clear();
        SelectedAnalysisMistake = null;

        if (cachedAnalysisResult is null)
        {
            AnalysisDetails = importedGameReplay.Game is null
                ? Localizer.Text(LocalizedStrings.MainAnalysisPlaceholder)
                : Localizer.Text(LocalizedStrings.MainRunAnalysisPlaceholder);
            return;
        }

        IEnumerable<SelectedMistake> source = cachedAnalysisResult.HighlightedMistakes;
        source = SelectedAnalysisFilter switch
        {
            "Blunder" => source.Where(item => item.Quality == MoveQualityBucket.Blunder),
            "Mistake" => source.Where(item => item.Quality == MoveQualityBucket.Mistake),
            "Inaccuracy" => source.Where(item => item.Quality == MoveQualityBucket.Inaccuracy),
            _ => source
        };

        foreach (SelectedMistake mistake in source)
        {
            AnalysisMistakes.Add(new AnalysisMistakeItemViewModel(mistake));
        }

        if (AnalysisMistakes.Count == 0)
        {
            AnalysisDetails = Localizer.Text(LocalizedStrings.MainNoMistakesMatchSelectedFilter);
            return;
        }

        SelectedAnalysisMistake = AnalysisMistakes[0];
    }

    private void ApplyAnalysisToImportedMoves()
    {
        importedGameReplay.ApplyAnalysisLabels(cachedAnalysisResultsBySide.Values);
    }

    private void LoadCachedAnalysisResultsForCurrentGame(EngineAnalysisOptions options)
    {
        ImportedGame? currentGame = importedGameReplay.Game;
        if (currentGame is null)
        {
            return;
        }

        foreach (PlayerSide side in AnalysisSides)
        {
            if (cachedAnalysisResultsBySide.ContainsKey(side))
            {
                continue;
            }

            if (analysisDataService.TryGetCachedAnalysis(currentGame, side, options, out GameAnalysisResult? cachedResult) && cachedResult is not null)
            {
                cachedAnalysisResultsBySide[side] = cachedResult;
            }
        }
    }

    private bool IsCurrentImportedGame(ImportedGame game)
    {
        return importedGameReplay.IsCurrentGame(game);
    }

    private void ShowSelectedMistakeOnBoard()
    {
        if (SelectedAnalysisMistake is null)
        {
            return;
        }

        if (SelectedAnalysisMistake.LeadMove is null)
        {
            StatusMessage = "Selected mistake has no associated moves and cannot be shown on the board.";
            return;
        }

        ReplayPly replayPly = SelectedAnalysisMistake.LeadMove.Replay;
        if (!importedGameReplay.TryMoveToReplayPly(replayPly, chessGame, out string? error))
        {
            StatusMessage = error ?? "Could not navigate to the selected mistake.";
            return;
        }

        OnImportedReplayStateChanged();
        ClearSelection();
        RefreshBoard();
        RefreshEngineSummary();
        RefreshImportedSummary();
        StatusMessage = $"Jumped to the board position after {replayPly.MoveNumber}{(replayPly.Side == PlayerSide.White ? "." : "...")} {replayPly.San}.";
    }

    private async Task<bool> TryLoadStoredGameForNavigationAsync(string gameFingerprint, PlayerSide side)
    {
        if (IsBusy)
        {
            return false;
        }

        if (!analysisDataService.TryLoadImportedGame(gameFingerprint, out ImportedGame? game) || game is null)
        {
            StatusMessage = "Could not find the selected game in local storage.";
            return false;
        }

        LoadImportedGame(game);
        SelectedAnalysisSide = side;
        await AnalyzeImportedGameAsync();
        return true;
    }

    private bool TryFocusAnalyzedMistake(int? ply)
    {
        if (!ply.HasValue)
        {
            return false;
        }

        AnalysisMistakeItemViewModel? matchingMistake = AnalysisMistakes.FirstOrDefault(item =>
            item.Mistake.Moves.Any(move => move.Replay.Ply == ply.Value));

        if (matchingMistake is null)
        {
            return false;
        }

        SelectedAnalysisMistake = matchingMistake;
        ShowSelectedMistakeOnBoard();
        return true;
    }

    private bool TryShowPositionBeforePly(int? ply, string? fenBefore, string successMessage)
    {
        if (!importedGameReplay.TryMoveToPositionBeforePly(ply, fenBefore, chessGame, out string? error))
        {
            StatusMessage = error ?? "Could not open the selected position.";
            return false;
        }

        OnImportedReplayStateChanged();
        ClearSelection();
        RefreshBoard();
        RefreshEngineSummary();
        RefreshImportedSummary();
        StatusMessage = successMessage;
        return true;
    }

    public Task NavigateToAnalysisMistakeAsync(MoveAnalysisResult move)
    {
        if (!importedGameReplay.TryMoveToReplayPly(move.Replay, chessGame, out string? error))
        {
            StatusMessage = error ?? "Could not navigate to the selected mistake.";
            return Task.CompletedTask;
        }

        OnImportedReplayStateChanged();
        ClearSelection();
        RefreshBoard();
        RefreshEngineSummary();
        RefreshImportedSummary();
        StatusMessage = $"Jumped to the board position after {move.Replay.MoveNumber}{(move.Replay.Side == PlayerSide.White ? "." : "...")} {move.Replay.San}.";
        return Task.CompletedTask;
    }

    private void TryInitializeEngine()
    {
        StatusMessage = engineSession.Reload();
    }

    private void RefreshEngineSummary()
    {
        if (!engineSession.IsAvailable)
        {
            SuggestionText = Localizer.Text(LocalizedStrings.MainEngineSuggestionsUnavailable);
            EvaluationText = Localizer.Text(LocalizedStrings.MainEvaluationUnavailable);
            EvaluationBarValue = 50;
            BestMoveArrows = [];
            OnPropertyChanged(nameof(BestMoveArrows));
            AnalyzeImportedGameCommand.RaiseCanExecuteChanged();
            OnPrimaryNextStepChanged();
            return;
        }

        try
        {
            string currentFen = chessGame.GetFen();
            MainWindowEngineSummary summary = engineSession.RefreshSummary(currentFen);
            string[] moves = summary.TopMoves.ToArray();
            SuggestionText = moves.Length == 0
                ? Localizer.Text(LocalizedStrings.MainTopMovesNone)
                : Localizer.Format(LocalizedStrings.MainTopMoves, string.Join(", ", moves));
            BestMoveArrows = moves
                .Where(move => move.Length >= 4)
                .Select((move, index) => new BoardArrowViewModel(
                    move[..2],
                    move.Substring(2, 2),
                    index switch
                    {
                        0 => Color.Parse("#2146FF"),
                        1 => Color.Parse("#169C16"),
                        _ => Color.Parse("#F39C12")
                    }))
                .ToList();
            OnPropertyChanged(nameof(BestMoveArrows));

            if (summary.Evaluation is null)
            {
                EvaluationText = Localizer.Text(LocalizedStrings.MainEvaluationUnavailable);
                EvaluationBarValue = 50;
            }
            else if (summary.Evaluation.MateIn is int mateIn)
            {
                int signedMate = chessGame.WhiteToMove ? mateIn : -mateIn;
                bool whiteWinning = signedMate > 0;
                EvaluationText = whiteWinning
                    ? Localizer.Format(LocalizedStrings.MainEvaluationWhiteMates, Math.Abs(signedMate))
                    : Localizer.Format(LocalizedStrings.MainEvaluationBlackMates, Math.Abs(signedMate));
                EvaluationBarValue = whiteWinning ? 100 : 0;
            }
            else
            {
                int cp = summary.Evaluation.Centipawns ?? 0;
                int whitePerspectiveCp = chessGame.WhiteToMove ? cp : -cp;
                double pawns = whitePerspectiveCp / 100.0;
                double normalized = Math.Clamp((pawns + 5.0) / 10.0, 0.0, 1.0);
                EvaluationBarValue = (int)Math.Round(normalized * 100);
                EvaluationText = Math.Abs(pawns) < 0.15
                    ? Localizer.Text(LocalizedStrings.MainEvaluationEven)
                    : pawns > 0
                        ? Localizer.Format(LocalizedStrings.MainEvaluationWhite, pawns.ToString("+0.0;-0.0;0.0", Localizer.CurrentCulture))
                        : Localizer.Format(LocalizedStrings.MainEvaluationBlack, Math.Abs(pawns).ToString("0.0", Localizer.CurrentCulture));
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            SuggestionText = Localizer.Text(LocalizedStrings.MainEngineSuggestionsUnavailable);
            EvaluationText = Localizer.Text(LocalizedStrings.MainEvaluationUnavailable);
            EvaluationBarValue = 50;
            BestMoveArrows = [];
            OnPropertyChanged(nameof(BestMoveArrows));
            StatusMessage = $"Engine refresh failed: {ex.Message}";
        }
        finally
        {
            AnalyzeImportedGameCommand.RaiseCanExecuteChanged();
            OnPrimaryNextStepChanged();
        }
    }

    private void OnPrimaryNextStepChanged()
    {
        OnPropertyChanged(nameof(IsEngineAvailable));
        OnPropertyChanged(nameof(IsEngineUnavailable));
        OnPropertyChanged(nameof(HasImportedGame));
        OnPropertyChanged(nameof(CanOpenImportedAnalysis));
        OnPropertyChanged(nameof(PrimaryNextStepTitle));
        OnPropertyChanged(nameof(PrimaryNextStepDescription));
    }

    private void OnImportedReplayStateChanged()
    {
        OnPropertyChanged(nameof(SelectedImportedMove));
        OnPrimaryNextStepChanged();
        ApplyNextImportedMoveCommand.RaiseCanExecuteChanged();
        ApplySelectedImportedMoveCommand.RaiseCanExecuteChanged();
    }

    private void RefreshImportedSummary()
    {
        ImportedGame? currentGame = importedGameReplay.Game;
        if (currentGame is null || importedGameReplay.ReplayCount == 0)
        {
            ImportedGameSummary = Localizer.Text(LocalizedStrings.MainImportedMovesNone);
            return;
        }

        string whitePlayer = string.IsNullOrWhiteSpace(currentGame.WhitePlayer)
            ? Localizer.Text(LocalizedStrings.CommonWhite)
            : currentGame.WhitePlayer;
        string blackPlayer = string.IsNullOrWhiteSpace(currentGame.BlackPlayer)
            ? Localizer.Text(LocalizedStrings.CommonBlack)
            : currentGame.BlackPlayer;
        string players = Localizer.Format(LocalizedStrings.CommonPlayersVersus, whitePlayer, blackPlayer);
        string result = string.IsNullOrWhiteSpace(currentGame.Result)
            ? Localizer.Text(LocalizedStrings.MainResultUnknown)
            : Localizer.Format(LocalizedStrings.SavedAnalysesResult, currentGame.Result);
        string eco = string.IsNullOrWhiteSpace(currentGame.Eco) ? string.Empty : $" | {OpeningCatalog.Describe(currentGame.Eco)}";
        string date = string.IsNullOrWhiteSpace(currentGame.DateText) ? string.Empty : $" | {currentGame.DateText}";
        ImportedGameSummary = Localizer.Format(
            LocalizedStrings.MainImportedMovesSummary,
            importedGameReplay.Cursor,
            importedGameReplay.ReplayCount,
            players,
            result,
            date,
            eco);
    }

    private void RefreshBoard()
    {
        OnPropertyChanged(nameof(BoardFen));
        OnPropertyChanged(nameof(SelectedSquareName));
        OnPropertyChanged(nameof(AvailableMoveSquares));
        OnPropertyChanged(nameof(BestMoveArrows));
        OnPropertyChanged(nameof(RotateBoard));
        RaiseCommandStates();
    }

    private string? GetPieceAt(int x, int y)
    {
        if (!FenPosition.TryParse(chessGame.GetFen(), out FenPosition? position, out _)
            || position is null)
        {
            return null;
        }

        return position.Board[x, y];
    }

    private void ClearSelection()
    {
        selectedSquare = null;
        availableTargets.Clear();
        PreviewTargetSquare = null;
        SelectedPieceMoveOption = null;
        ClearPieceMoveOptions();
        RefreshBoard();
    }

    private void UpdatePieceMoveOptions(string fromSquare, string pieceName, List<LegalMoveInfo> movesForPiece)
    {
        PieceMoveOptions.Clear();
        PieceMoveOptionsHeader = Localizer.Format(LocalizedStrings.MainSelectedPieceHeader, pieceName, fromSquare, movesForPiece.Count);
        PreviewTargetSquare = null;
        SelectedPieceMoveOption = null;

        if (movesForPiece.Count == 0)
        {
            PieceMoveOptions.Add(new PieceMoveOptionViewModel("-", string.Empty, Localizer.Text(LocalizedStrings.MainNoLegalMovesForPiece), string.Empty, false));
            return;
        }

        string currentFen = chessGame.GetFen();
        string perspectiveSide = chessGame.WhiteToMove ? "White" : "Black";
        EngineAnalysis? baselineAnalysis = null;
        string? bestMove = null;

        if (engineSession.IsAvailable)
        {
            try
            {
                baselineAnalysis = engineSession.AnalyzePosition(currentFen, new EngineAnalysisOptions(Depth: 10, MultiPv: 1, MoveTimeMs: 90));
                bestMove = baselineAnalysis.BestMoveUci;
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                baselineAnalysis = null;
                bestMove = null;
            }
        }

        foreach (LegalMoveInfo move in movesForPiece.OrderBy(m => m.San, StringComparer.Ordinal))
        {
            PieceMovePresentation presentation = BuildPieceMovePresentation(move, currentFen, perspectiveSide, baselineAnalysis, bestMove);
            PieceMoveOptions.Add(new PieceMoveOptionViewModel(
                move.San,
                move.Uci,
                presentation.Label,
                move.ToSquare,
                string.Equals(move.Uci, bestMove, StringComparison.OrdinalIgnoreCase),
                presentation.EvalText,
                presentation.EvalBrush));
        }
    }

    private void ClearPieceMoveOptions()
    {
        PieceMoveOptions.Clear();
        PieceMoveOptionsHeader = Localizer.Text(LocalizedStrings.MainSelectedPieceNone);
        PieceMoveOptions.Add(new PieceMoveOptionViewModel("-", string.Empty, Localizer.Text(LocalizedStrings.MainSelectPieceInspectMoves), string.Empty, false));
    }

    private PieceMovePresentation BuildPieceMovePresentation(LegalMoveInfo move, string currentFen, string perspectiveSide, EngineAnalysis? baselineAnalysis, string? bestMoveUci)
    {
        string bestMarker = string.Equals(move.Uci, bestMoveUci, StringComparison.OrdinalIgnoreCase) ? "* " : "  ";
        string moveText = $"{bestMarker}{FormatSanAndUci(move.San, move.Uci)}";
        PlayerSide movingSide = perspectiveSide == "White" ? PlayerSide.White : PlayerSide.Black;

        if (TryFindCachedMoveAnalysis(currentFen, move.Uci, movingSide, out MoveAnalysisResult? cachedMoveAnalysis)
            && cachedMoveAnalysis is not null)
        {
            return new PieceMovePresentation(
                moveText,
                FormatEvalScore(cachedMoveAnalysis.EvalAfterCp, cachedMoveAnalysis.PlayedMateIn),
                GetEvalBrush(cachedMoveAnalysis.EvalAfterCp, cachedMoveAnalysis.PlayedMateIn));
        }

        if (!engineSession.IsAvailable || baselineAnalysis is null)
        {
            return new PieceMovePresentation(moveText, string.Empty, "#657386");
        }

        try
        {
            ChessGame tempGame = new();
            if (!tempGame.TryLoadFen(currentFen, out _)
                || !tempGame.TryApplyUci(move.Uci, out AppliedMoveInfo? appliedMove, out _)
                || appliedMove is null)
            {
                return new PieceMovePresentation(moveText, string.Empty, "#657386");
            }

            EngineAnalysis moveAnalysis = engineSession.AnalyzePosition(appliedMove.FenAfter, new EngineAnalysisOptions(Depth: 10, MultiPv: 1, MoveTimeMs: 90));
            EngineLine? moveLine = moveAnalysis.Lines.Count > 0 ? moveAnalysis.Lines[0] : null;
            int moveCp = NormalizePerspectiveScore(moveLine?.Centipawns, perspectiveSide, perspectiveSide == "White" ? "Black" : "White");
            int? moveMate = moveLine?.MateIn is int mate ? -mate : null;
            string scoreText = FormatEvalScore(moveLine?.Centipawns is null ? null : moveCp, moveMate);
            string evalBrush = GetEvalBrush(moveLine?.Centipawns is null ? null : moveCp, moveMate);
            return new PieceMovePresentation(moveText, scoreText, evalBrush);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return new PieceMovePresentation(moveText, string.Empty, "#657386");
        }
    }

    private bool TryFindCachedMoveAnalysis(string fenBefore, string moveUci, PlayerSide movingSide, out MoveAnalysisResult? analysis)
    {
        string normalizedFen = NormalizeFenForAnalysisMatch(fenBefore);
        analysis = cachedAnalysisResultsBySide.Values
            .Where(result => result.AnalyzedSide == movingSide)
            .SelectMany(result => result.MoveAnalyses)
            .FirstOrDefault(move =>
                move.Replay.Side == movingSide
                && string.Equals(move.Replay.Uci, moveUci, StringComparison.Ordinal)
                && string.Equals(NormalizeFenForAnalysisMatch(move.Replay.FenBefore), normalizedFen, StringComparison.Ordinal));
        return analysis is not null;
    }

    private static string FormatEvalScore(int? centipawns, int? mateIn)
    {
        if (mateIn is int mate)
        {
            return $"mate {mate}";
        }

        return centipawns is int cp
            ? $"{cp / 100.0:+0.00;-0.00;+0.00}"
            : string.Empty;
    }

    private static string GetEvalBrush(int? perspectiveCp, int? mateIn = null)
    {
        if (mateIn is int mate)
        {
            return mate > 0 ? "#1F7A55" : "#B93838";
        }

        if (perspectiveCp is not int cp)
        {
            return "#657386";
        }

        if (cp > 0)
        {
            return "#1F7A55";
        }

        return cp < 0 ? "#B93838" : "#657386";
    }

    private static string NormalizeFenForAnalysisMatch(string fen)
    {
        string[] parts = fen.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 4
            ? string.Join(' ', parts.Take(4))
            : fen;
    }

    private async Task<string?> SelectMoveToApplyAsync(List<LegalMoveInfo> matchingMoves)
    {
        if (matchingMoves.Count == 0)
        {
            return null;
        }

        if (matchingMoves.Count == 1)
        {
            return matchingMoves[0].Uci;
        }

        if (promotionMoveSelector is not null && matchingMoves.All(move => !string.IsNullOrWhiteSpace(move.PromotionPiece)))
        {
            LegalMoveInfo? selectedMove = await promotionMoveSelector(matchingMoves);
            return selectedMove?.Uci;
        }

        string queenPiece = chessGame.WhiteToMove ? "Q" : "q";
        LegalMoveInfo? queenPromotion = matchingMoves.FirstOrDefault(move => string.Equals(move.PromotionPiece, queenPiece, StringComparison.Ordinal));
        return queenPromotion?.Uci ?? matchingMoves[0].Uci;
    }

    private void RaiseCommandStates()
    {
        UndoCommand.RaiseCanExecuteChanged();
        RotateBoardCommand.RaiseCanExecuteChanged();
        ApplyNextImportedMoveCommand.RaiseCanExecuteChanged();
        ApplySelectedImportedMoveCommand.RaiseCanExecuteChanged();
        AnalyzeImportedGameCommand.RaiseCanExecuteChanged();
        StopImportCommand.RaiseCanExecuteChanged();
        ShowSelectedMistakeOnBoardCommand.RaiseCanExecuteChanged();
    }

    private void SaveImportedGame(ImportedGame parsedGame)
    {
        analysisDataService.SaveImportedGame(parsedGame);
    }

    private void SaveImportedGames(IReadOnlyList<ImportedGame> games)
    {
        analysisDataService.SaveImportedGames(games);
    }

    public static string? DetectPrimaryPlayer(IReadOnlyList<ImportedGame> games)
    {
        return games
            .SelectMany(game => new[] { game.WhitePlayer, game.BlackPlayer })
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .GroupBy(name => name!, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault()
            ?.Key;
    }

    private static PlayerSide ResolveAnalysisSide(ImportedGame game, string? primaryPlayer, PlayerSide fallbackSide)
    {
        if (!string.IsNullOrWhiteSpace(primaryPlayer))
        {
            if (string.Equals(game.WhitePlayer, primaryPlayer, StringComparison.OrdinalIgnoreCase))
            {
                return PlayerSide.White;
            }

            if (string.Equals(game.BlackPlayer, primaryPlayer, StringComparison.OrdinalIgnoreCase))
            {
                return PlayerSide.Black;
            }
        }

        return fallbackSide;
    }

    private static bool PlayerMatchesSide(ImportedGame game, string? primaryPlayer, PlayerSide side)
    {
        if (string.IsNullOrWhiteSpace(primaryPlayer))
        {
            return true;
        }

        string? playerName = side == PlayerSide.White ? game.WhitePlayer : game.BlackPlayer;
        return string.Equals(playerName, primaryPlayer, StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildBulkAnalysisStatus(
        ImportedGame game,
        PlayerSide side,
        int current,
        int total,
        string? primaryPlayer)
    {
        string playerText = string.IsNullOrWhiteSpace(primaryPlayer)
            ? FormatPlayerSide(side)
            : Localizer.Format(LocalizedStrings.MainPrimaryPlayerAsSide, primaryPlayer, FormatPlayerSide(side));
        string players = FormatImportedPlayers(game);
        return Localizer.Format(LocalizedStrings.MainAnalyzingPgnFileStatus, current, total, players, playerText);
    }

    private static string BuildAnalysisFailureMessage(ImportedGame game, PlayerSide side, Exception ex)
    {
        string players = FormatImportedPlayers(game);
        string date = string.IsNullOrWhiteSpace(game.DateText) ? string.Empty : $" {game.DateText}";
        return Localizer.Format(LocalizedStrings.MainAnalysisFailureSummary, players, date, FormatPlayerSide(side), ex.Message);
    }

    private static string FormatImportedPlayers(ImportedGame game)
    {
        string whitePlayer = string.IsNullOrWhiteSpace(game.WhitePlayer)
            ? Localizer.Text(LocalizedStrings.CommonWhite)
            : game.WhitePlayer;
        string blackPlayer = string.IsNullOrWhiteSpace(game.BlackPlayer)
            ? Localizer.Text(LocalizedStrings.CommonBlack)
            : game.BlackPlayer;
        return Localizer.Format(LocalizedStrings.CommonPlayersVersus, whitePlayer, blackPlayer);
    }

    private static string FormatPlayerSide(PlayerSide side)
        => side == PlayerSide.White
            ? Localizer.Text(LocalizedStrings.CommonWhite)
            : Localizer.Text(LocalizedStrings.CommonBlack);

    private static bool TryParseSquare(string square, out (int X, int Y) point)
    {
        point = default;
        if (string.IsNullOrWhiteSpace(square) || square.Length != 2)
        {
            return false;
        }

        char file = char.ToLowerInvariant(square[0]);
        char rank = square[1];
        if (file < 'a' || file > 'h' || rank < '1' || rank > '8')
        {
            return false;
        }

        point = (file - 'a', 8 - (rank - '0'));
        return true;
    }

    private static string FormatSanAndUci(string san, string uci)
        => string.Equals(san, uci, StringComparison.OrdinalIgnoreCase) ? san : $"{san} ({uci})";

    private static int NormalizePerspectiveScore(int? cp, string perspectiveSide, string sideToMove)
    {
        int sign = string.Equals(perspectiveSide, sideToMove, StringComparison.Ordinal) ? 1 : -1;
        return (cp ?? 0) * sign;
    }

    private sealed record PieceMovePresentation(string Label, string EvalText, string EvalBrush);
}
