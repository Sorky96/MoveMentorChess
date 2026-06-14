namespace MoveMentorChess.Opening;

public sealed record OpeningTreePruningOptions(
    int MinDistinctGames,
    int MaxMovesPerPosition,
    double MinMoveShare,
    bool AlwaysKeepMainMove)
{
    public static OpeningTreePruningOptions ProductionDefault { get; } = new(
        MinDistinctGames: 30,
        MaxMovesPerPosition: 5,
        MinMoveShare: 0.05,
        AlwaysKeepMainMove: true);
}
