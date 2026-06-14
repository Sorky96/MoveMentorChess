namespace MoveMentorChess.Domain;

public sealed record PgnBatchParseResult(
    IReadOnlyList<ImportedGame> Games,
    IReadOnlyList<PgnBatchParseError> Errors);
