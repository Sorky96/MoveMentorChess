using MoveMentorChess.Persistence;

namespace MoveMentorChess.App.ViewModels;

internal sealed class GameAnalysisResultCacheAdapter : IAnalysisResultCache
{
    public static GameAnalysisResultCacheAdapter Instance { get; } = new();

    private GameAnalysisResultCacheAdapter()
    {
    }

    public GameAnalysisCacheKey CreateKey(ImportedGame game, PlayerSide side, EngineAnalysisOptions options)
        => GameAnalysisCache.CreateKey(game, side, options);

    public bool TryGetResult(GameAnalysisCacheKey key, out GameAnalysisResult? result)
        => GameAnalysisCache.TryGetResult(key, out result);

    public void StoreResult(GameAnalysisCacheKey key, GameAnalysisResult result)
        => GameAnalysisCache.StoreResult(key, result);

    public bool TryGetWindowState(ImportedGame importedGame, out AnalysisWindowState? state)
        => GameAnalysisCache.TryGetWindowState(importedGame, out state);

    public void StoreWindowState(ImportedGame importedGame, AnalysisWindowState state)
        => GameAnalysisCache.StoreWindowState(importedGame, state);
}
