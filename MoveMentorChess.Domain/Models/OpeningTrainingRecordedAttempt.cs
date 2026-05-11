namespace MoveMentorChess.Domain;

public sealed record OpeningTrainingRecordedAttempt(
    string PositionId,
    OpeningTrainingMode Mode,
    OpeningTrainingSourceKind PositionSource,
    OpeningTrainingAttemptStatus Status,
    string Eco,
    string OpeningName,
    string? ThemeLabel,
    string SubmittedMoveText,
    string? ResolvedSan,
    string? ResolvedUci,
    OpeningTrainingScore Score,
    DateTime RecordedUtc,
    OpeningBranchKey? BranchKey = null,
    OpeningPositionKey? PositionKey = null,
    OpeningKey? OpeningKey = null,
    OpeningLineKey? OpeningLineKey = null)
{
    public OpeningTrainingRecordedAttempt()
        : this(
            string.Empty,
            OpeningTrainingMode.LineRecall,
            OpeningTrainingSourceKind.ExampleGame,
            OpeningTrainingAttemptStatus.Normal,
            string.Empty,
            string.Empty,
            null,
            string.Empty,
            null,
            null,
            OpeningTrainingScore.Wrong,
            DateTime.MinValue,
            null,
            null,
            null,
            null)
    {
    }

    public OpeningTrainingRecordedAttempt(
        string positionId,
        OpeningTrainingMode mode,
        OpeningTrainingSourceKind positionSource,
        string eco,
        string openingName,
        string? themeLabel,
        string submittedMoveText,
        string? resolvedSan,
        string? resolvedUci,
        OpeningTrainingScore score,
        DateTime recordedUtc)
        : this(
            positionId,
            mode,
            positionSource,
            OpeningTrainingAttemptStatus.Normal,
            eco,
            openingName,
            themeLabel,
            submittedMoveText,
            resolvedSan,
            resolvedUci,
            score,
            recordedUtc,
            null,
            null,
            null,
            null)
    {
    }
}
