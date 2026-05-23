using System.Globalization;

namespace MoveMentorChess.Training;

public sealed class OpeningTrainerService
{
        private readonly IImportedGameStore importedGameStore;
    private readonly TrainingAnalysisDataSource analysisDataSource;
    private readonly OpeningTheoryQueryService? openingTheory;
    private readonly IOpeningTrainingHistoryStore? historyStore;
    private readonly IClock clock;
    private readonly OpeningTrainingMoveEvaluator moveEvaluator = new();
    private readonly OpeningTrainingSessionBuilder sessionBuilder;

    public OpeningTrainerService(IAnalysisStore analysisStore)
        : this(analysisStore, SystemClock.Instance)
    {
    }

    public OpeningTrainerService(IAnalysisStore analysisStore, IClock clock)
        : this(
            analysisStore,
            new TrainingAnalysisDataSource(analysisStore, analysisStore),
            OpeningTheorySourceResolver.Create(analysisStore),
            analysisStore as IOpeningTrainingHistoryStore,
            clock)
    {
    }

    public OpeningTrainerService(IImportedGameStore importedGameStore)
        : this(importedGameStore, SystemClock.Instance)
    {
    }

    public OpeningTrainerService(IImportedGameStore importedGameStore, IClock clock)
        : this(
            importedGameStore,
            RequireResultStore(importedGameStore),
            RequireMoveAnalysisStore(importedGameStore),
            importedGameStore as IOpeningTheoryStore,
            importedGameStore as IOpeningTrainingHistoryStore,
            clock)
    {
    }

    public OpeningTrainerService(
        IImportedGameStore importedGameStore,
        IAnalysisResultStore resultStore,
        IStoredMoveAnalysisStore moveAnalysisStore,
        IOpeningTheoryStore? openingTheoryStore = null,
        IOpeningTrainingHistoryStore? historyStore = null)
        : this(importedGameStore, resultStore, moveAnalysisStore, openingTheoryStore, historyStore, SystemClock.Instance)
    {
    }

    public OpeningTrainerService(
        IImportedGameStore importedGameStore,
        IAnalysisResultStore resultStore,
        IStoredMoveAnalysisStore moveAnalysisStore,
        IOpeningTheoryStore? openingTheoryStore,
        IOpeningTrainingHistoryStore? historyStore,
        IClock clock)
        : this(
            importedGameStore,
            new TrainingAnalysisDataSource(moveAnalysisStore, resultStore),
            openingTheoryStore is null ? null : new OpeningTheoryQueryService(openingTheoryStore),
            historyStore,
            clock)
    {
    }

    internal OpeningTrainerService(
        IImportedGameStore importedGameStore,
        TrainingAnalysisDataSource analysisDataSource,
        IOpeningTheoryStore? openingTheoryStore = null,
        IOpeningTrainingHistoryStore? historyStore = null)
        : this(importedGameStore, analysisDataSource, openingTheoryStore, historyStore, SystemClock.Instance)
    {
    }

    internal OpeningTrainerService(
        IImportedGameStore importedGameStore,
        TrainingAnalysisDataSource analysisDataSource,
        IOpeningTheoryStore? openingTheoryStore,
        IOpeningTrainingHistoryStore? historyStore,
        IClock clock)
        : this(
            importedGameStore,
            analysisDataSource,
            openingTheoryStore is null ? null : new OpeningTheoryQueryService(openingTheoryStore),
            historyStore,
            clock)
    {
    }

    internal OpeningTrainerService(
        IImportedGameStore importedGameStore,
        TrainingAnalysisDataSource analysisDataSource,
        OpeningTheoryQueryService? openingTheory,
        IOpeningTrainingHistoryStore? historyStore = null)
        : this(importedGameStore, analysisDataSource, openingTheory, historyStore, SystemClock.Instance)
    {
    }

    internal OpeningTrainerService(
        IImportedGameStore importedGameStore,
        TrainingAnalysisDataSource analysisDataSource,
        OpeningTheoryQueryService? openingTheory,
        IOpeningTrainingHistoryStore? historyStore,
        IClock clock)
    {
        this.importedGameStore = importedGameStore ?? throw new ArgumentNullException(nameof(importedGameStore));
        this.analysisDataSource = analysisDataSource ?? throw new ArgumentNullException(nameof(analysisDataSource));
        this.openingTheory = openingTheory;
        this.historyStore = historyStore;
        this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
        sessionBuilder = new OpeningTrainingSessionBuilder(
            importedGameStore,
            analysisDataSource,
            openingTheory,
            historyStore,
            clock);
    }

    /// <summary>
    /// Builds an opening training session for the given player.  The session-building pipeline
    /// is delegated to <see cref="OpeningTrainingSessionBuilder"/>; this method is a thin facade
    /// kept for API compatibility.
    /// </summary>
    public bool TryBuildSession(string playerKeyOrName, out OpeningTrainingSession? session, OpeningTrainingSessionOptions? options = null)
        => sessionBuilder.TryBuild(playerKeyOrName, options, out session);

    public OpeningTrainingAttemptResult EvaluateMove(OpeningTrainingPosition position, string submittedMoveText)
    {
        ArgumentNullException.ThrowIfNull(position);

        if (position.AnswerKind != OpeningTrainingAnswerKind.Move)
        {
            return EvaluateAnswer(position, submittedMoveText);
        }

        return moveEvaluator.EvaluateMove(position, submittedMoveText);
    }

    public OpeningTrainingAttemptResult EvaluateAnswer(OpeningTrainingPosition position, string answerId)
    {
        ArgumentNullException.ThrowIfNull(position);
        if (position.AnswerKind == OpeningTrainingAnswerKind.Move)
        {
            return EvaluateMove(position, answerId);
        }

        OpeningTrainingAnswerOption? selected = position.AnswerOptions?
            .FirstOrDefault(option => string.Equals(option.Id, answerId, StringComparison.OrdinalIgnoreCase)
                || string.Equals(option.Text, answerId, StringComparison.OrdinalIgnoreCase));
        IReadOnlyList<OpeningTrainingMoveOption> expected = position.AnswerOptions?
            .Where(option => option.IsCorrect)
            .Select(option => new OpeningTrainingMoveOption(
                option.Text,
                option.Id,
                OpeningTrainingMoveRole.Expected,
                true,
                option.Explanation,
                OpeningLineRecallReferenceKind.ReferenceLine,
                OpeningTrainingMoveSourceKind.OpeningBook,
                null,
                null))
            .ToList()
            ?? [];

        if (selected is null)
        {
            return new OpeningTrainingAttemptResult(
                position.PositionId,
                position.Mode,
                position.SourceKind,
                OpeningTrainingAttemptStatus.Normal,
                answerId,
                null,
                answerId,
                expected,
                OpeningTrainingScore.Wrong,
                "The selected answer option is not available for this position.",
                [],
                expected,
                []);
        }

        OpeningTrainingScore score = selected.IsCorrect
            ? OpeningTrainingScore.Correct
            : OpeningTrainingScore.Wrong;
        string explanation = selected.Explanation
            ?? (selected.IsCorrect ? "Correct plan." : "That plan does not fit this position.");
        OpeningTrainingMoveOption selectedReference = new(
            selected.Text,
            selected.Id,
            OpeningTrainingMoveRole.Expected,
            selected.IsCorrect,
            selected.Explanation,
            OpeningLineRecallReferenceKind.ReferenceLine,
            OpeningTrainingMoveSourceKind.OpeningBook,
            null,
            null);

        return new OpeningTrainingAttemptResult(
            position.PositionId,
            position.Mode,
            position.SourceKind,
            OpeningTrainingAttemptStatus.Normal,
            selected.Text,
            selected.Text,
            selected.Id,
            expected,
            score,
            explanation,
            selected.IsCorrect ? [selectedReference] : [],
            expected,
            []);
    }

    public OpeningLineRecallAttemptResult EvaluateLineRecallMove(OpeningTrainingPosition position, string submittedMoveText)
    {
        ArgumentNullException.ThrowIfNull(position);

        if (position.Mode != OpeningTrainingMode.LineRecall)
        {
            throw new ArgumentException("Line recall evaluation is available only for line recall positions.", nameof(position));
        }

        OpeningTrainingAttemptResult result = EvaluateMove(position, submittedMoveText);

        return new OpeningLineRecallAttemptResult(
            result.PositionId,
            result.SubmittedMoveText,
            result.ResolvedSan,
            result.ResolvedUci,
            ToLineRecallGrade(result.Score),
            result.ShortExplanation,
            result.MatchingReferences,
            result.PreferredReferences,
            result.PlayableReferences);
    }

    public OpeningMistakeRepairAttemptResult EvaluateMistakeRepairMove(OpeningTrainingPosition position, string submittedMoveText)
    {
        ArgumentNullException.ThrowIfNull(position);

        if (position.Mode != OpeningTrainingMode.MistakeRepair)
        {
            throw new ArgumentException("Mistake repair evaluation is available only for mistake repair positions.", nameof(position));
        }

        OpeningTrainingAttemptResult result = EvaluateMove(position, submittedMoveText);
        IReadOnlyList<OpeningTrainingMoveOption> preferredReferences = result.PreferredReferences;
        IReadOnlyList<OpeningTrainingMoveOption> playableReferences = result.PlayableReferences;
        string betterMoveSummary = OpeningTrainingMoveEvaluator.BuildBetterMoveSummary(position, preferredReferences);
        string whyBetter = OpeningTrainingMoveEvaluator.BuildWhyBetterSummary(position, preferredReferences, playableReferences);

        return new OpeningMistakeRepairAttemptResult(
            result.PositionId,
            result.SubmittedMoveText,
            result.ResolvedSan,
            result.ResolvedUci,
            ToMistakeRepairGrade(result.Score),
            result.ShortExplanation,
            betterMoveSummary,
            whyBetter,
            result.MatchingReferences,
            result.PreferredReferences,
            result.PlayableReferences);
    }

    public OpeningTrainingSessionResult BuildSessionResult(
        OpeningTrainingSession session,
        IReadOnlyList<OpeningTrainingAttemptResult> attempts,
        OpeningTrainingSessionOutcome outcome = OpeningTrainingSessionOutcome.Completed,
        DateTime? completedUtc = null,
        string? startSource = null,
        string? recommendationId = null,
        int hintCount = 0,
        int? timeToFirstMoveSeconds = null,
        DateTime? abandonedUtc = null,
        IReadOnlyList<string>? completedNextActionIds = null)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(attempts);

        Dictionary<string, OpeningTrainingPosition> positionsById = session.Positions
            .ToDictionary(position => position.PositionId, StringComparer.Ordinal);
        DateTime finishedUtc = completedUtc?.ToUniversalTime() ?? clock.UtcNow;
        List<OpeningTrainingRecordedAttempt> recordedAttempts = attempts
            .Select(attempt =>
            {
                positionsById.TryGetValue(attempt.PositionId, out OpeningTrainingPosition? position);
                return new OpeningTrainingRecordedAttempt(
                    attempt.PositionId,
                    attempt.Mode,
                    attempt.PositionSource,
                    attempt.Status,
                    position?.Eco ?? string.Empty,
                    position?.OpeningName ?? string.Empty,
                    position?.ThemeLabel,
                    attempt.SubmittedMoveText,
                    attempt.ResolvedSan,
                    attempt.ResolvedUci,
                    attempt.Score,
                    finishedUtc,
                    position?.OpeningBranchKey,
                    position?.OpeningPositionKey,
                    position?.OpeningKey,
                    position?.OpeningLineKey);
            })
            .ToList();
        List<OpeningReviewItem> reviewItems = BuildReviewItems(recordedAttempts, finishedUtc);

        return new OpeningTrainingSessionResult(
            session.SessionId,
            session.PlayerKey,
            session.DisplayName,
            session.CreatedUtc,
            finishedUtc,
            outcome,
            session.TrainingStyle,
            session.Strictness,
            session.Positions.Count,
            recordedAttempts.Count,
            recordedAttempts.Count(attempt => attempt.Score == OpeningTrainingScore.Correct),
            recordedAttempts.Count(attempt => attempt.Score == OpeningTrainingScore.Playable),
            recordedAttempts.Count(attempt => attempt.Score == OpeningTrainingScore.Wrong),
            session.Positions
                .Select(position => position.Eco)
                .Concat(recordedAttempts.Select(attempt => attempt.Eco))
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                .ToList(),
            session.Positions
                .Select(position => position.ThemeLabel)
                .Concat(recordedAttempts.Select(attempt => attempt.ThemeLabel))
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                .ToList(),
            recordedAttempts,
            reviewItems,
            startSource,
            recommendationId,
            hintCount,
            timeToFirstMoveSeconds,
            abandonedUtc?.ToUniversalTime(),
            completedNextActionIds);
    }

    public OpeningTrainingSessionResult SaveSessionResult(
        OpeningTrainingSession session,
        IReadOnlyList<OpeningTrainingAttemptResult> attempts,
        OpeningTrainingSessionOutcome outcome = OpeningTrainingSessionOutcome.Completed,
        DateTime? completedUtc = null,
        string? startSource = null,
        string? recommendationId = null,
        int hintCount = 0,
        int? timeToFirstMoveSeconds = null,
        DateTime? abandonedUtc = null,
        IReadOnlyList<string>? completedNextActionIds = null)
    {
        OpeningTrainingSessionResult result = BuildSessionResult(
            session,
            attempts,
            outcome,
            completedUtc,
            startSource,
            recommendationId,
            hintCount,
            timeToFirstMoveSeconds,
            abandonedUtc,
            completedNextActionIds);
        if (historyStore is null)
        {
            throw new InvalidOperationException("The analysis store does not support opening training history.");
        }

        historyStore.SaveOpeningTrainingSessionResult(result);
        if (result.ReviewItems is { Count: > 0 })
        {
            historyStore.SaveOpeningReviewItems(session.PlayerKey, result.ReviewItems);
        }

        return result;
    }

    private static OpeningLineRecallGrade ToLineRecallGrade(OpeningTrainingScore score)
    {
        return score switch
        {
            OpeningTrainingScore.Correct => OpeningLineRecallGrade.Correct,
            OpeningTrainingScore.Playable => OpeningLineRecallGrade.Playable,
            _ => OpeningLineRecallGrade.Wrong
        };
    }

    private static OpeningMistakeRepairGrade ToMistakeRepairGrade(OpeningTrainingScore score)
    {
        return score switch
        {
            OpeningTrainingScore.Correct => OpeningMistakeRepairGrade.Correct,
            OpeningTrainingScore.Playable => OpeningMistakeRepairGrade.Playable,
            _ => OpeningMistakeRepairGrade.Wrong
        };
    }

    private static IAnalysisResultStore RequireResultStore(object store)
    {
        return store as IAnalysisResultStore
            ?? throw new ArgumentException("The store must also provide analysis results.", nameof(store));
    }

    private static IStoredMoveAnalysisStore RequireMoveAnalysisStore(object store)
    {
        return store as IStoredMoveAnalysisStore
            ?? throw new ArgumentException("The store must also provide stored move analyses.", nameof(store));
    }

    private static List<OpeningReviewItem> BuildReviewItems(
        List<OpeningTrainingRecordedAttempt> attempts,
        DateTime finishedUtc)
    {
        return attempts
            .Where(attempt => attempt.BranchKey.HasValue && attempt.PositionKey.HasValue)
            .Select(attempt =>
            {
                int reviewDays = attempt.Score switch
                {
                    OpeningTrainingScore.Wrong => 0,
                    OpeningTrainingScore.Playable => 2,
                    _ => 4
                };
                int correctStreak = attempt.Score == OpeningTrainingScore.Correct ? 1 : 0;
                if (correctStreak >= 3)
                {
                    reviewDays = 10;
                }

                return new OpeningReviewItem(
                    attempt.BranchKey!.Value,
                    attempt.PositionKey!.Value,
                    finishedUtc,
                    finishedUtc.AddDays(reviewDays),
                    attempt.Score == OpeningTrainingScore.Wrong ? 1.3 : attempt.Score == OpeningTrainingScore.Playable ? 1.8 : 2.2,
                    correctStreak,
                    attempt.Score == OpeningTrainingScore.Wrong ? 1 : 0,
                    1,
                    attempt.OpeningKey,
                    attempt.OpeningLineKey);
            })
            .ToList();
    }
}
