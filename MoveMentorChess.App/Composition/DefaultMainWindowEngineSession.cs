using MoveMentorChess.Analysis;
using MoveMentorChess.App.ViewModels;
using MoveMentorChess.Engine;

namespace MoveMentorChess.App.Composition;

internal sealed class DefaultMainWindowEngineSession : IMainWindowEngineSession
{
    private readonly IStockfishPathResolver stockfishPathResolver;
    private readonly Func<StockfishSettings> settingsProvider;
    private StockfishEngine? engine;

    public DefaultMainWindowEngineSession(IStockfishPathResolver stockfishPathResolver)
        : this(stockfishPathResolver, StockfishSettingsStore.Load)
    {
    }

    internal DefaultMainWindowEngineSession(
        IStockfishPathResolver stockfishPathResolver,
        Func<StockfishSettings> settingsProvider)
    {
        this.stockfishPathResolver = stockfishPathResolver ?? throw new ArgumentNullException(nameof(stockfishPathResolver));
        this.settingsProvider = settingsProvider ?? throw new ArgumentNullException(nameof(settingsProvider));
    }

    public bool IsAvailable => engine is not null;

    public IEngineAnalyzer? Analyzer => engine;

    public string Reload()
    {
        DisposeEngine();
        StockfishEngine? initializedEngine = null;

        try
        {
            string? enginePath = stockfishPathResolver.Resolve();
            if (string.IsNullOrWhiteSpace(enginePath))
            {
                throw new InvalidOperationException("Could not locate the external chess engine executable.");
            }

            StockfishSettings stockfishSettings = settingsProvider();
            initializedEngine = new StockfishEngine(enginePath, stockfishSettings.ToEngineOptions());
            initializedEngine.SendCommand("setoption name MultiPV value 3");
            engine = initializedEngine;
            initializedEngine = null;
            return $"MoveMentor Chess is ready. External chess engine loaded from {enginePath}.";
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            initializedEngine?.Dispose();
            DisposeEngine();
            return $"MoveMentor Chess is ready, but the analysis engine is unavailable. {ex.Message}";
        }
    }

    public MainWindowEngineSummary RefreshSummary(string fen)
    {
        StockfishEngine activeEngine = RequireEngine();
        activeEngine.SetPositionFen(fen);
        return new MainWindowEngineSummary(
            activeEngine.GetTopMoves(3),
            activeEngine.GetEvaluationSummary());
    }

    public EngineAnalysis AnalyzePosition(string fen, EngineAnalysisOptions options)
        => RequireEngine().AnalyzePosition(fen, options);

    public void Dispose()
    {
        DisposeEngine();
    }

    private StockfishEngine RequireEngine()
        => engine ?? throw new InvalidOperationException("The analysis engine is unavailable.");

    private void DisposeEngine()
    {
        engine?.Dispose();
        engine = null;
    }
}
