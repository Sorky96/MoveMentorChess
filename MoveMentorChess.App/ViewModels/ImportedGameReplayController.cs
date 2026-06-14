using System.Collections.ObjectModel;
using System.Diagnostics;
using MoveMentorChess.Analysis;
using MoveMentorChess.Opening;

namespace MoveMentorChess.App.ViewModels;

internal sealed class ImportedGameReplayController : ViewModelBase
{
    private readonly GameReplayService replayService;
    private ImportedMoveItemViewModel? selectedMove;

    public ImportedGameReplayController()
        : this(new GameReplayService())
    {
    }

    internal ImportedGameReplayController(GameReplayService replayService)
    {
        this.replayService = replayService ?? throw new ArgumentNullException(nameof(replayService));
    }

    public ObservableCollection<ImportedMoveItemViewModel> ImportedMoves { get; } = [];

    public ImportedGame? Game { get; private set; }

    public IReadOnlyList<ReplayPly> Replay { get; private set; } = Array.Empty<ReplayPly>();

    public int Cursor { get; private set; }

    public int ReplayCount => Replay.Count;

    public bool HasReplayableGame => Game is not null && Replay.Count > 0;

    public bool CanApplyNextMove => Cursor < Replay.Count;

    public ImportedMoveItemViewModel? SelectedMove
    {
        get => selectedMove;
        private set => SetProperty(ref selectedMove, value);
    }

    public bool SetSelectedMove(ImportedMoveItemViewModel? value)
    {
        if (EqualityComparer<ImportedMoveItemViewModel?>.Default.Equals(selectedMove, value))
        {
            return false;
        }

        SelectedMove = value;
        return true;
    }

    public void Load(ImportedGame game)
    {
        ArgumentNullException.ThrowIfNull(game);

        IReadOnlyList<ReplayPly> replay = replayService.Replay(game);
        Game = game;
        Replay = replay;
        Cursor = 0;
        ImportedMoves.Clear();
        for (int i = 0; i < Replay.Count; i++)
        {
            ImportedMoves.Add(new ImportedMoveItemViewModel(i, Replay[i]));
        }

        SelectedMove = null;
        OnReplayStateChanged();
    }

    public bool TryLoadFirstReplayableGame(IReadOnlyList<ImportedGame> games, out int skippedGames)
    {
        ArgumentNullException.ThrowIfNull(games);

        skippedGames = 0;
        foreach (ImportedGame game in games)
        {
            try
            {
                Load(game);
                return true;
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                Trace.TraceWarning(
                    "ImportedGameReplayController: skipped imported game '{0}' because replay loading failed ({1}: {2})",
                    GameFingerprint.Compute(game.PgnText),
                    ex.GetType().Name,
                    ex.Message);
                skippedGames++;
            }
        }

        return false;
    }

    public void ClampCursorToReplayEnd()
    {
        Cursor = Replay.Count > 0 ? Math.Min(Cursor, Replay.Count) : 0;
        OnPropertyChanged(nameof(Cursor));
        OnPropertyChanged(nameof(CanApplyNextMove));
    }

    public bool TryApplyNextMove(ChessGame board, out string? error)
    {
        return TryMoveToIndex(Cursor, board, out error);
    }

    public bool TryApplySelectedMove(ChessGame board, out string? error)
    {
        error = null;
        return SelectedMove is not null && TryMoveToIndex(SelectedMove.Index, board, out error);
    }

    public bool TryMoveToIndex(int index, ChessGame board, out string? error)
    {
        ArgumentNullException.ThrowIfNull(board);
        error = null;

        if (index < 0 || index >= Replay.Count)
        {
            return false;
        }

        ReplayPly replayPly = Replay[index];
        if (!board.TryLoadFen(replayPly.FenAfter, out error))
        {
            error ??= "Could not load the imported position.";
            return false;
        }

        Cursor = index + 1;
        SelectedMove = ImportedMoves[index];
        OnReplayStateChanged();
        return true;
    }

    public bool TryMoveToReplayPly(ReplayPly replayPly, ChessGame board, out string? error)
    {
        ArgumentNullException.ThrowIfNull(replayPly);
        ArgumentNullException.ThrowIfNull(board);

        if (!board.TryLoadFen(replayPly.FenAfter, out error))
        {
            error ??= "Could not load the imported position.";
            return false;
        }

        Cursor = replayPly.Ply;
        SelectMoveByPly(replayPly.Ply);
        OnReplayStateChanged();
        return true;
    }

    public bool TryMoveToPositionBeforePly(int? ply, string? fenBefore, ChessGame board, out string? error)
    {
        ArgumentNullException.ThrowIfNull(board);
        error = null;

        string? targetFen = !string.IsNullOrWhiteSpace(fenBefore)
            ? fenBefore
            : ResolveFenBeforePly(ply);
        if (string.IsNullOrWhiteSpace(targetFen))
        {
            return false;
        }

        if (!board.TryLoadFen(targetFen, out error))
        {
            error ??= "Could not load the imported position.";
            return false;
        }

        Cursor = Math.Max(0, (ply ?? 1) - 1);
        SelectPreviousMoveForCursor();
        OnReplayStateChanged();
        return true;
    }

    public bool TryShowAnalysisProgress(GameAnalysisProgress progress, ChessGame board)
    {
        ArgumentNullException.ThrowIfNull(progress);
        ArgumentNullException.ThrowIfNull(board);

        if (!board.TryLoadFen(progress.Fen, out _))
        {
            return false;
        }

        Cursor = progress.Stage == GameAnalysisProgressStage.AfterMove
            ? progress.Replay.Ply
            : Math.Max(0, progress.Replay.Ply - 1);
        SelectMoveByPly(progress.Replay.Ply);
        OnReplayStateChanged();
        return true;
    }

    public void ApplyAnalysisLabels(IEnumerable<GameAnalysisResult> analysisResults)
    {
        ArgumentNullException.ThrowIfNull(analysisResults);
        ClearAnalysisLabels();

        Dictionary<int, MoveAnalysisResult> analysesByPly = analysisResults
            .SelectMany(result => result.MoveAnalyses)
            .GroupBy(move => move.Replay.Ply)
            .ToDictionary(group => group.Key, group => group.First());

        foreach (ImportedMoveItemViewModel item in ImportedMoves)
        {
            if (analysesByPly.TryGetValue(item.ReplayPly.Ply, out MoveAnalysisResult? analysis))
            {
                item.ApplyAnalysis(analysis);
            }
        }
    }

    public void ClearAnalysisLabels()
    {
        foreach (ImportedMoveItemViewModel item in ImportedMoves)
        {
            item.ClearAnalysis();
        }
    }

    public bool IsCurrentGame(ImportedGame game)
    {
        ArgumentNullException.ThrowIfNull(game);

        return Game is not null
            && string.Equals(
                GameFingerprint.Compute(Game.PgnText),
                GameFingerprint.Compute(game.PgnText),
                StringComparison.Ordinal);
    }

    public string? ResolveFenBeforePly(int? ply)
    {
        if (!ply.HasValue)
        {
            return null;
        }

        if (ply.Value <= 1)
        {
            ChessGame initialGame = new();
            return initialGame.GetFen();
        }

        int previousIndex = ply.Value - 2;
        return previousIndex < 0 || previousIndex >= Replay.Count
            ? null
            : Replay[previousIndex].FenAfter;
    }

    private void SelectMoveByPly(int ply)
    {
        int index = ply - 1;
        SelectedMove = index >= 0 && index < ImportedMoves.Count
            ? ImportedMoves[index]
            : null;
    }

    private void SelectPreviousMoveForCursor()
    {
        int index = Cursor - 1;
        SelectedMove = index >= 0 && index < ImportedMoves.Count
            ? ImportedMoves[index]
            : null;
    }

    private void OnReplayStateChanged()
    {
        OnPropertyChanged(nameof(Game));
        OnPropertyChanged(nameof(Replay));
        OnPropertyChanged(nameof(Cursor));
        OnPropertyChanged(nameof(ReplayCount));
        OnPropertyChanged(nameof(HasReplayableGame));
        OnPropertyChanged(nameof(CanApplyNextMove));
    }
}
