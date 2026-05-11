namespace MoveMentorChess.Domain;

public sealed record SpecialTrainingModeDefinition(
    SpecialTrainingModeKind Kind,
    string Title,
    string Description,
    string CommandLabel,
    int TimeLimitMinutes,
    int MaxPositions,
    OpeningTrainingStrictness Strictness,
    IReadOnlyList<OpeningTrainingMode> PreferredModes,
    RepertoireSide PreferredSide = RepertoireSide.Both,
    bool PrioritizeWeakPositions = false,
    bool PrioritizeOpponentReplies = false);

public enum SpecialTrainingModeKind
{
    FiveMinutePrep,
    OpponentPreparation,
    QuickBlackReview,
    RepairWeakestPositions
}
