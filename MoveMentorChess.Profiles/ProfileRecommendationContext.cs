namespace MoveMentorChess.Profiles;

internal sealed record RecommendationContext(
    GamePhase? DominantPhase,
    PlayerSide? DominantSide,
    IReadOnlyList<string> TopOpenings);

internal sealed record RecommendationOccurrence(
    PlayerSide Side,
    GamePhase? Phase,
    string Eco);
