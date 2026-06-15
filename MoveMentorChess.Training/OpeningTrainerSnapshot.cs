namespace MoveMentorChess.Training;

internal sealed record OpeningTrainerSnapshot(
    string GameFingerprint,
    string PlayerKey,
    string DisplayName,
    PlayerSide Side,
    string OpponentName,
    string? DateText,
    string? Result,
    string Eco,
    string OpeningName,
    int Depth,
    int MultiPv,
    int? MoveTimeMs,
    DateTime AnalysisUpdatedUtc,
    IReadOnlyList<StoredMoveAnalysis> OpeningMoves);
