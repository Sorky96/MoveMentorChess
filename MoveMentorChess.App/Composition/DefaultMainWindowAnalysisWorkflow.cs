using MoveMentorChess.Analysis;
using MoveMentorChess.App.ViewModels;
using MoveMentorChess.Engine;

namespace MoveMentorChess.App.Composition;

internal sealed class DefaultMainWindowAnalysisWorkflow : IMainWindowAnalysisWorkflow
{
    private readonly IMainWindowAnalysisDataService analysisDataService;
    private readonly Func<StockfishSettings> settingsProvider;

    public DefaultMainWindowAnalysisWorkflow(IMainWindowAnalysisDataService analysisDataService)
        : this(analysisDataService, StockfishSettingsStore.Load)
    {
    }

    internal DefaultMainWindowAnalysisWorkflow(
        IMainWindowAnalysisDataService analysisDataService,
        Func<StockfishSettings> settingsProvider)
    {
        this.analysisDataService = analysisDataService ?? throw new ArgumentNullException(nameof(analysisDataService));
        this.settingsProvider = settingsProvider ?? throw new ArgumentNullException(nameof(settingsProvider));
    }

    public EngineAnalysisOptions CreateDefaultAnalysisOptions()
        => new();

    public EngineAnalysisOptions CreateBulkAnalysisOptions()
        => settingsProvider().ToBulkAnalysisOptions();

    public Task<GameAnalysisResult> AnalyzeGameAsync(
        IEngineAnalyzer engineAnalyzer,
        ImportedGame game,
        PlayerSide side,
        EngineAnalysisOptions options,
        IProgress<GameAnalysisProgress>? progress,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(engineAnalyzer);
        ArgumentNullException.ThrowIfNull(game);
        ArgumentNullException.ThrowIfNull(options);

        GameAnalysisService analysisService = new(
            engineAnalyzer,
            openingTheory: analysisDataService.CreateOpeningTheory(),
            playerMistakeProfileSource: analysisDataService.CreatePlayerMistakeProfileSource());

        return Task.Run(
            () => analysisService.AnalyzeGame(game, side, options, progress, cancellationToken),
            cancellationToken);
    }
}
