namespace MoveMentorChess.Profiles;

public sealed record PlayerStrengthEstimateInput(
    string GameFingerprint,
    DateTime? GameDate,
    GameTimeControlCategory TimeControlCategory,
    int? PlayerRating,
    int? OpponentRating,
    double? ActualScore,
    double? ExpectedScore,
    IReadOnlyList<StoredMoveAnalysis> Moves,
    int SameTimeControlSampleSize);
