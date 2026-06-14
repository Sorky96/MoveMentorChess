namespace MoveMentorChess.Domain;

public interface IStoredMoveAnalysisStore
{
    IReadOnlyList<StoredMoveAnalysis> ListMoveAnalyses(string? filterText = null, int limit = 5000);
}
