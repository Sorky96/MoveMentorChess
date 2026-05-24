using MoveMentorChess.Persistence;

namespace MoveMentorChess.App.ViewModels;

internal static class AnalysisResultCacheLoader
{
    public static bool TryLoadExistingResult(
        IAnalysisResultCache cache,
        ImportedGame importedGame,
        PlayerSide side,
        EngineAnalysisOptions analysisOptions,
        IReadOnlyDictionary<PlayerSide, GameAnalysisResult> initialResultsBySide,
        out GameAnalysisResult? result,
        out GameAnalysisCacheKey cacheKey,
        out string statusText)
    {
        ArgumentNullException.ThrowIfNull(cache);

        cacheKey = cache.CreateKey(importedGame, side, analysisOptions);

        if (TryGetInitialResult(importedGame, side, initialResultsBySide, out result) && result is not null)
        {
            statusText = $"Loaded saved analysis for {side}.";
            return true;
        }

        if (cache.TryGetResult(cacheKey, out result) && result is not null)
        {
            statusText = $"Loaded cached analysis for {side}.";
            return true;
        }

        statusText = $"No cached analysis for {side}. Run analysis to generate it.";
        return false;
    }

    public static bool TryGetWindowState(
        IAnalysisResultCache cache,
        ImportedGame importedGame,
        out AnalysisWindowState? state)
    {
        ArgumentNullException.ThrowIfNull(cache);
        return cache.TryGetWindowState(importedGame, out state);
    }

    public static void StoreWindowState(
        IAnalysisResultCache cache,
        ImportedGame importedGame,
        AnalysisWindowState state)
    {
        ArgumentNullException.ThrowIfNull(cache);
        cache.StoreWindowState(importedGame, state);
    }

    public static void StoreResult(IAnalysisResultCache cache, GameAnalysisCacheKey cacheKey, GameAnalysisResult result)
    {
        ArgumentNullException.ThrowIfNull(cache);
        cache.StoreResult(cacheKey, result);
    }

    private static bool TryGetInitialResult(
        ImportedGame importedGame,
        PlayerSide side,
        IReadOnlyDictionary<PlayerSide, GameAnalysisResult> initialResultsBySide,
        out GameAnalysisResult? result)
    {
        result = null;
        if (!initialResultsBySide.TryGetValue(side, out GameAnalysisResult? candidate)
            || !IsAnalysisForGame(candidate, importedGame))
        {
            return false;
        }

        result = candidate;
        return true;
    }

    public static bool IsAnalysisForGame(GameAnalysisResult result, ImportedGame game)
    {
        return string.Equals(
            GameFingerprint.Compute(result.Game.PgnText),
            GameFingerprint.Compute(game.PgnText),
            StringComparison.Ordinal);
    }
}
