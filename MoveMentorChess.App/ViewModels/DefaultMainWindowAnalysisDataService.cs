using MoveMentorChess.Analysis;
using MoveMentorChess.Opening;
using MoveMentorChess.Persistence;

namespace MoveMentorChess.App.ViewModels;

internal sealed class DefaultMainWindowAnalysisDataService : IMainWindowAnalysisDataService
{
    private readonly Func<IAnalysisStore?> analysisStoreProvider;
    private readonly IAnalysisResultCache analysisResultCache;

    public DefaultMainWindowAnalysisDataService(Func<IAnalysisStore?> analysisStoreProvider)
        : this(analysisStoreProvider, GameAnalysisResultCacheAdapter.Instance)
    {
    }

    internal DefaultMainWindowAnalysisDataService(
        Func<IAnalysisStore?> analysisStoreProvider,
        IAnalysisResultCache analysisResultCache)
    {
        this.analysisStoreProvider = analysisStoreProvider ?? throw new ArgumentNullException(nameof(analysisStoreProvider));
        this.analysisResultCache = analysisResultCache ?? throw new ArgumentNullException(nameof(analysisResultCache));
    }

    public void SaveImportedGame(ImportedGame game)
    {
        IAnalysisStore? store = analysisStoreProvider();
        if (store is null)
        {
            return;
        }

        try
        {
            store.SaveImportedGame(game);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // Import should still succeed even if local persistence is temporarily unavailable.
        }
    }

    public void SaveImportedGames(IReadOnlyList<ImportedGame> games)
    {
        IAnalysisStore? store = analysisStoreProvider();
        if (store is null || games.Count == 0)
        {
            return;
        }

        try
        {
            store.SaveImportedGames(games);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // Import should still succeed even if local persistence is temporarily unavailable.
        }
    }

    public bool TryLoadImportedGame(string gameFingerprint, out ImportedGame? game)
    {
        IAnalysisStore? store = analysisStoreProvider();
        if (store is null)
        {
            game = null;
            return false;
        }

        return store.TryLoadImportedGame(gameFingerprint, out game) && game is not null;
    }

    public bool TryGetCachedAnalysis(ImportedGame game, PlayerSide side, EngineAnalysisOptions options, out GameAnalysisResult? result)
    {
        GameAnalysisCacheKey cacheKey = analysisResultCache.CreateKey(game, side, options);
        return analysisResultCache.TryGetResult(cacheKey, out result) && result is not null;
    }

    public void StoreAnalysisResult(ImportedGame game, PlayerSide side, EngineAnalysisOptions options, GameAnalysisResult result)
    {
        analysisResultCache.StoreResult(analysisResultCache.CreateKey(game, side, options), result);
    }

    public IPlayerMistakeProfileSource? CreatePlayerMistakeProfileSource()
    {
        IAnalysisStore? store = analysisStoreProvider();
        return store is null ? null : new StoreBackedPlayerMistakeProfileSource(() => store);
    }

    public OpeningTheoryQueryService? CreateOpeningTheory()
    {
        IAnalysisStore? store = analysisStoreProvider();
        return store is null ? null : PersistenceOpeningTheorySourceResolver.Create(store);
    }
}
