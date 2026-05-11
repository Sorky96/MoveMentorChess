namespace MoveMentorChess.Training;

public sealed class SpecialTrainingModeService
{
    private static readonly IReadOnlyList<SpecialTrainingModeDefinition> Definitions =
    [
        new SpecialTrainingModeDefinition(
            SpecialTrainingModeKind.FiveMinutePrep,
            "5 minutes before a game",
            "A compact warm-up focused on the highest-value moves from the selected line.",
            "Start 5 min prep",
            5,
            5,
            OpeningTrainingStrictness.BookFlexible,
            [OpeningTrainingMode.LineRecall, OpeningTrainingMode.BranchAwareness]),
        new SpecialTrainingModeDefinition(
            SpecialTrainingModeKind.OpponentPreparation,
            "Prepare for an opponent",
            "Prioritize common replies and practical branches before a specific matchup.",
            "Start opponent prep",
            12,
            8,
            OpeningTrainingStrictness.BookFlexible,
            [OpeningTrainingMode.BranchAwareness, OpeningTrainingMode.LineRecall],
            PrioritizeOpponentReplies: true),
        new SpecialTrainingModeDefinition(
            SpecialTrainingModeKind.QuickBlackReview,
            "Quick Black review",
            "A short Black-repertoire refresh with a strict cap on positions.",
            "Start Black review",
            8,
            6,
            OpeningTrainingStrictness.BookFlexible,
            [OpeningTrainingMode.LineRecall],
            RepertoireSide.Black),
        new SpecialTrainingModeDefinition(
            SpecialTrainingModeKind.RepairWeakestPositions,
            "Repair my 3 weakest positions",
            "Start from saved weak positions when available, then fall back to the selected line.",
            "Repair weakest",
            10,
            3,
            OpeningTrainingStrictness.BookFlexible,
            [OpeningTrainingMode.MistakeRepair, OpeningTrainingMode.LineRecall],
            PrioritizeWeakPositions: true)
    ];

    public IReadOnlyList<SpecialTrainingModeDefinition> ListDefinitions() => Definitions;

    public OpeningTrainingSessionOptions BuildOptions(SpecialTrainingModeDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        return new OpeningTrainingSessionOptions(
            Modes: definition.PreferredModes,
            MaxPositions: definition.MaxPositions,
            MaxPositionsPerSource: definition.MaxPositions,
            SelectedSide: definition.PreferredSide,
            TrainingStyle: OpeningTrainingStyle.Memorization,
            Strictness: definition.Strictness,
            PrioritizeOpponentFrequency: definition.PrioritizeOpponentReplies,
            SpecialMode: definition.Kind,
            TimeLimitMinutes: definition.TimeLimitMinutes);
    }
}
