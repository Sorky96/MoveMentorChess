namespace MoveMentorChess.Training;

internal sealed record BranchRoot(
    OpeningTrainerSnapshot Snapshot,
    StoredMoveAnalysis AnchorMove,
    string RootFen,
    int AnchorPly,
    string AnchorSan,
    string? MistakeLabel,
    OpeningIssue? FirstIssue,
    string? ThemeLabel,
    int Priority,
    IReadOnlyList<StoredMoveAnalysis> SampleLine,
    IReadOnlyList<OpeningTrainingBranch> Branches,
    IReadOnlyList<OpeningTrainingMoveOption> CandidateMoves,
    OpeningTrainingMoveOption? PrimaryRecommendedResponse,
    IReadOnlyList<OpeningTrainingMove> PrimaryContinuation,
    string BranchSelectionSummary);
