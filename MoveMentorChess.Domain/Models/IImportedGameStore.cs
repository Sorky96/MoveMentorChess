namespace MoveMentorChess.Domain;

public interface IImportedGameStore
{
    void SaveImportedGame(ImportedGame game);
    void SaveImportedGames(IReadOnlyList<ImportedGame> games);
    bool TryLoadImportedGame(string gameFingerprint, out ImportedGame? game);
    bool DeleteImportedGame(string gameFingerprint);
    void ClearImportedAnalysisData();
    IReadOnlyList<SavedImportedGameSummary> ListImportedGames(string? filterText = null, int limit = 200);
}
