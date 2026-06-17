using MoveMentorChess.Engine;

namespace MoveMentorChess.App.ViewModels;

internal interface IMainWindowAnalysisWorkflow
{
    EngineAnalysisOptions CreateDefaultAnalysisOptions();

    EngineAnalysisOptions CreateBulkAnalysisOptions();

    IMainWindowAnalysisRun CreateAnalysisRun(IEngineAnalyzer engineAnalyzer);
}

internal interface IMainWindowAnalysisRun : IDisposable
{
    Task<GameAnalysisResult> AnalyzeGameAsync(
        ImportedGame game,
        PlayerSide side,
        EngineAnalysisOptions options,
        IProgress<GameAnalysisProgress>? progress,
        CancellationToken cancellationToken = default);
}
