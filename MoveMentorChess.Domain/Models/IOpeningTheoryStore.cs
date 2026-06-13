namespace MoveMentorChess.Domain;

public interface IOpeningTheoryStore
{
    bool TryGetOpeningPositionByKey(string positionKey, out OpeningTheoryPosition? position);
    IReadOnlyList<OpeningTheoryMove> GetOpeningMovesByPositionKey(
        string positionKey,
        int limit = 10,
        bool playableOnly = false);
    IReadOnlyList<OpeningLineCatalogItem> ListOpeningLines(string? filterText = null, RepertoireSide? repertoireSide = null, int limit = 100);
    bool TryGetOpeningOverview(
        OpeningLineKey lineKey,
        RepertoireSide repertoireSide,
        int maxDepth,
        out OpeningTrainerOverview? overview);
}
