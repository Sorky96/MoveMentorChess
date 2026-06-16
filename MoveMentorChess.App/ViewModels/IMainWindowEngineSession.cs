using MoveMentorChess.Engine;

namespace MoveMentorChess.App.ViewModels;

internal interface IMainWindowEngineSession : IDisposable
{
    bool IsAvailable { get; }

    IEngineAnalyzer? Analyzer { get; }

    string Reload();

    MainWindowEngineSummary RefreshSummary(string fen);

    EngineAnalysis AnalyzePosition(string fen, EngineAnalysisOptions options);
}

internal sealed record MainWindowEngineSummary(
    IReadOnlyList<string> TopMoves,
    EvaluationSummary? Evaluation);
