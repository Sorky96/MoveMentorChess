using MoveMentorChess.Persistence;

namespace MoveMentorChess.App.ViewModels;

internal sealed class DefaultSavedLibraryDataService : ISavedLibraryDataService
{
    private readonly Func<IAnalysisStore?> analysisStoreProvider;
    private readonly IAnalysisResultCache analysisResultCache;

    public DefaultSavedLibraryDataService(Func<IAnalysisStore?> analysisStoreProvider)
        : this(analysisStoreProvider, GameAnalysisResultCacheAdapter.Instance)
    {
    }

    internal DefaultSavedLibraryDataService(
        Func<IAnalysisStore?> analysisStoreProvider,
        IAnalysisResultCache analysisResultCache)
    {
        this.analysisStoreProvider = analysisStoreProvider ?? throw new ArgumentNullException(nameof(analysisStoreProvider));
        this.analysisResultCache = analysisResultCache ?? throw new ArgumentNullException(nameof(analysisResultCache));
    }

    public bool IsAvailable => analysisStoreProvider() is not null;

    public IReadOnlyList<SavedImportedGameSummary> ListImportedGames(string? filterText)
        => GetRequiredStore().ListImportedGames(filterText);

    public bool TryLoadImportedGame(string gameFingerprint, out ImportedGame? game)
        => GetRequiredStore().TryLoadImportedGame(gameFingerprint, out game);

    public bool DeleteGameAndCachedAnalysis(string gameFingerprint)
    {
        if (!GetRequiredStore().DeleteImportedGame(gameFingerprint))
        {
            return false;
        }

        analysisResultCache.RemoveGame(gameFingerprint);
        return true;
    }

    public IReadOnlyList<GameAnalysisResult> ListResults(string? filterText, int limit)
        => GetRequiredStore().ListResults(filterText, limit);

    private IAnalysisStore GetRequiredStore()
        => analysisStoreProvider() ?? throw new InvalidOperationException("Local analysis store is unavailable.");
}
