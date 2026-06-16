using MoveMentorChess.Engine;

namespace MoveMentorChess.App.ViewModels;

internal interface IMainWindowAnalysisWorkflow
{
    EngineAnalysisOptions CreateDefaultAnalysisOptions();

    EngineAnalysisOptions CreateBulkAnalysisOptions();

    Task<GameAnalysisResult> AnalyzeGameAsync(
        IEngineAnalyzer engineAnalyzer,
        ImportedGame game,
        PlayerSide side,
        EngineAnalysisOptions options,
        IProgress<GameAnalysisProgress>? progress,
        CancellationToken cancellationToken = default);
}
