namespace MoveMentorChess.App.ViewModels;

public sealed record BulkPgnAnalysisResult(
    string? PrimaryPlayer,
    int AnalyzedGames,
    int CachedGames,
    int SkippedGames,
    int FailedGames,
    IReadOnlyList<string> FailureMessages);
