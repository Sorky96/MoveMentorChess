using MoveMentorChess.Engine;

namespace MoveMentorChess.App.ViewModels;

internal sealed class MainWindowAnalysisRunLease(
    IMainWindowAnalysisWorkflow analysisWorkflow,
    IEngineAnalyzer engineAnalyzer) : IDisposable
{
    private readonly IMainWindowAnalysisWorkflow analysisWorkflow = analysisWorkflow ?? throw new ArgumentNullException(nameof(analysisWorkflow));
    private readonly IEngineAnalyzer engineAnalyzer = engineAnalyzer ?? throw new ArgumentNullException(nameof(engineAnalyzer));
    private IMainWindowAnalysisRun? analysisRun;

    public Task<GameAnalysisResult> AnalyzeGameAsync(
        ImportedGame game,
        PlayerSide side,
        EngineAnalysisOptions options,
        IProgress<GameAnalysisProgress>? progress,
        CancellationToken cancellationToken = default)
    {
        analysisRun ??= this.analysisWorkflow.CreateAnalysisRun(this.engineAnalyzer);
        return analysisRun.AnalyzeGameAsync(game, side, options, progress, cancellationToken);
    }

    public void Dispose()
    {
        analysisRun?.Dispose();
    }
}
