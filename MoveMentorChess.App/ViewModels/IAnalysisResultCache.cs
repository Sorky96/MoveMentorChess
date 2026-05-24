using MoveMentorChess.Persistence;

namespace MoveMentorChess.App.ViewModels;

internal interface IAnalysisResultCache
{
    GameAnalysisCacheKey CreateKey(ImportedGame game, PlayerSide side, EngineAnalysisOptions options);

    bool TryGetResult(GameAnalysisCacheKey key, out GameAnalysisResult? result);

    void StoreResult(GameAnalysisCacheKey key, GameAnalysisResult result);

    void RemoveGame(string gameFingerprint);

    bool TryGetWindowState(ImportedGame importedGame, out AnalysisWindowState? state);

    void StoreWindowState(ImportedGame importedGame, AnalysisWindowState state);
}
