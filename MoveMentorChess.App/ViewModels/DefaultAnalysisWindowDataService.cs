using MoveMentorChess.Opening;
using MoveMentorChess.Persistence;

namespace MoveMentorChess.App.ViewModels;

internal sealed class DefaultAnalysisWindowDataService : IAnalysisWindowDataService
{
    private readonly Func<IAnalysisStore?> storeProvider;
    private readonly IClock clock;
    private readonly IAnalysisResultCache analysisResultCache;

    public DefaultAnalysisWindowDataService(Func<IAnalysisStore?> storeProvider)
        : this(storeProvider, SystemClock.Instance)
    {
    }

    public DefaultAnalysisWindowDataService(Func<IAnalysisStore?> storeProvider, IClock clock)
        : this(storeProvider, clock, GameAnalysisResultCacheAdapter.Instance)
    {
    }

    internal DefaultAnalysisWindowDataService(
        Func<IAnalysisStore?> storeProvider,
        IClock clock,
        IAnalysisResultCache analysisResultCache)
    {
        this.storeProvider = storeProvider ?? throw new ArgumentNullException(nameof(storeProvider));
        this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
        this.analysisResultCache = analysisResultCache ?? throw new ArgumentNullException(nameof(analysisResultCache));
    }

    public DateTime UtcNow => clock.UtcNow;

    public bool IsAnalysisForGame(GameAnalysisResult result, ImportedGame game)
        => AnalysisResultCacheLoader.IsAnalysisForGame(result, game);

    public bool TryLoadExistingResult(
        ImportedGame importedGame,
        PlayerSide side,
        EngineAnalysisOptions analysisOptions,
        IReadOnlyDictionary<PlayerSide, GameAnalysisResult> initialResultsBySide,
        out GameAnalysisResult? result,
        out GameAnalysisCacheKey cacheKey,
        out string statusText)
        => AnalysisResultCacheLoader.TryLoadExistingResult(
            analysisResultCache,
            importedGame,
            side,
            analysisOptions,
            initialResultsBySide,
            out result,
            out cacheKey,
            out statusText);

    public bool TryGetWindowState(ImportedGame importedGame, out AnalysisWindowState? state)
        => AnalysisResultCacheLoader.TryGetWindowState(analysisResultCache, importedGame, out state);

    public void StoreWindowState(ImportedGame importedGame, AnalysisWindowState state)
        => AnalysisResultCacheLoader.StoreWindowState(analysisResultCache, importedGame, state);

    public void StoreResult(GameAnalysisCacheKey cacheKey, GameAnalysisResult result)
        => AnalysisResultCacheLoader.StoreResult(analysisResultCache, cacheKey, result);

    public void SaveMoveAdviceFeedback(MoveAdviceFeedback feedback)
        => storeProvider()?.SaveMoveAdviceFeedback(feedback);

    public MoveAdviceFeedback? FindLatestFeedback(
        ImportedGame importedGame,
        PlayerSide analyzedSide,
        EngineAnalysisOptions analysisOptions,
        MoveAnalysisResult lead)
        => AnalysisFeedbackService.FindLatestFeedback(
            storeProvider(),
            importedGame,
            analyzedSide,
            analysisOptions,
            lead);

    public OpeningTheoryQueryService? CreateOpeningTheory()
    {
        IAnalysisStore? store = storeProvider();
        return store is null ? null : OpeningTheorySourceResolver.Create(store);
    }
}
