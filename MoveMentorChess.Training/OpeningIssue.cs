namespace MoveMentorChess.Training;

internal sealed record OpeningIssue(
    OpeningTrainerSnapshot Snapshot,
    StoredMoveAnalysis Move);
