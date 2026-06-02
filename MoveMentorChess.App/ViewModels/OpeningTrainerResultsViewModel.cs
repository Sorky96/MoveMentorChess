using System.Collections.ObjectModel;
using System.Globalization;

namespace MoveMentorChess.App.ViewModels;

public sealed class OpeningTrainerResultsViewModel : ViewModelBase
{
    private TrainingSessionOutcomeSummary? outcomeSummary;
    private TrainingResultLearningPlan? learningPlan;
    private TrainingNextAction? selectedNextAction;
    private TrainingNextActionCardViewModel? selectedSecondaryNextAction;
    private string resultHeadline = "Finish practice to see your review plan.";
    private string resultRecommendation = "Your next review suggestion will appear here.";

    public ObservableCollection<string> ResultItems { get; } = [];

    public ObservableCollection<TrainingNextAction> NextActionItems { get; } = [];

    public ObservableCollection<TrainingNextActionCardViewModel> NextActionCards { get; } = [];

    public ObservableCollection<TrainingNextActionCardViewModel> SecondaryNextActionCards { get; } = [];

    public ObservableCollection<TrainingResultReviewItem> LearningPlanReviewItems { get; } = [];

    public string ResultHeadline
    {
        get => resultHeadline;
        private set => SetProperty(ref resultHeadline, value);
    }

    public string ResultRecommendation
    {
        get => resultRecommendation;
        private set => SetProperty(ref resultRecommendation, value);
    }

    public TrainingSessionOutcomeSummary? OutcomeSummary
    {
        get => outcomeSummary;
        private set => SetProperty(ref outcomeSummary, value);
    }

    public TrainingResultLearningPlan? LearningPlan
    {
        get => learningPlan;
        private set
        {
            if (SetProperty(ref learningPlan, value))
            {
                RaiseLearningPlanStateChanged();
            }
        }
    }

    public string LearningPlanMasteredText => LearningPlan?.MasteredText ?? "Mastered: finish practice to build a plan.";

    public string LearningPlanRepeatText => LearningPlan?.RepeatText ?? "To review: finish practice first.";

    public string LearningPlanNextReviewText => LearningPlan?.NextReviewText ?? "Next review: finish practice first.";

    public string LearningPlanReasonText => LearningPlan?.ReasonText ?? "Reason: the trainer will use your moves, hints, and misses.";

    public string ResultsMasteredLabel => "Completed";

    public string ResultsNeedsReviewLabel => "To Revisit";

    public string ResultsBiggestWeaknessText(int wrongAttempts, TrainingResultTone tone)
        => OpeningTrainerResultPresentation.BuildBiggestWeaknessText(tone, wrongAttempts);

    public string ResultsNextBestActionText => SelectedNextAction?.Title ?? "Finish practice to unlock the next best action.";

    public string ResultsNextActionReasonText => SelectedNextAction?.Description ?? LearningPlanReasonText;

    public bool HasAdvancedResultDetails => ResultItems.Count > 0 || LearningPlanReviewItems.Count > 0;

    public string ResultCelebrationTitle(int wrongAttempts, TrainingResultTone tone)
        => OpeningTrainerResultPresentation.BuildCelebrationTitle(tone, wrongAttempts);

    public string ResultCelebrationText(int completedSteps, TrainingResultTone tone)
        => OpeningTrainerResultPresentation.BuildCelebrationText(tone, completedSteps);

    public string ResultOutcomeBadge(int wrongAttempts, TrainingResultTone tone)
        => OpeningTrainerResultPresentation.BuildOutcomeBadge(tone, wrongAttempts);

    public string ResultNextStepSummary(TrainingResultTone tone)
        => OpeningTrainerResultPresentation.BuildNextStepSummary(tone, PrimaryNextAction);

    public bool HasLearningPlanReviewItems => LearningPlanReviewItems.Count > 0;

    public string LearningPlanReviewPlaceholder => HasLearningPlanReviewItems
        ? string.Empty
        : "No urgent review positions from this run.";

    public string ResultsCompletedMetricText(OpeningTrainingSession? guidedSession, int completedSteps)
        => guidedSession is null
            ? "0/0"
            : $"{completedSteps}/{guidedSession.Positions.Count}";

    public static string ResultsClearMetricText(int correctAnswers)
        => correctAnswers.ToString(CultureInfo.InvariantCulture);

    public static string ResultsAlternativesMetricText(int playableAnswers)
        => playableAnswers.ToString(CultureInfo.InvariantCulture);

    public static string ResultsHintsMetricText(int hintUseCount)
        => hintUseCount.ToString(CultureInfo.InvariantCulture);

    public static string ResultsRevisitMetricText(int wrongAttempts)
        => wrongAttempts.ToString(CultureInfo.InvariantCulture);

    public TrainingNextAction? SelectedNextAction
    {
        get => selectedNextAction;
        set
        {
            if (SetProperty(ref selectedNextAction, value))
            {
                OnPropertyChanged(nameof(SelectedNextActionButtonText));
                OnPropertyChanged(nameof(ResultsNextBestActionText));
                OnPropertyChanged(nameof(ResultsNextActionReasonText));
            }
        }
    }

    public bool HasNextActions => NextActionItems.Count > 0;

    public string NextActionsPlaceholder => HasNextActions
        ? string.Empty
        : "Finish a session to unlock the next action plan.";

    public string SelectedNextActionButtonText => SelectedNextAction?.CommandLabel ?? "Select next action";

    public TrainingNextActionCardViewModel? PrimaryNextAction => NextActionCards.FirstOrDefault();

    public bool HasPrimaryNextAction => PrimaryNextAction is not null;

    public TrainingNextActionCardViewModel? SelectedSecondaryNextAction
    {
        get => selectedSecondaryNextAction;
        set => SetProperty(ref selectedSecondaryNextAction, value);
    }

    public bool HasSecondaryNextActions => SecondaryNextActionCards.Count > 0;

    public void Reset()
    {
        ResultHeadline = "Practice in progress.";
        ResultRecommendation = "Finish the run to get your next review step.";
        LearningPlan = null;
        ReplaceItems(ResultItems, []);
        ReplaceItems(LearningPlanReviewItems, []);
        ReplaceItems(NextActionItems, []);
        RebuildNextActionCards();
        SelectedNextAction = null;
        OutcomeSummary = null;
        RaiseNextActionStateChanged();
        RaiseLearningPlanStateChanged();
        RaiseResultsStateChanged();
    }

    public void AddResultLine(OpeningTrainingAttemptResult result)
    {
        string label = result.Status == OpeningTrainingAttemptStatus.TransposedToKnownPosition
            ? "Transposed"
            : result.Score.ToString();
        ResultItems.Insert(0, $"{label} | {result.SubmittedMoveText} | {result.ShortExplanation}");
        OnPropertyChanged(nameof(HasAdvancedResultDetails));
    }

    public void CompleteSession(
        TrainingSessionOutcomeSummary summary,
        OpeningTrainingSession? session,
        string openingName,
        int wrongAttempts,
        int playableAnswers,
        int transposedAnswers,
        IReadOnlyList<TrainingNextAction> nextActions,
        TrainingResultLearningPlan sessionLearningPlan)
    {
        OutcomeSummary = summary;
        ResultHeadline = OpeningTrainerResultPresentation.BuildCompletionHeadline(session?.Positions.Count, openingName);
        ResultRecommendation = OpeningTrainerResultPresentation.BuildCompletionRecommendation(
            wrongAttempts,
            playableAnswers,
            transposedAnswers);
        ReplaceItems(NextActionItems, nextActions);
        RebuildNextActionCards();
        LearningPlan = sessionLearningPlan;
        ReplaceItems(LearningPlanReviewItems, sessionLearningPlan.ReviewItems);
        SelectedNextAction = NextActionItems.FirstOrDefault();
        RaiseLearningPlanStateChanged();
        RaiseNextActionStateChanged();
        RaiseResultsStateChanged();
    }

    public void RaiseResultsStateChanged()
    {
        OnPropertyChanged(nameof(ResultsNextBestActionText));
        OnPropertyChanged(nameof(ResultsNextActionReasonText));
        OnPropertyChanged(nameof(HasAdvancedResultDetails));
        OnPropertyChanged(nameof(OutcomeSummary));
    }

    public void RaiseNextActionStateChanged()
    {
        OnPropertyChanged(nameof(HasNextActions));
        OnPropertyChanged(nameof(NextActionsPlaceholder));
        OnPropertyChanged(nameof(SelectedNextActionButtonText));
        OnPropertyChanged(nameof(PrimaryNextAction));
        OnPropertyChanged(nameof(HasPrimaryNextAction));
        OnPropertyChanged(nameof(HasSecondaryNextActions));
        OnPropertyChanged(nameof(ResultsNextBestActionText));
        OnPropertyChanged(nameof(ResultsNextActionReasonText));
    }

    public void RaiseLearningPlanStateChanged()
    {
        OnPropertyChanged(nameof(LearningPlan));
        OnPropertyChanged(nameof(LearningPlanMasteredText));
        OnPropertyChanged(nameof(LearningPlanRepeatText));
        OnPropertyChanged(nameof(LearningPlanNextReviewText));
        OnPropertyChanged(nameof(LearningPlanReasonText));
        OnPropertyChanged(nameof(ResultsNextActionReasonText));
        OnPropertyChanged(nameof(HasLearningPlanReviewItems));
        OnPropertyChanged(nameof(LearningPlanReviewPlaceholder));
        OnPropertyChanged(nameof(HasAdvancedResultDetails));
    }

    private void RebuildNextActionCards()
    {
        IReadOnlyList<TrainingNextActionCardViewModel> cards = NextActionItems
            .Select((action, index) => TrainingNextActionCardViewModel.Create(action, index == 0))
            .ToList();
        ReplaceItems(NextActionCards, cards);
        ReplaceItems(SecondaryNextActionCards, cards.Skip(1).ToList());
        SelectedNextAction = NextActionItems.FirstOrDefault();
        SelectedSecondaryNextAction = SecondaryNextActionCards.FirstOrDefault();
        RaiseNextActionStateChanged();
    }

    private static void ReplaceItems<T>(ObservableCollection<T> collection, IReadOnlyList<T> items)
    {
        collection.Clear();
        foreach (T item in items)
        {
            collection.Add(item);
        }
    }
}
