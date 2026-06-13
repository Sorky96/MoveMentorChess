namespace MoveMentorChess.Domain;

public interface IAnalysisResultStore
{
    IReadOnlyList<GameAnalysisResult> ListResults(string? filterText = null, int limit = 500);
    bool TryLoadResult(GameAnalysisCacheKey key, out GameAnalysisResult? result);
    void SaveResult(GameAnalysisCacheKey key, GameAnalysisResult result);
}
