namespace MoveMentorChess.Profiles;

public sealed record ProfileDataAvailability(
    int ImportedGames,
    int AnalyzedProfiles,
    int OpeningTreePositions);
