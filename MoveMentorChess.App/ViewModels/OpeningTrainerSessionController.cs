using System.Globalization;

namespace MoveMentorChess.App.ViewModels;

/// <summary>
/// Manages the in-session flow for a guided opening study session.
/// This class was extracted from <see cref="OpeningTrainerWindowViewModel"/> to give it a single
/// responsibility: owning the session state machine (start → step navigation → hints / don't-know
/// / reference reveal → completion / abandonment).
///
/// The VM creates this controller and wires the <see cref="ISessionFlowCallbacks"/> so that all
/// observable side-effects (property notifications, UI list updates, page transitions) remain in
/// the ViewModel while the pure session logic lives here.
/// </summary>
internal sealed class OpeningTrainerSessionController
{
    private readonly OpeningTrainerWorkspaceService workspaceService;
    private readonly ISessionFlowCallbacks callbacks;

    // Session state
    private OpeningTrainingSession? guidedSession;
    private int currentStepIndex;
    private int completedSteps;
    private int correctAnswers;
    private int playableAnswers;
    private int wrongAttempts;
    private int transposedAnswers;
    private int hintUseCount;
    private int currentHintIndex;
    private DateTime? studyStartedUtc;
    private DateTime? firstMoveUtc;
    private string? currentStartSource;
    private string? currentRecommendationId;
    private bool studyAbandonedTracked;
    private bool sessionResultSaved;
    private readonly List<OpeningTrainingAttemptResult> currentSessionAttempts = [];
    private readonly HashSet<string> completedNextActionIds = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> scheduledActionIdsBySource = new(StringComparer.OrdinalIgnoreCase);

    internal OpeningTrainerSessionController(
        OpeningTrainerWorkspaceService workspaceService,
        ISessionFlowCallbacks callbacks)
    {
        this.workspaceService = workspaceService ?? throw new ArgumentNullException(nameof(workspaceService));
        this.callbacks = callbacks ?? throw new ArgumentNullException(nameof(callbacks));
    }

    // -------------------------------------------------------------------------
    // Derived read-only state (used by VM computed properties)
    // -------------------------------------------------------------------------

    internal OpeningTrainingSession? GuidedSession => guidedSession;
    internal int CurrentStepIndex => currentStepIndex;
    internal int CompletedSteps => completedSteps;
    internal int CorrectAnswers => correctAnswers;
    internal int PlayableAnswers => playableAnswers;
    internal int WrongAttempts => wrongAttempts;
    internal int TransposedAnswers => transposedAnswers;
    internal int HintUseCount => hintUseCount;
    internal string? CurrentRecommendationId => currentRecommendationId;
    internal string? CurrentStartSource => currentStartSource;
    internal IReadOnlyList<OpeningTrainingAttemptResult> CurrentSessionAttempts => currentSessionAttempts;

    internal OpeningTrainingPosition? CurrentPosition
        => guidedSession is null || currentStepIndex >= guidedSession.Positions.Count
            ? null
            : guidedSession.Positions[currentStepIndex];

    internal bool CanUseDontKnow
        => CurrentPosition is not null && !HasDontKnowAttemptForCurrentPosition();

    internal bool HasSession => guidedSession is not null;

    internal int GetTimeToFirstMoveSeconds()
        => studyStartedUtc.HasValue && firstMoveUtc.HasValue
            ? Math.Max(0, (int)Math.Round((firstMoveUtc.Value - studyStartedUtc.Value).TotalSeconds))
            : 0;

    internal int CountDontKnowAttempts()
        => currentSessionAttempts.Count(attempt =>
            string.Equals(attempt.SubmittedMoveText, "I do not know", StringComparison.OrdinalIgnoreCase));

    internal TrainingSessionOutcomeSummary? OutcomeSummary { get; private set; }

    // -------------------------------------------------------------------------
    // Session lifecycle
    // -------------------------------------------------------------------------

    internal void StartSession(
        OpeningTrainingSession session,
        string startSource,
        string? recommendationId,
        bool studyAbandonedTracked = false)
    {
        guidedSession = session ?? throw new ArgumentNullException(nameof(session));
        studyStartedUtc = workspaceService.UtcNow;
        firstMoveUtc = null;
        currentStartSource = startSource;
        currentRecommendationId = startSource is "today_recommendation" or "overview_priority"
            ? recommendationId
            : null;
        this.studyAbandonedTracked = studyAbandonedTracked;
        sessionResultSaved = false;
        currentSessionAttempts.Clear();
        completedNextActionIds.Clear();
        scheduledActionIdsBySource.Clear();
        currentStepIndex = 0;
        ResetCounters();
    }

    internal void TrackAbandonmentIfLeavingStudy(int currentPageIndex, int nextPageIndex, int studyPageIndex, int resultsPageIndex)
    {
        if (studyAbandonedTracked
            || currentPageIndex != studyPageIndex
            || nextPageIndex == studyPageIndex
            || nextPageIndex == resultsPageIndex
            || guidedSession is null
            || sessionResultSaved
            || completedSteps >= guidedSession.Positions.Count)
        {
            return;
        }

        studyAbandonedTracked = true;
        sessionResultSaved = true;
        DateTime abandonedUtc = workspaceService.UtcNow;
        workspaceService.SaveSessionResult(
            guidedSession,
            currentSessionAttempts,
            OpeningTrainingSessionOutcome.Abandoned,
            currentStartSource,
            currentRecommendationId,
            hintUseCount,
            GetTimeToFirstMoveSeconds(),
            abandonedUtc,
            completedNextActionIds.ToList());
        callbacks.OnSessionAbandoned(guidedSession, currentRecommendationId, currentStartSource, completedSteps, GetTimeToFirstMoveSeconds());
    }

    // -------------------------------------------------------------------------
    // Step navigation
    // -------------------------------------------------------------------------

    internal void MoveNext()
    {
        if (guidedSession is null)
        {
            return;
        }

        if (currentStepIndex < guidedSession.Positions.Count - 1)
        {
            currentStepIndex++;
            ResetCurrentHintProgression();
            callbacks.OnStepLoaded(CurrentPosition, currentStepIndex, guidedSession);
        }
        else
        {
            CompleteStudy();
        }

        callbacks.OnStudyNavigationStateChanged();
    }

    internal void MovePrevious()
    {
        if (guidedSession is null)
        {
            return;
        }

        if (currentStepIndex > 0)
        {
            currentStepIndex--;
            ResetCurrentHintProgression();
            callbacks.OnStepLoaded(CurrentPosition, currentStepIndex, guidedSession);
        }

        callbacks.OnStudyNavigationStateChanged();
    }

    internal void LoadCurrentStep()
    {
        ResetCurrentHintProgression();
        callbacks.OnStepLoaded(CurrentPosition, currentStepIndex, guidedSession);
        callbacks.OnStudyNavigationStateChanged();
    }

    // -------------------------------------------------------------------------
    // Move evaluation
    // -------------------------------------------------------------------------

    internal EvaluateResult EvaluateMove(string submittedAnswer)
    {
        OpeningTrainingPosition? position = CurrentPosition;
        if (position is null)
        {
            return EvaluateResult.None;
        }

        firstMoveUtc ??= workspaceService.UtcNow;
        OpeningTrainingAttemptResult result = workspaceService.Evaluate(position, submittedAnswer);
        currentSessionAttempts.Add(result);

        if (result.Score == OpeningTrainingScore.Wrong)
        {
            wrongAttempts++;
            return new EvaluateResult(result, position, accepted: false);
        }

        completedSteps++;
        if (result.Score == OpeningTrainingScore.Correct)
        {
            correctAnswers++;
        }
        else
        {
            playableAnswers++;
        }

        if (result.Status == OpeningTrainingAttemptStatus.TransposedToKnownPosition)
        {
            transposedAnswers++;
        }

        guidedSession = workspaceService.RebuildContinuationAfterAcceptedMove(
            guidedSession!,
            currentStepIndex,
            position,
            result);

        return new EvaluateResult(result, position, accepted: true);
    }

    // -------------------------------------------------------------------------
    // Hints / Don't know / Reference reveal
    // -------------------------------------------------------------------------

    internal HintResult ShowNextHint(bool trackAsDontKnow = false)
    {
        OpeningTrainingPosition? position = CurrentPosition;
        if (position is null)
        {
            return HintResult.None;
        }

        IReadOnlyList<TrainingCoachHint> hints = workspaceService.BuildCoachHints(position);
        if (hints.Count == 0)
        {
            return HintResult.NoHint;
        }

        TrainingCoachHint hint = hints[Math.Min(currentHintIndex, hints.Count - 1)];
        if (currentHintIndex < hints.Count - 1)
        {
            currentHintIndex++;
        }

        hintUseCount++;

        return new HintResult(hint, trackAsDontKnow, position.PositionId, hintUseCount);
    }

    internal DontKnowResult UseDontKnow()
    {
        OpeningTrainingPosition? position = CurrentPosition;
        if (position is null || HasDontKnowAttemptForCurrentPosition())
        {
            return DontKnowResult.None;
        }

        firstMoveUtc ??= workspaceService.UtcNow;
        OpeningTrainingAttemptResult result = BuildDontKnowAttempt(position);
        currentSessionAttempts.Add(result);
        wrongAttempts++;

        HintResult hint = ShowNextHint(trackAsDontKnow: true);

        return new DontKnowResult(result, hint, position.PositionId, currentStepIndex, hintUseCount, CountDontKnowAttempts());
    }

    internal void MarkReferenceRevealed()
    {
        // nothing to track internally – caller decides IsStudyReferenceVisible;
        // we record nothing per-session, but the telemetry call is in the VM.
    }

    // -------------------------------------------------------------------------
    // Post-session next-action execution
    // -------------------------------------------------------------------------

    internal void CompleteNextAction(string actionId, string? scheduledActionId, TrainingNextActionKind kind)
    {
        completedNextActionIds.Add(actionId);
        if (scheduledActionId is not null && kind == TrainingNextActionKind.RepeatNow)
        {
            workspaceService.MarkScheduledActionCompleted(callbacks.PlayerKey, scheduledActionId);
        }
    }

    internal void RecordScheduledAction(string sourceActionId, string scheduledActionId)
        => scheduledActionIdsBySource[sourceActionId] = scheduledActionId;

    internal bool TryGetScheduledActionId(string sourceActionId, out string? scheduledActionId)
        => scheduledActionIdsBySource.TryGetValue(sourceActionId, out scheduledActionId);

    // -------------------------------------------------------------------------
    // Summary building
    // -------------------------------------------------------------------------

    internal TrainingSessionOutcomeSummary BuildOutcomeSummary(int positionCount)
    {
        double completion = positionCount == 0
            ? 0
            : Math.Round((double)completedSteps / positionCount * 100d, 1);
        int accepted = correctAnswers + playableAnswers;
        double accuracy = completedSteps == 0
            ? 0
            : Math.Round((double)accepted / completedSteps * 100d, 1);

        string headline = wrongAttempts > 0
            ? "Needs reinforcement"
            : playableAnswers > 0 || hintUseCount > 0
                ? "Almost stable"
                : "Stable line";

        OutcomeSummary = new TrainingSessionOutcomeSummary(
            headline,
            positionCount,
            completedSteps,
            correctAnswers,
            playableAnswers,
            wrongAttempts,
            hintUseCount,
            completion,
            accuracy);

        return OutcomeSummary;
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    private void CompleteStudy()
    {
        int positionCount = guidedSession?.Positions.Count ?? 0;
        TrainingSessionOutcomeSummary summary = BuildOutcomeSummary(positionCount);
        OpeningTrainingSessionResult? savedSessionResult = null;

        if (guidedSession is not null && !sessionResultSaved)
        {
            sessionResultSaved = true;
            savedSessionResult = workspaceService.SaveSessionResult(
                guidedSession,
                currentSessionAttempts,
                OpeningTrainingSessionOutcome.Completed,
                currentStartSource,
                currentRecommendationId,
                hintUseCount,
                GetTimeToFirstMoveSeconds(),
                null,
                completedNextActionIds.ToList());
        }

        scheduledActionIdsBySource.Clear();
        callbacks.OnSessionCompleted(summary, guidedSession, savedSessionResult, currentRecommendationId, currentStartSource, completedSteps, wrongAttempts, hintUseCount, CountDontKnowAttempts(), GetTimeToFirstMoveSeconds(), currentSessionAttempts);
    }

    private void ResetCounters()
    {
        completedSteps = 0;
        correctAnswers = 0;
        playableAnswers = 0;
        wrongAttempts = 0;
        transposedAnswers = 0;
        hintUseCount = 0;
        currentHintIndex = 0;
        OutcomeSummary = null;
    }

    private void ResetCurrentHintProgression()
    {
        currentHintIndex = 0;
    }

    private bool HasDontKnowAttemptForCurrentPosition()
    {
        string? positionId = CurrentPosition?.PositionId;
        return !string.IsNullOrWhiteSpace(positionId)
            && currentSessionAttempts.Any(attempt =>
                string.Equals(attempt.PositionId, positionId, StringComparison.Ordinal)
                && string.Equals(attempt.SubmittedMoveText, "I do not know", StringComparison.OrdinalIgnoreCase));
    }

    private static OpeningTrainingAttemptResult BuildDontKnowAttempt(OpeningTrainingPosition position)
    {
        List<OpeningTrainingMoveOption> preferredReferences = position.CandidateMoves
            .Where(option => option.IsPreferred)
            .ToList();

        if (preferredReferences.Count == 0)
        {
            preferredReferences = position.CandidateMoves.Take(1).ToList();
        }

        return new OpeningTrainingAttemptResult(
            position.PositionId,
            position.Mode,
            position.SourceKind,
            OpeningTrainingAttemptStatus.Normal,
            "I do not know",
            null,
            null,
            position.CandidateMoves,
            OpeningTrainingScore.Wrong,
            "Marked for review because you chose to see help before answering.",
            [],
            preferredReferences,
            position.CandidateMoves.Where(option => !preferredReferences.Contains(option)).ToList(),
            null,
            preferredReferences.FirstOrDefault()?.Idea,
            "Use the first hint, then make the prepared move yourself.",
            TrainingCoachHintLevel.Plan,
            TrainingMistakeCategory.Unknown,
            true);
    }

    // -------------------------------------------------------------------------
    // Result value types
    // -------------------------------------------------------------------------

    internal readonly struct EvaluateResult
    {
        internal static EvaluateResult None => new();

        internal EvaluateResult(OpeningTrainingAttemptResult attempt, OpeningTrainingPosition position, bool accepted)
        {
            HasValue = true;
            Attempt = attempt;
            Position = position;
            Accepted = accepted;
        }

        internal bool HasValue { get; }
        internal OpeningTrainingAttemptResult Attempt { get; }
        internal OpeningTrainingPosition Position { get; }
        internal bool Accepted { get; }
    }

    internal readonly struct HintResult
    {
        internal static HintResult None => new();
        internal static readonly HintResult NoHint = new(null, false, null, 0);

        internal HintResult(TrainingCoachHint? hint, bool trackAsDontKnow, string? positionId, int hintUseCount)
        {
            HasValue = true;
            Hint = hint;
            TrackAsDontKnow = trackAsDontKnow;
            PositionId = positionId;
            HintUseCount = hintUseCount;
        }

        internal bool HasValue { get; }
        internal TrainingCoachHint? Hint { get; }
        internal bool TrackAsDontKnow { get; }
        internal string? PositionId { get; }
        internal int HintUseCount { get; }
    }

    internal readonly struct DontKnowResult
    {
        internal static DontKnowResult None => new();

        internal DontKnowResult(
            OpeningTrainingAttemptResult attempt,
            HintResult hint,
            string positionId,
            int stepIndex,
            int hintUseCount,
            int dontKnowCount)
        {
            HasValue = true;
            Attempt = attempt;
            Hint = hint;
            PositionId = positionId;
            StepIndex = stepIndex;
            HintUseCount = hintUseCount;
            DontKnowCount = dontKnowCount;
        }

        internal bool HasValue { get; }
        internal OpeningTrainingAttemptResult Attempt { get; }
        internal HintResult Hint { get; }
        internal string PositionId { get; }
        internal int StepIndex { get; }
        internal int HintUseCount { get; }
        internal int DontKnowCount { get; }
    }

    // -------------------------------------------------------------------------
    // Callback interface – implemented by the ViewModel
    // -------------------------------------------------------------------------

    /// <summary>
    /// All observable effects that <see cref="OpeningTrainerSessionController"/> needs to trigger
    /// on the ViewModel are channelled through this interface.  This avoids a direct dependency
    /// from the controller to the concrete ViewModel class.
    /// </summary>
    internal interface ISessionFlowCallbacks
    {
        string PlayerKey { get; }

        void OnStepLoaded(OpeningTrainingPosition? position, int stepIndex, OpeningTrainingSession? session);

        void OnStudyNavigationStateChanged();

        void OnSessionCompleted(
            TrainingSessionOutcomeSummary summary,
            OpeningTrainingSession? session,
            OpeningTrainingSessionResult? savedResult,
            string? recommendationId,
            string? startSource,
            int completedSteps,
            int wrongAttempts,
            int hintUseCount,
            int dontKnowCount,
            int timeToFirstMoveSeconds,
            IReadOnlyList<OpeningTrainingAttemptResult> attempts);

        void OnSessionAbandoned(
            OpeningTrainingSession session,
            string? recommendationId,
            string? startSource,
            int completedSteps,
            int timeToFirstMoveSeconds);
    }
}
