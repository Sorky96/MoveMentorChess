using MoveMentorChess.Persistence;

namespace MoveMentorChess.App.ViewModels;

internal sealed class StoreBackedSavedLibraryDataService : ISavedLibraryDataService
{
    private readonly IAnalysisStore analysisStore;
    private readonly IAnalysisResultCache analysisResultCache;

    public StoreBackedSavedLibraryDataService(IAnalysisStore analysisStore)
        : this(analysisStore, GameAnalysisResultCacheAdapter.Instance)
    {
    }

    internal StoreBackedSavedLibraryDataService(IAnalysisStore analysisStore, IAnalysisResultCache analysisResultCache)
    {
        this.analysisStore = analysisStore ?? throw new ArgumentNullException(nameof(analysisStore));
        this.analysisResultCache = analysisResultCache ?? throw new ArgumentNullException(nameof(analysisResultCache));
    }

    public bool IsAvailable => true;

    public IReadOnlyList<SavedImportedGameSummary> ListImportedGames(string? filterText)
        => analysisStore.ListImportedGames(filterText);

    public bool TryLoadImportedGame(string gameFingerprint, out ImportedGame? game)
        => analysisStore.TryLoadImportedGame(gameFingerprint, out game);

    public bool DeleteGameAndCachedAnalysis(string gameFingerprint)
    {
        if (!analysisStore.DeleteImportedGame(gameFingerprint))
        {
            return false;
        }

        analysisResultCache.RemoveGame(gameFingerprint);
        return true;
    }

    public IReadOnlyList<GameAnalysisResult> ListResults(string? filterText, int limit)
        => analysisStore.ListResults(filterText, limit);
}
