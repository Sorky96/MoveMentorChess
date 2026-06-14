namespace MoveMentorChess.Domain;

public sealed record PgnBatchParseError(int GameOrdinal, string Message);
