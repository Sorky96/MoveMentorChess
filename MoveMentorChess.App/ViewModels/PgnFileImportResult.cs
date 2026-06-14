namespace MoveMentorChess.App.ViewModels;

public sealed record PgnFileImportResult(
    int ImportedGames,
    int SkippedGames,
    IReadOnlyList<ImportedGame> Games);
