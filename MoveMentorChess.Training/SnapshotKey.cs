namespace MoveMentorChess.Training;

internal readonly record struct SnapshotKey(
    string GameFingerprint,
    PlayerSide Side);
