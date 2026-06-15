namespace MoveMentorChess.Training;

internal sealed record SavedOpeningReplay(
    OpeningTrainerSnapshot Snapshot,
    ImportedGame Game,
    IReadOnlyList<ReplayPly> Replay);
