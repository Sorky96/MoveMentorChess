using MoveMentorChess.Engine;

namespace MoveMentorChess.App.ViewModels;

public sealed record AnalysisWindowRequest(
    ImportedGame ImportedGame,
    IEngineAnalyzer? EngineAnalyzer,
    Func<MoveAnalysisResult, Task> NavigateToMoveAsync,
    Action<GameAnalysisProgress>? AnalysisProgress,
    PlayerSide InitialSide,
    IReadOnlyDictionary<PlayerSide, GameAnalysisResult> InitialResultsBySide);
