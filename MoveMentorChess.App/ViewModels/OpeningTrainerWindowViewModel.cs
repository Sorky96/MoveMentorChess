using System.Collections.ObjectModel;
using System.Globalization;
using Avalonia.Media;
using MoveMentorChess.Localization;
using static MoveMentorChess.App.ViewModels.OpeningTrainerPresentationText;
using MoveMentorChess.Persistence;
using MoveMentorChess.Training;

namespace MoveMentorChess.App.ViewModels;

public sealed class OpeningTrainerWindowViewModel : ViewModelBase
{
    private const int SelectionPageIndex = 0;
    private const int OverviewPageIndex = 1;
    private const int StudyPageIndex = 2;
    private const int ResultsPageIndex = 3;
    private const int TotalPages = 4;

    private readonly OpeningTrainerWorkspaceService workspaceService;
    private readonly OpeningStudyFeedbackAnimator studyFeedbackAnimator = new();
    private readonly OpeningTrainerTelemetryAdapter telemetryAdapter = new();
    private readonly OpeningTrainerResultsViewModel resultsViewModel = new();
    private readonly HashSet<string> studyAvailableTargets = new(StringComparer.OrdinalIgnoreCase);
    private readonly OpeningTrainerSessionController sessionController;
    // Compatibility shims – state still referenced by the rest of the ViewModel.
    // These are now delegated to sessionController.
    private IReadOnlyList<OpeningTrainingAttemptResult> currentSessionAttempts => sessionController.CurrentSessionAttempts;
    private OpeningTrainingSession? guidedSession => sessionController.GuidedSession;
    private int currentStepIndex => sessionController.CurrentStepIndex;
    private int completedSteps => sessionController.CompletedSteps;
    private int correctAnswers => sessionController.CorrectAnswers;
    private int playableAnswers => sessionController.PlayableAnswers;
    private int wrongAttempts => sessionController.WrongAttempts;
    private int transposedAnswers => sessionController.TransposedAnswers;
    private int hintUseCount => sessionController.HintUseCount;
    private OpeningLineCatalogItem? selectedOpening;
    private TrainingRecommendationCard? todayRecommendation;
    private TrainingPriorityItem? selectedPriority;
    private OpeningTrainingAnswerOption? selectedAnswerOption;
    private OpeningTrainingIntensityChoice? selectedIntensityChoice;
    private PlayerOpeningPlan? playerOpeningPlan;
    private SpecialTrainingModeDefinition? selectedSpecialMode;
    private OpeningTrainerOverview? overview;
    private string? studySelectedSquare;
    private string? studyPreviewTargetSquare;
    private int currentPageIndex = SelectionPageIndex;
    private string filterText = string.Empty;
    private string advancedPlayerKey = string.Empty;
    private OpeningTrainingProfileChoice? selectedProfileChoice;
    private RepertoireSide selectedSide = RepertoireSide.Both;
    private OpeningTrainingStrictness selectedStrictness = OpeningTrainingStrictness.BookFlexible;
    private string previewFen = new ChessGame().GetFen();
    private string summaryText = Localizer.Text(LocalizedStrings.OpeningTrainerChooseOpeningPreview);
    private string opponentSummary = Localizer.Text(LocalizedStrings.OpeningTrainerCommonRepliesPlaceholder);
    private string coverageText = Localizer.Text(LocalizedStrings.OpeningTrainerNoPracticeHistory);
    private string coverageExplanation = Localizer.Text(LocalizedStrings.OpeningTrainerPickOpeningCoverage);
    private string currentPrompt = Localizer.Text(LocalizedStrings.OpeningTrainerPracticeIdle);
    private string currentWhy = Localizer.Text(LocalizedStrings.OpeningTrainerWhyPlaceholder);
    private string currentHintText = Localizer.Text(LocalizedStrings.OpeningTrainerHintsPlaceholder);
    private string currentHintLevel = Localizer.Text(LocalizedStrings.OpeningTrainerNoHintUsed);
    private string moveInput = string.Empty;
    private string resultText = string.Empty;
    private string studyFeedbackText = string.Empty;
    private IBrush studyFeedbackBrush = Brushes.Transparent;
    private IBrush studyFeedbackBorderBrush = Brushes.Transparent;
    private double studyFeedbackOpacity;
    private bool isStudyReferenceVisible;
    private bool canRevealStudyReference;
    private bool isAdvancedOptionsExpanded;
    private bool overviewOpenedFromTodayRecommendation;

    public OpeningTrainerWindowViewModel(IAnalysisStore analysisStore)
        : this(new OpeningTrainerWorkspaceService(analysisStore))
    {
    }

    public OpeningTrainerWindowViewModel(OpeningTrainerWorkspaceService workspaceService)
    {
        this.workspaceService = workspaceService ?? throw new ArgumentNullException(nameof(workspaceService));
        sessionController = new OpeningTrainerSessionController(workspaceService, new SessionCallbacks(this));
        RefreshCommand = new RelayCommand(RefreshOpenings);
        GoToOverviewCommand = new RelayCommand(OpenOverviewPage, () => SelectedOpening is not null && overview is not null);
        GoToSelectionCommand = new RelayCommand(() => SetPage(SelectionPageIndex));
        StartRecommendedStudyCommand = new RelayCommand(StartRecommendedStudy, () => TodayRecommendation is not null);
        StartRecommendedPracticeNowCommand = new RelayCommand(StartRecommendedPracticeNow, () => TodayRecommendation is not null);
        StartGuidedStudyCommand = new RelayCommand(StartGuidedStudy, () => SelectedOpening is not null && overview is not null);
        StartPriorityStudyCommand = new RelayCommand(StartPriorityStudy, () => SelectedPriority is not null && SelectedOpening is not null && overview is not null);
        StartSpecialModeCommand = new RelayCommand(StartSpecialMode, () => SelectedSpecialMode is not null && SelectedOpening is not null && overview is not null);
        ShowHintCommand = new RelayCommand(ShowNextHint, () => CurrentPosition is not null);
        DontKnowCommand = new RelayCommand(UseDontKnow, () => CanUseDontKnow);
        RevealStudyReferenceCommand = new RelayCommand(RevealStudyReference, () => CanRevealStudyReference);
        EvaluateMoveCommand = new RelayCommand(EvaluateMove, CanEvaluateCurrentAnswer);
        NextStepCommand = new RelayCommand(MoveNext, () => guidedSession is not null && currentStepIndex < guidedSession.Positions.Count);
        PreviousStepCommand = new RelayCommand(MovePrevious, () => guidedSession is not null && currentStepIndex > 0);
        RestartStudyCommand = new RelayCommand(RestartStudy, () => SelectedOpening is not null && overview is not null);
        ExecuteNextActionCommand = new RelayCommand(ExecuteSelectedNextAction, () => SelectedNextAction is not null);
        ExecutePrimaryNextActionCommand = new RelayCommand(ExecutePrimaryNextAction, () => PrimaryNextAction is not null);
        ExecuteSecondaryNextActionCommand = new RelayCommand<TrainingNextActionCardViewModel>(
            ExecuteSecondaryNextAction,
            action => action is not null);
        ExecuteSelectedSecondaryNextActionCommand = new RelayCommand(
            ExecuteSelectedSecondaryNextAction,
            () => SelectedSecondaryNextAction is not null);

        selectedProfileChoice = AvailableProfileChoices.First(choice => choice.Id == "both");
        selectedIntensityChoice = AvailableIntensityChoices.First(choice => choice.Id == "balanced");
        selectedSide = selectedProfileChoice.Side;
        selectedStrictness = selectedIntensityChoice.Strictness;
        RefreshOpenings();
        workspaceService.TrackTelemetry(
            OpeningTrainingTelemetryEvents.OpeningTrainerOpened,
            PlayerKey,
            SelectedOpening,
            properties: BuildBaseTelemetryProperties());
    }

    public ObservableCollection<OpeningLineCatalogItem> OpeningItems { get; } = [];

    public ObservableCollection<string> MainLineItems { get; } = [];

    public ObservableCollection<string> BranchItems { get; } = [];

    public ObservableCollection<TrainingPriorityItem> PriorityItems { get; } = [];

    public ObservableCollection<string> WeakPositionItems { get; } = [];

    public ObservableCollection<string> ResultItems => resultsViewModel.ResultItems;

    public ObservableCollection<TrainingNextAction> NextActionItems => resultsViewModel.NextActionItems;

    public ObservableCollection<TrainingNextActionCardViewModel> NextActionCards => resultsViewModel.NextActionCards;

    public ObservableCollection<TrainingNextActionCardViewModel> SecondaryNextActionCards => resultsViewModel.SecondaryNextActionCards;

    public ObservableCollection<OpeningTrainingAnswerOption> AnswerOptionItems { get; } = [];

    public ObservableCollection<TrainingResultReviewItem> LearningPlanReviewItems => resultsViewModel.LearningPlanReviewItems;

    public ObservableCollection<OpeningUnderstandingCard> UnderstandingCards { get; } = [];

    public ObservableCollection<PlayerOpeningPlanItem> TodayPlanItems { get; } = [];

    public ObservableCollection<PlayerOpeningPlanItem> WeeklyPlanItems { get; } = [];

    public ObservableCollection<PlayerOpeningPlanItem> LongTermGapItems { get; } = [];

    public ObservableCollection<SpecialTrainingModeDefinition> SpecialTrainingModes { get; } = [];

    public IReadOnlyList<OpeningTrainingProfileChoice> AvailableProfileChoices { get; } =
    [
        new("white", "Play White", "Build today's training from your White repertoire.", RepertoireSide.White, "opening-coach:white"),
        new("black", "Play Black", "Build today's training from your Black repertoire.", RepertoireSide.Black, "opening-coach:black"),
        new("both", "Both Sides", "Use the full repertoire when choosing today's training.", RepertoireSide.Both, "opening-coach:both")
    ];

    public IReadOnlyList<OpeningTrainingIntensityChoice> AvailableIntensityChoices { get; } =
    [
        new("safe", "Safe Review", "Only due known positions.", OpeningTrainingStrictness.StrictRepertoire),
        new("balanced", "Balanced", "Due positions plus weak branches.", OpeningTrainingStrictness.BookFlexible),
        new("challenge", "Challenge", "Adds less familiar opponent replies.", OpeningTrainingStrictness.Exploration)
    ];

    public IReadOnlyList<RepertoireSide> AvailableSides { get; } = Enum.GetValues<RepertoireSide>();

    public IReadOnlyList<OpeningTrainingStrictness> AvailableStrictnessOptions { get; } = Enum.GetValues<OpeningTrainingStrictness>();

    public RelayCommand RefreshCommand { get; }

    public RelayCommand GoToOverviewCommand { get; }

    public RelayCommand GoToSelectionCommand { get; }

    public RelayCommand StartRecommendedStudyCommand { get; }

    public RelayCommand StartRecommendedPracticeNowCommand { get; }

    public RelayCommand StartGuidedStudyCommand { get; }

    public RelayCommand StartPriorityStudyCommand { get; }

    public RelayCommand StartSpecialModeCommand { get; }

    public RelayCommand ShowHintCommand { get; }

    public RelayCommand DontKnowCommand { get; }

    public RelayCommand RevealStudyReferenceCommand { get; }

    public RelayCommand EvaluateMoveCommand { get; }

    public RelayCommand NextStepCommand { get; }

    public RelayCommand PreviousStepCommand { get; }

    public RelayCommand RestartStudyCommand { get; }

    public RelayCommand ExecuteNextActionCommand { get; }

    public RelayCommand ExecutePrimaryNextActionCommand { get; }

    public RelayCommand<TrainingNextActionCardViewModel> ExecuteSecondaryNextActionCommand { get; }

    public RelayCommand ExecuteSelectedSecondaryNextActionCommand { get; }

    public void OpenLineFromCoverage(OpeningLineCatalogItem opening)
    {
        ArgumentNullException.ThrowIfNull(opening);
        SelectedOpening = opening;
        SetPage(OverviewPageIndex);
    }

    public string FilterText
    {
        get => filterText;
        set => SetProperty(ref filterText, value);
    }

    public string PlayerKey => !string.IsNullOrWhiteSpace(AdvancedPlayerKey)
        ? AdvancedPlayerKey.Trim()
        : SelectedProfileChoice?.PlayerKey ?? "opening-coach:both";

    public string ActiveHistoryKeyText => Localizer.Format(LocalizedStrings.OpeningTrainerActiveHistoryKey, PlayerKey);

    public string AdvancedPlayerKey
    {
        get => advancedPlayerKey;
        set
        {
            if (SetProperty(ref advancedPlayerKey, value))
            {
                OnPropertyChanged(nameof(PlayerKey));
                OnPropertyChanged(nameof(ActiveHistoryKeyText));
                RefreshTodayRecommendation();
                LoadOverview();
            }
        }
    }

    public OpeningTrainingProfileChoice? SelectedProfileChoice
    {
        get => selectedProfileChoice;
        set
        {
            if (SetProperty(ref selectedProfileChoice, value))
            {
                OnPropertyChanged(nameof(PlayerKey));
                OnPropertyChanged(nameof(ActiveHistoryKeyText));
                OnPropertyChanged(nameof(SelectedProfileSummary));
                if (value is not null && selectedSide != value.Side)
                {
                    selectedSide = value.Side;
                    OnPropertyChanged(nameof(SelectedSide));
                }

                RefreshOpenings();
            }
        }
    }

    public string SelectedProfileSummary => SelectedProfileChoice is null
        ? "Choose how today's training should pick from your repertoire."
        : SelectedProfileChoice.Description;

    public RepertoireSide SelectedSide
    {
        get => selectedSide;
        set
        {
            if (SetProperty(ref selectedSide, value))
            {
                RefreshOpenings();
            }
        }
    }

    public OpeningTrainingStrictness SelectedStrictness
    {
        get => selectedStrictness;
        set => SetProperty(ref selectedStrictness, value);
    }

    public OpeningLineCatalogItem? SelectedOpening
    {
        get => selectedOpening;
        set
        {
            if (SetProperty(ref selectedOpening, value))
            {
                LoadOverview();
                RaiseNavigationStateChanged();
                OnPropertyChanged(nameof(IsBoardRotated));
            }
        }
    }

    public TrainingRecommendationCard? TodayRecommendation
    {
        get => todayRecommendation;
        private set
        {
            if (SetProperty(ref todayRecommendation, value))
            {
                RaiseTodayRecommendationStateChanged();
            }
        }
    }

    public bool HasTodayRecommendation => TodayRecommendation is not null;

    public string TodayRecommendationOpening => TodayRecommendation?.OpeningLine.DisplayName ?? "No recommendation available";

    public string TodayRecommendationMeta => TodayRecommendation is null
        ? "Import opening theory to enable recommendations."
        : $"{TodayRecommendation.OpeningLine.RepertoireSide} | {TodayRecommendation.Difficulty} | about {TodayRecommendation.EstimatedDurationMinutes} min";

    public string TodayRecommendationReason => TodayRecommendation?.Reason ?? "The trainer needs at least one available opening line.";

    public string TodayRecommendationAction => TodayRecommendation?.RecommendedAction ?? "Start practice";

    public string TodayLessonOpening => TodayRecommendation?.OpeningLine.DisplayName ?? "Choose an opening first";

    public string TodayLessonSideText => TodayRecommendation is null
        ? "No active theory"
        : TodayRecommendation.OpeningLine.RepertoireSide switch
        {
            RepertoireSide.White => "White repertoire",
            RepertoireSide.Black => "Black repertoire",
            _ => "Both sides"
        };

    public string TodayLessonDurationText => TodayRecommendation is null
        ? "Duration appears after import"
        : $"About {TodayRecommendation.EstimatedDurationMinutes} min";

    public string TodayLessonMoveCountText => TodayRecommendation is null
        ? "No positions to train"
        : TodayRecommendation.OpeningLine.BookBranchCount > 0
            ? $"{TodayRecommendation.OpeningLine.BookBranchCount} positions / branches"
            : $"{Math.Max(1, TodayRecommendation.OpeningLine.BookGameCount)} theory games";

    public string TodayLessonReason => TodayRecommendation?.Reason ?? "Import or choose an opening to start today's training.";

    public string TodayDecisionSummary => TodayRecommendation is null
        ? "Recommended today: import or choose an opening before starting."
        : $"Today: {GetRecommendedPositionCount()} review positions from a {GetReviewMoveCount()}-move line, approx. {GetEstimatedDurationText()}, {FormatRepertoireSide(TodayRecommendation.OpeningLine.RepertoireSide)} repertoire.";

    public string TodayStartSequenceText => SelectedIntensityChoice?.Id switch
    {
        "safe" => "Starts with due known positions. Weak-branch repairs stay optional.",
        "challenge" => "Starts with due positions, then adds less familiar opponent replies.",
        _ => "Starts with due positions, then repairs weak branches."
    };

    public string TodayLessonReasonDetail
    {
        get
        {
            if (TodayRecommendation is null)
            {
                return "Once theory is available, the trainer will explain why this line is the next safest use of your time.";
            }

            int weakBranches = overview?.Coverage.WeakBranches ?? TodayRecommendation.OpeningLine.BookBranchCount;
            string reason = TodayRecommendation.Reason.Trim();
            if (TodayRecommendation.ReasonCode == TrainingRecommendationReasonCode.RevisitDue && TodayRecommendation.Priority >= 10_000)
            {
                reason = "This review is due because some scheduled items passed their review window.";
            }

            string modeContext = SelectedIntensityChoice?.Id switch
            {
                "safe" => "Safe Review keeps weak-branch repair optional.",
                "challenge" => "Challenge mode adds less familiar opponent replies after the main line.",
                _ => "Balanced mode adds weak-branch repair after the main line."
            };
            string goal = weakBranches > 0
                ? $"Goal: confirm the main setup and repair {weakBranches} weak branch{PluralSuffix(weakBranches)}."
                : "Goal: confirm the main setup and keep recall stable.";

            return $"{reason} {modeContext}{Environment.NewLine}{goal}";
        }
    }

    public string TodayTrainingReasonLabel => HasTodayLesson
        ? "Recommended because..."
        : "Ready when you are";

    public string TodayLessonButtonText => HasTodayLesson ? "Start guided training" : "Import openings first";

    public bool HasTodayLesson => TodayRecommendation is not null;

    public OpeningTrainingIntensityChoice? SelectedIntensityChoice
    {
        get => selectedIntensityChoice;
        set
        {
            if (SetProperty(ref selectedIntensityChoice, value))
            {
                SelectedStrictness = value?.Strictness ?? OpeningTrainingStrictness.BookFlexible;
                OnPropertyChanged(nameof(SelectedIntensitySummary));
                OnPropertyChanged(nameof(TodayDecisionSummary));
                OnPropertyChanged(nameof(TodayStartSequenceText));
                OnPropertyChanged(nameof(TodayLessonReasonDetail));
                OnPropertyChanged(nameof(PracticeFocusText));
            }
        }
    }

    public string SelectedIntensitySummary => SelectedIntensityChoice?.Description
        ?? "Choose how cautious today's practice should feel.";

    public bool IsAdvancedOptionsExpanded
    {
        get => isAdvancedOptionsExpanded;
        set
        {
            bool wasExpanded = isAdvancedOptionsExpanded;
            if (SetProperty(ref isAdvancedOptionsExpanded, value) && value && !wasExpanded)
            {
                workspaceService.TrackTelemetry(
                    OpeningTrainingTelemetryEvents.OpeningAdvancedOpened,
                    PlayerKey,
                    SelectedOpening,
                    properties: BuildBaseTelemetryProperties());
            }
        }
    }

    public PlayerOpeningPlan? PlayerOpeningPlan
    {
        get => playerOpeningPlan;
        private set => SetProperty(ref playerOpeningPlan, value);
    }

    public string PlayerOpeningPlanTitle => "Your training rhythm";

    public string PlayerOpeningPlanSummary => PlayerOpeningPlan?.Summary ?? "Your training rhythm will appear after loading local theory.";

    public string PlayerOpeningProgressText => PlayerOpeningPlan is null
        ? Localizer.Text(LocalizedStrings.OpeningTrainerNoPracticeHistory)
        : PlayerOpeningPlan.Progress.SessionCount == 0
            ? "Start a session to build repertoire progress."
            : $"{PlayerOpeningPlan.Progress.AttemptCount} moves practiced, {PlayerOpeningPlan.Progress.AccuracyPercent:0.#}% accepted.";

    public string PlayerOpeningProgressInterpretation => PlayerOpeningPlan is null
        ? "Progress history will turn into a short coaching note after your first completed session."
        : PlayerOpeningPlan.Progress.SessionCount == 0
            ? "You are starting fresh, so today's session focuses on building the first reliable recall path."
            : PlayerOpeningPlan.Progress.AccuracyPercent >= 80
                ? "Your accuracy is stable. Today's session focuses on retention, not new material."
                : "Recent practice still has some friction. Today's session keeps the scope controlled and repairs weak spots first.";

    public SpecialTrainingModeDefinition? SelectedSpecialMode
    {
        get => selectedSpecialMode;
        set
        {
            if (SetProperty(ref selectedSpecialMode, value))
            {
                StartSpecialModeCommand.RaiseCanExecuteChanged();
                OnPropertyChanged(nameof(SelectedSpecialModeDescription));
                OnPropertyChanged(nameof(SelectedSpecialModeButtonText));
            }
        }
    }

    public string SelectedSpecialModeDescription => SelectedSpecialMode?.Description ?? "Choose a special mode to start a focused preset.";

    public string SelectedSpecialModeButtonText => SelectedSpecialMode?.CommandLabel ?? "Start special mode";

    public TrainingPriorityItem? SelectedPriority
    {
        get => selectedPriority;
        set
        {
            if (SetProperty(ref selectedPriority, value))
            {
                StartPriorityStudyCommand.RaiseCanExecuteChanged();
                OnPropertyChanged(nameof(SelectedPriorityActionText));
            }
        }
    }

    public bool HasPriorityItems => PriorityItems.Count > 0;

    public string PriorityItemsPlaceholder => HasPriorityItems
        ? string.Empty
        : "No ranked priorities are available for this opening yet.";

    public string SelectedPriorityActionText => SelectedPriority is null
        ? "Select a priority to train it."
        : SelectedPriority.Action switch
        {
            TrainingPriorityAction.RepairThisPosition => "Repair This Position",
            TrainingPriorityAction.ReviewOpponentReply => "Review This Reply",
            _ => "Train Selected Branch"
        };

    public string PreviewFen
    {
        get => previewFen;
        private set => SetProperty(ref previewFen, value);
    }

    public bool IsBoardRotated => SelectedOpening?.RepertoireSide == RepertoireSide.Black;

    public IReadOnlyList<BoardArrowViewModel> PreviewArrows { get; private set; } = [];

    public string SummaryText
    {
        get => summaryText;
        private set => SetProperty(ref summaryText, value);
    }

    public string OpponentSummary
    {
        get => opponentSummary;
        private set => SetProperty(ref opponentSummary, value);
    }

    public string CoverageText
    {
        get => coverageText;
        private set => SetProperty(ref coverageText, value);
    }

    public string CoverageExplanation
    {
        get => coverageExplanation;
        private set => SetProperty(ref coverageExplanation, value);
    }

    public string CurrentPrompt
    {
        get => currentPrompt;
        private set => SetProperty(ref currentPrompt, value);
    }

    public string CurrentWhy
    {
        get => currentWhy;
        private set => SetProperty(ref currentWhy, value);
    }

    public string CurrentHintText
    {
        get => currentHintText;
        private set => SetProperty(ref currentHintText, value);
    }

    public string CurrentHintLevel
    {
        get => currentHintLevel;
        private set
        {
            if (SetProperty(ref currentHintLevel, value))
            {
                OnPropertyChanged(nameof(StudyBoardStatusText));
            }
        }
    }

    public string MoveInput
    {
        get => moveInput;
        set
        {
            if (SetProperty(ref moveInput, value))
            {
                EvaluateMoveCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public OpeningTrainingAnswerOption? SelectedAnswerOption
    {
        get => selectedAnswerOption;
        set
        {
            if (SetProperty(ref selectedAnswerOption, value))
            {
                EvaluateMoveCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool HasAnswerOptions => AnswerOptionItems.Count > 0;

    public string ResultText
    {
        get => resultText;
        private set
        {
            if (SetProperty(ref resultText, value))
            {
                OnPropertyChanged(nameof(StudyFeedbackSummaryText));
            }
        }
    }

    public string StudyFeedbackText
    {
        get => studyFeedbackText;
        private set => SetProperty(ref studyFeedbackText, value);
    }

    public IBrush StudyFeedbackBrush
    {
        get => studyFeedbackBrush;
        private set => SetProperty(ref studyFeedbackBrush, value);
    }

    public IBrush StudyFeedbackBorderBrush
    {
        get => studyFeedbackBorderBrush;
        private set => SetProperty(ref studyFeedbackBorderBrush, value);
    }

    public double StudyFeedbackOpacity
    {
        get => studyFeedbackOpacity;
        private set => SetProperty(ref studyFeedbackOpacity, value);
    }

    public string ResultHeadline => resultsViewModel.ResultHeadline;

    public string ResultRecommendation => resultsViewModel.ResultRecommendation;

    public bool IsStudyReferenceVisible
    {
        get => isStudyReferenceVisible;
        private set
        {
            if (SetProperty(ref isStudyReferenceVisible, value))
            {
                OnPropertyChanged(nameof(StudyReferenceButtonText));
                OnPropertyChanged(nameof(IsStudyReferenceHidden));
                OnPropertyChanged(nameof(StudyReferencePromptText));
                RevealStudyReferenceCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool CanRevealStudyReference
    {
        get => canRevealStudyReference && CurrentPosition is not null && !IsStudyReferenceVisible;
        private set
        {
            if (SetProperty(ref canRevealStudyReference, value))
            {
                OnPropertyChanged(nameof(StudyReferenceButtonText));
                OnPropertyChanged(nameof(StudyReferencePromptText));
                RevealStudyReferenceCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string StudyReferenceButtonText => IsStudyReferenceVisible
        ? "Line shown"
        : "Reveal line";

    public bool IsStudyReferenceHidden => !IsStudyReferenceVisible;

    public string StudyReferencePromptText => CanRevealStudyReference
        ? "The line is available because you chose I Don't Know. Reveal it when you are ready to compare."
        : "Line reference unlocks after I Don't Know, so recall and hints come first.";

    public string DontKnowButtonText => "I Don't Know";

    public bool CanUseDontKnow => sessionController.CanUseDontKnow;

    public TrainingSessionOutcomeSummary? OutcomeSummary => resultsViewModel.OutcomeSummary;

    public TrainingResultLearningPlan? LearningPlan => resultsViewModel.LearningPlan;

    public string LearningPlanMasteredText => resultsViewModel.LearningPlanMasteredText;

    public string LearningPlanRepeatText => resultsViewModel.LearningPlanRepeatText;

    public string LearningPlanNextReviewText => resultsViewModel.LearningPlanNextReviewText;

    public string LearningPlanReasonText => resultsViewModel.LearningPlanReasonText;

    public string ResultsMasteredLabel => resultsViewModel.ResultsMasteredLabel;

    public string ResultsNeedsReviewLabel => resultsViewModel.ResultsNeedsReviewLabel;

    public string ResultsBiggestWeaknessText => resultsViewModel.ResultsBiggestWeaknessText(wrongAttempts, ResultTone);

    public string ResultsNextBestActionText => resultsViewModel.ResultsNextBestActionText;

    public string ResultsNextActionReasonText => resultsViewModel.ResultsNextActionReasonText;

    public bool HasAdvancedResultDetails => resultsViewModel.HasAdvancedResultDetails;

    public string ResultCelebrationTitle => resultsViewModel.ResultCelebrationTitle(wrongAttempts, ResultTone);

    public string ResultCelebrationText => resultsViewModel.ResultCelebrationText(completedSteps, ResultTone);

    public string ResultOutcomeBadge => resultsViewModel.ResultOutcomeBadge(wrongAttempts, ResultTone);

    public string ResultNextStepSummary => resultsViewModel.ResultNextStepSummary(ResultTone);

    public bool HasLearningPlanReviewItems => resultsViewModel.HasLearningPlanReviewItems;

    public string LearningPlanReviewPlaceholder => resultsViewModel.LearningPlanReviewPlaceholder;

    public string ResultsCompletedMetricText => resultsViewModel.ResultsCompletedMetricText(guidedSession, completedSteps);

    public string ResultsClearMetricText => OpeningTrainerResultsViewModel.ResultsClearMetricText(correctAnswers);

    public string ResultsAlternativesMetricText => OpeningTrainerResultsViewModel.ResultsAlternativesMetricText(playableAnswers);

    public string ResultsHintsMetricText => OpeningTrainerResultsViewModel.ResultsHintsMetricText(hintUseCount);

    public string ResultsRevisitMetricText => OpeningTrainerResultsViewModel.ResultsRevisitMetricText(wrongAttempts);

    private TrainingResultTone ResultTone
    {
        get
        {
            return OpeningTrainerResultPresentation.DetermineTone(
                guidedSession is not null,
                completedSteps,
                playableAnswers,
                wrongAttempts,
                hintUseCount);
        }
    }

    public TrainingNextAction? SelectedNextAction
    {
        get => resultsViewModel.SelectedNextAction;
        set
        {
            TrainingNextAction? before = resultsViewModel.SelectedNextAction;
            resultsViewModel.SelectedNextAction = value;
            if (!EqualityComparer<TrainingNextAction?>.Default.Equals(before, resultsViewModel.SelectedNextAction))
            {
                ExecuteNextActionCommand.RaiseCanExecuteChanged();
                OnPropertyChanged(nameof(SelectedNextAction));
                OnPropertyChanged(nameof(SelectedNextActionButtonText));
                OnPropertyChanged(nameof(ResultsNextBestActionText));
                OnPropertyChanged(nameof(ResultsNextActionReasonText));
                ExecutePrimaryNextActionCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool HasNextActions => resultsViewModel.HasNextActions;

    public string NextActionsPlaceholder => resultsViewModel.NextActionsPlaceholder;

    public string SelectedNextActionButtonText => resultsViewModel.SelectedNextActionButtonText;

    public TrainingNextActionCardViewModel? PrimaryNextAction => resultsViewModel.PrimaryNextAction;

    public bool HasPrimaryNextAction => resultsViewModel.HasPrimaryNextAction;

    public TrainingNextActionCardViewModel? SelectedSecondaryNextAction
    {
        get => resultsViewModel.SelectedSecondaryNextAction;
        set
        {
            TrainingNextActionCardViewModel? before = resultsViewModel.SelectedSecondaryNextAction;
            resultsViewModel.SelectedSecondaryNextAction = value;
            if (!EqualityComparer<TrainingNextActionCardViewModel?>.Default.Equals(before, resultsViewModel.SelectedSecondaryNextAction))
            {
                OnPropertyChanged(nameof(SelectedSecondaryNextAction));
                ExecuteSecondaryNextActionCommand.RaiseCanExecuteChanged();
                ExecuteSelectedSecondaryNextActionCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool HasSecondaryNextActions => resultsViewModel.HasSecondaryNextActions;

    public string? StudySelectedSquare => studySelectedSquare;

    public string? StudyPreviewTargetSquare
    {
        get => studyPreviewTargetSquare;
        private set => SetProperty(ref studyPreviewTargetSquare, value);
    }

    public IReadOnlyList<string> StudyAvailableMoveSquares => studyAvailableTargets.ToList();

    public string StudyBoardHint => CurrentPosition is null
        ? "Start practice to use the board."
        : studySelectedSquare is null
            ? "Select a piece, then choose a highlighted square."
            : $"Selected {studySelectedSquare}. Click a highlighted target square to play the move.";

    public string StudyInputModeText => CurrentPosition is null
        ? "Board input is idle."
        : CurrentPosition.AnswerKind == OpeningTrainingAnswerKind.Move
            ? "Accepted: main repertoire move and sound theory alternatives."
            : "Choose the answer option that best explains the position.";

    public string StudyTaskTitle => CurrentPosition is null
        ? "Find the repertoire move"
        : CurrentPosition.AnswerKind == OpeningTrainingAnswerKind.Move
            ? $"Find {CurrentPosition.SideToMove}'s repertoire move"
            : "Choose the best plan";

    public string StudyTaskSubtitle => guidedSession is null
        ? "Position 0 of 0"
        : $"Position {Math.Min(currentStepIndex + 1, guidedSession.Positions.Count)} of {guidedSession.Positions.Count} - {SelectedOpeningName}";

    public string StudyBoardStatusText => CurrentPosition is null
        ? "No position loaded"
        : AppendCurrentPositionPracticeStatus($"{CurrentPosition.SideToMove} to move - {StudyProgressText} - {CurrentHintLevel}");

    public string StudySideToMoveText => CurrentPosition is null
        ? "No position loaded"
        : $"{CurrentPosition.SideToMove} to move";

    public string StudyMovePromptText => CurrentPosition is null
        ? "Start practice to see the move prompt."
        : CurrentPosition.AnswerKind == OpeningTrainingAnswerKind.Move
            ? $"{CurrentPosition.SideToMove} to move. Find the prepared move from your repertoire."
            : "Choose the answer that matches the plan for this position.";

    public string StudyFeedbackSummaryText => string.IsNullOrWhiteSpace(ResultText)
        ? "No move submitted yet."
        : ResultText;

    public string CurrentPositionGoalText => CurrentPosition is null
        ? "Start practice to see the current goal."
        : !string.IsNullOrWhiteSpace(CurrentPosition.ThemeLabel)
            ? $"Train the {CurrentPosition.ThemeLabel.ToLowerInvariant()} idea in this position."
            : CurrentPosition.AnswerKind == OpeningTrainingAnswerKind.Move
                ? "Train the next prepared move from memory."
                : "Train the idea behind this position before moving on.";

    public string CurrentMoveTrainingPurposeText => CurrentPosition is null
        ? "The trainer will show what this move is meant to build."
        : CurrentPosition.CandidateMoves.FirstOrDefault(move => move.IsPreferred)?.Idea?.ShortExplanation
            ?? CurrentPosition.CandidateMoves.FirstOrDefault(move => move.IsPreferred)?.Note
            ?? CurrentPosition.BetterMoveReason
            ?? CurrentPosition.Instruction;

    public string CurrentAttemptHistoryText => currentSessionAttempts.Count == 0
        ? "No moves submitted yet in this run."
        : $"This run: {currentSessionAttempts.Count} submitted, {correctAnswers} clear, {playableAnswers} accepted alternative(s), {wrongAttempts} to revisit.";

    public string SessionCorrectCountText => $"{correctAnswers} clear";

    public string SessionAcceptedAlternativesText => $"{playableAnswers} accepted alternative(s)";

    public string SessionNeedsReviewText => $"{wrongAttempts} to revisit";

    private string AppendCurrentPositionPracticeStatus(string status)
    {
        string? positionId = CurrentPosition?.PositionId;
        if (string.IsNullOrWhiteSpace(positionId))
        {
            return status;
        }

        int attempts = currentSessionAttempts.Count(attempt =>
            string.Equals(attempt.PositionId, positionId, StringComparison.Ordinal));
        if (attempts == 0)
        {
            return status;
        }

        bool needsPractice = currentSessionAttempts.Any(attempt =>
            string.Equals(attempt.PositionId, positionId, StringComparison.Ordinal)
            && attempt.Score == OpeningTrainingScore.Wrong);
        string tryText = attempts == 1 ? "1 try" : $"{attempts} tries";
        string practiceText = needsPractice ? "to revisit" : "on track";
        return $"{status} - {tryText} - {practiceText}";
    }

    public string StageTitle => currentPageIndex switch
    {
        SelectionPageIndex => "Choose Today's Training",
        OverviewPageIndex => "Understand The Idea",
        StudyPageIndex => "Practice From Memory",
        ResultsPageIndex => "Review And Continue",
        _ => "Opening Trainer"
    };

    public string StageDescription => currentPageIndex switch
    {
        SelectionPageIndex => "Start with the recommendation for today, then use Advanced Options only when you want a specific line.",
        OverviewPageIndex => "See the idea, common replies, and the focus before you practice from memory.",
        StudyPageIndex => "Recall the move first, then use hints only when you need a nudge.",
        ResultsPageIndex => "See what is stable, what needs review, and the next best action.",
        _ => string.Empty
    };

    public string StageProgressLabel => $"Stage {currentPageIndex + 1} of {TotalPages} - {StageTitle}";

    public double StageProgressPercent => (currentPageIndex + 1d) / TotalPages * 100d;

    public bool IsSelectionPageVisible => currentPageIndex == SelectionPageIndex;

    public bool IsOverviewPageVisible => currentPageIndex == OverviewPageIndex;

    public bool IsStudyPageVisible => currentPageIndex == StudyPageIndex;

    public bool IsResultsPageVisible => currentPageIndex == ResultsPageIndex;

    public bool HasSelectedOpening => SelectedOpening is not null;

    public string SelectedOpeningName => SelectedOpening?.DisplayName ?? "No opening selected";

    public string SelectedOpeningSideText => SelectedOpening is null
        ? "Choose an item from the list"
        : $"{SelectedOpening.RepertoireSide} repertoire";

    public string OpeningContextStartsFrom => overview is null || overview.MainLine.Count == 0
        ? "Starts from: choose an opening to see the first moves."
        : $"Starts from: {FormatMainLine(overview.MainLine, 4)}";

    public string OpeningContextPlan
    {
        get
        {
            if (overview is null)
            {
                return "Plan: load an opening to see the strategic map before practice.";
            }

            string? idea = overview.WhyTheseMovesMatter
                .Select(moveIdea => moveIdea.ShortExplanation)
                .FirstOrDefault(text => !string.IsNullOrWhiteSpace(text));
            idea ??= overview.MainLine
                .Select(move => move.Idea?.ShortExplanation)
                .FirstOrDefault(text => !string.IsNullOrWhiteSpace(text));

            return string.IsNullOrWhiteSpace(idea)
                ? "Plan: build the center, finish development, and avoid early tactical overreach."
                : $"Plan: {idea}";
        }
    }

    public string CoverageHumanText
    {
        get
        {
            if (overview is null)
            {
                return Localizer.Text(LocalizedStrings.OpeningTrainerPickOpeningCoverage);
            }

            int needsReview = overview.Coverage.WeakBranches;
            if (PlayerOpeningPlan?.Progress.SessionCount > 0)
            {
                return needsReview == 0
                    ? "You have practice history in this repertoire. This opening is in good shape for today."
                    : $"You have practice history in this repertoire. {needsReview} total review position{PluralSuffix(needsReview)} are worth revisiting today.";
            }

            if (overview.Coverage.CoveragePercent <= 0.1)
            {
                return $"You are starting fresh in this opening. {needsReview} total review position{PluralSuffix(needsReview)} are ready for practice today.";
            }

            return needsReview == 0
                ? "This opening is in good shape. Today can stay light and retention-focused."
                : $"{needsReview} total review position{PluralSuffix(needsReview)} are worth revisiting before adding new material.";
        }
    }

    public string CoverageMetricText => overview is null
        ? "No coverage metrics loaded yet."
        : $"Today's set: {overview.Coverage.CoveredBranches}/{overview.Coverage.TotalBookBranches} practiced | {overview.Coverage.WeakBranches} to revisit";

    public string PracticeFocusText => overview is null
        ? "Practice focus appears after an opening loads."
        : SelectedIntensityChoice?.Id switch
        {
            "safe" => "Focus: main line review only. Weak branches are listed below as optional repairs.",
            "challenge" => $"Focus: main line from {FormatMainLine(overview.MainLine, 4)}, plus less familiar opponent replies.",
            _ => overview.Coverage.WeakBranches > 0
                ? $"Focus: {overview.Coverage.WeakBranches} review position{PluralSuffix(overview.Coverage.WeakBranches)} from your {FormatMainLine(overview.MainLine, 4)} line. The full line contains {GetReviewMoveCount()} moves, but today's practice stays focused."
                : $"Focus: main line from {FormatMainLine(overview.MainLine, 4)}, plus the most useful opponent replies."
        };

    public string MainLineText => overview is null || overview.MainLine.Count == 0
        ? "No main line loaded yet."
        : FormatMainLine(overview.MainLine, Math.Min(12, overview.MainLine.Count));

    public string RememberThisText
    {
        get
        {
            if (overview is null || overview.MainLine.Count == 0)
            {
                return "Remember: load an opening to see the plan before practicing the moves.";
            }

            string line = FormatMainLine(overview.MainLine, Math.Min(6, overview.MainLine.Count));
            string idea = overview.WhyTheseMovesMatter
                .Select(moveIdea => moveIdea.ShortExplanation)
                .FirstOrDefault(text => !string.IsNullOrWhiteSpace(text))
                ?? OpeningContextPlan.Replace("Plan: ", string.Empty, StringComparison.OrdinalIgnoreCase);
            return $"Remember: {idea} Start from {line}, then follow the plan instead of memorizing isolated moves.";
        }
    }

    public string WeakPositionsPlaceholder => WeakPositionItems.Count == 0
        ? "No weak positions are saved for this opening yet."
        : string.Empty;

    public bool ShowWeakPositionsPlaceholder => WeakPositionItems.Count == 0;

    public double StudyProgressPercent => guidedSession is null || guidedSession.Positions.Count == 0
        ? 0
        : Math.Round((double)Math.Min(currentStepIndex + 1, guidedSession.Positions.Count) / guidedSession.Positions.Count * 100d, 1);

    public string StudyProgressText => guidedSession is null
        ? "Practice has not started."
        : $"Position {Math.Min(currentStepIndex + 1, guidedSession.Positions.Count)}/{guidedSession.Positions.Count}";

    public string ResultsSummaryText => guidedSession is null
        ? "No session data yet."
        : $"{completedSteps}/{guidedSession.Positions.Count} positions, {correctAnswers} clear, {playableAnswers} accepted alternative(s), {wrongAttempts} to revisit, {hintUseCount} hint(s) used.";

    public string TranspositionSummaryText => transposedAnswers == 0
        ? "No transposition shortcuts appeared in this run."
        : $"Transposed to known positions {transposedAnswers} time(s).";

    private OpeningTrainingPosition? CurrentPosition => guidedSession is not null && currentStepIndex >= 0 && currentStepIndex < guidedSession.Positions.Count
        ? guidedSession.Positions[currentStepIndex]
        : null;

    private void RefreshOpenings()
    {
        IReadOnlyList<OpeningLineCatalogItem> items = workspaceService.ListOpeningLines(FilterText, SelectedSide, 120);
        ReplaceItems(OpeningItems, items);
        RefreshTodayRecommendation();
        if (items.Count > 0)
        {
            OpeningLineCatalogItem? recommendedLine = TodayRecommendation?.OpeningLine;
            SelectedOpening = recommendedLine is not null && items.Contains(recommendedLine)
                ? recommendedLine
                : items[0];
        }
        else
        {
            SelectedOpening = null;
            overview = null;
            ReplaceItems(MainLineItems, []);
            ReplaceItems(BranchItems, []);
            ReplaceItems(PriorityItems, []);
            ReplaceItems(UnderstandingCards, []);
            SelectedPriority = null;
            ReplaceItems(WeakPositionItems, []);
            SummaryText = Localizer.Text(LocalizedStrings.OpeningTrainerNoOpeningsMatch);
            OpponentSummary = Localizer.Text(LocalizedStrings.OpeningTrainerOpponentRepliesUnavailable);
            CoverageText = Localizer.Text(LocalizedStrings.OpeningTrainerNoPracticeHistory);
            CoverageExplanation = Localizer.Text(LocalizedStrings.OpeningTrainerTryAnotherFilterOrSide);
            OnPropertyChanged(nameof(MainLineText));
            OnPropertyChanged(nameof(RememberThisText));
            OnPropertyChanged(nameof(WeakPositionsPlaceholder));
            OnPropertyChanged(nameof(ShowWeakPositionsPlaceholder));
            RaisePriorityStateChanged();
        }
    }

    private void RefreshTodayRecommendation()
    {
        TodayRecommendation = workspaceService.GetRecommendationForToday(PlayerKey, SelectedSide, 120);
        if (TodayRecommendation is not null)
        {
            Dictionary<string, string> properties = BuildRecommendationTelemetryProperties(TodayRecommendation);
            workspaceService.TrackTelemetry(
                OpeningTrainingTelemetryEvents.OpeningDailyLessonShown,
                PlayerKey,
                TodayRecommendation.OpeningLine,
                recommendationId: TodayRecommendation.OpeningLine.LineKey.Value,
                properties: properties);
            workspaceService.TrackTelemetry(
                OpeningTrainingTelemetryEvents.OpeningRecommendationShown,
                PlayerKey,
                TodayRecommendation.OpeningLine,
                recommendationId: TodayRecommendation.OpeningLine.LineKey.Value,
                properties: properties);
        }

        PlayerOpeningPlan = workspaceService.GetPlayerOpeningPlan(PlayerKey, SelectedSide, 120);
        ReplaceItems(SpecialTrainingModes, workspaceService.ListSpecialTrainingModes());
        SelectedSpecialMode ??= SpecialTrainingModes.FirstOrDefault();
        ReplaceItems(TodayPlanItems, PlayerOpeningPlan.Today);
        ReplaceItems(WeeklyPlanItems, PlayerOpeningPlan.ThisWeek);
        ReplaceItems(LongTermGapItems, PlayerOpeningPlan.LongTermGaps);
        OnPropertyChanged(nameof(HasTodayRecommendation));
        OnPropertyChanged(nameof(TodayRecommendationOpening));
        OnPropertyChanged(nameof(TodayRecommendationMeta));
        OnPropertyChanged(nameof(TodayRecommendationReason));
        OnPropertyChanged(nameof(TodayRecommendationAction));
        OnPropertyChanged(nameof(TodayLessonOpening));
        OnPropertyChanged(nameof(TodayLessonSideText));
        OnPropertyChanged(nameof(TodayLessonDurationText));
        OnPropertyChanged(nameof(TodayLessonMoveCountText));
        OnPropertyChanged(nameof(TodayLessonReason));
        OnPropertyChanged(nameof(TodayTrainingReasonLabel));
        OnPropertyChanged(nameof(TodayLessonButtonText));
        OnPropertyChanged(nameof(HasTodayLesson));
        OnPropertyChanged(nameof(PlayerOpeningPlanTitle));
        OnPropertyChanged(nameof(PlayerOpeningPlanSummary));
        OnPropertyChanged(nameof(PlayerOpeningProgressText));
        OnPropertyChanged(nameof(PlayerOpeningProgressInterpretation));
            OnPropertyChanged(nameof(CoverageHumanText));
            OnPropertyChanged(nameof(MainLineText));
            OnPropertyChanged(nameof(RememberThisText));
        OnPropertyChanged(nameof(SelectedSpecialModeDescription));
        OnPropertyChanged(nameof(SelectedSpecialModeButtonText));
        StartRecommendedStudyCommand.RaiseCanExecuteChanged();
        StartRecommendedPracticeNowCommand.RaiseCanExecuteChanged();
        StartSpecialModeCommand.RaiseCanExecuteChanged();
    }

    private void StartRecommendedStudy()
    {
        if (!PrepareTodayRecommendationForStudy())
        {
            return;
        }

        overviewOpenedFromTodayRecommendation = true;
        SetPage(OverviewPageIndex);
    }

    private void StartRecommendedPracticeNow()
    {
        if (!PrepareTodayRecommendationForStudy())
        {
            return;
        }

        overviewOpenedFromTodayRecommendation = false;
        StartGuidedStudy(null, "today_recommendation", TodayRecommendation!.OpeningLine.LineKey.Value);
    }

    private bool PrepareTodayRecommendationForStudy()
    {
        if (TodayRecommendation is null)
        {
            return false;
        }

        SelectedOpening = TodayRecommendation.OpeningLine;
        workspaceService.TrackTelemetry(
            OpeningTrainingTelemetryEvents.OpeningDailyLessonStarted,
            PlayerKey,
            SelectedOpening,
            recommendationId: TodayRecommendation.OpeningLine.LineKey.Value,
            properties: BuildRecommendationTelemetryProperties(TodayRecommendation));
        return true;
    }

    private void StartPriorityStudy()
    {
        if (SelectedPriority is null)
        {
            return;
        }

        workspaceService.TrackTelemetry(
            OpeningTrainingTelemetryEvents.OverviewRecommendationSelected,
            PlayerKey,
            SelectedOpening,
            recommendationId: SelectedPriority.Id,
            properties: BuildBaseTelemetryProperties(new Dictionary<string, string>
            {
                ["action"] = SelectedPriority.Action.ToString(),
                ["reason_code"] = SelectedPriority.ReasonCode.ToString(),
                ["recommendation_type"] = "overview_priority"
            }));
        StartGuidedStudy(null, "overview_priority", SelectedPriority.Id, BuildSessionTarget(SelectedPriority));
    }

    private void StartSpecialMode()
    {
        if (SelectedSpecialMode is null)
        {
            return;
        }

        StartGuidedStudy(SelectedSpecialMode, $"special_mode:{SelectedSpecialMode.Kind}", null);
    }

    private void OpenOverviewPage()
    {
        if (overview is null)
        {
            return;
        }

        SetPage(OverviewPageIndex);
    }

    private void LoadOverview()
    {
        if (SelectedOpening is null || !workspaceService.TryGetOverview(SelectedOpening, PlayerKey, out OpeningTrainerOverview? loadedOverview) || loadedOverview is null)
        {
            overview = null;
            ReplaceItems(MainLineItems, []);
            ReplaceItems(BranchItems, []);
            ReplaceItems(PriorityItems, []);
            ReplaceItems(UnderstandingCards, []);
            SelectedPriority = null;
            ReplaceItems(WeakPositionItems, []);
            SummaryText = Localizer.Text(LocalizedStrings.OpeningTrainerCouldNotLoadOverview);
            OpponentSummary = Localizer.Text(LocalizedStrings.OpeningTrainerOpponentRepliesUnavailable);
            CoverageText = Localizer.Text(LocalizedStrings.OpeningTrainerNoPracticeHistory);
            CoverageExplanation = Localizer.Text(LocalizedStrings.OpeningTrainerOpeningNeedsMoreTheory);
            RaiseSelectionCoachingTextChanged();
            OnPropertyChanged(nameof(WeakPositionsPlaceholder));
            OnPropertyChanged(nameof(ShowWeakPositionsPlaceholder));
            RaisePriorityStateChanged();
            return;
        }

        overview = loadedOverview;
        PreviewFen = SelectedOpening.RootFen;
        PreviewArrows = [];
        OnPropertyChanged(nameof(PreviewArrows));
        SummaryText = $"{SelectedOpening.DisplayName}{Environment.NewLine}Main line moves: {overview.MainLine.Count}";
        OpponentSummary = FormatOpponentSummary(overview.OpponentReplyProfile.Summary);
        CoverageText = $"Your saved progress: {overview.Coverage.CoveredBranches}/{overview.Coverage.TotalBookBranches}";
        CoverageExplanation = overview.Coverage.CoveragePercent <= 0.1
            ? "You do not have saved review progress for this opening yet, so coverage starts at zero."
            : $"Stable branches: {overview.Coverage.StableBranches}. Unseen common branches: {overview.Coverage.UnseenCommonBranches}.";
        ReplaceItems(MainLineItems, overview.MainLine.Select(FormatMoveLabel).ToList());
        ReplaceItems(BranchItems, overview.CommonBranches.Select(branch =>
            $"{branch.OpponentMove} - {FormatBranchFrequencyLabel(branch, overview.CommonBranches)}").ToList());
        ReplaceItems(PriorityItems, overview.Priorities);
        ReplaceItems(UnderstandingCards, workspaceService.BuildUnderstandingCards(overview, SelectedOpening));
        SelectedPriority = PriorityItems.FirstOrDefault();
        ReplaceItems(WeakPositionItems, overview.WeakPositions.Select(position =>
            $"{position.OpeningName} | {position.Instruction}").ToList());
        ResultText = string.Empty;
        RaiseSelectionCoachingTextChanged();
        OnPropertyChanged(nameof(MainLineText));
        OnPropertyChanged(nameof(RememberThisText));
        OnPropertyChanged(nameof(WeakPositionsPlaceholder));
        OnPropertyChanged(nameof(ShowWeakPositionsPlaceholder));
        RaisePriorityStateChanged();
    }

    private void StartGuidedStudy()
    {
        if (overviewOpenedFromTodayRecommendation && TodayRecommendation is not null)
        {
            overviewOpenedFromTodayRecommendation = false;
            StartGuidedStudy(null, "today_recommendation", TodayRecommendation.OpeningLine.LineKey.Value);
            return;
        }

        StartGuidedStudy(null, "manual", null);
    }

    private void StartGuidedStudy(SpecialTrainingModeDefinition? specialMode, string startSource, string? recommendationId)
        => StartGuidedStudy(specialMode, startSource, recommendationId, null);

    private void StartGuidedStudy(
        SpecialTrainingModeDefinition? specialMode,
        string startSource,
        string? recommendationId,
        OpeningTrainingSessionTarget? target)
    {
        if (SelectedOpening is null || overview is null)
        {
            return;
        }

        OpeningTrainingSession session = workspaceService.BuildGuidedStudySession(SelectedOpening, overview, PlayerKey, SelectedStrictness, specialMode, target);
        
        sessionController.StartSession(session, startSource, recommendationId);

        string? telemetryRecommendationId = startSource is "today_recommendation" or "overview_priority"
            ? recommendationId
            : null;

        workspaceService.TrackTelemetry(
            OpeningTrainingTelemetryEvents.OpeningTrainingStarted,
            PlayerKey,
            SelectedOpening,
            session,
            recommendationId: telemetryRecommendationId,
            specialMode: specialMode?.Kind,
            properties: BuildBaseTelemetryProperties(new Dictionary<string, string>
            {
                ["start_source"] = startSource,
                ["position_count"] = session.Positions.Count.ToString(CultureInfo.InvariantCulture),
                ["target_fallback"] = (target is not null && !session.Positions.Any(position => IsTargetedPosition(position, target))).ToString().ToLowerInvariant(),
                ["recommendation_type"] = BuildRecommendationType(startSource, specialMode),
                ["reason_code"] = TodayRecommendation is not null && string.Equals(recommendationId, TodayRecommendation.OpeningLine.LineKey.Value, StringComparison.Ordinal)
                    ? TodayRecommendation.ReasonCode.ToString()
                    : "unknown"
            }));

        if (specialMode is not null)
        {
            workspaceService.TrackTelemetry(
                OpeningTrainingTelemetryEvents.SpecialModeStarted,
                PlayerKey,
                SelectedOpening,
                session,
                specialMode: specialMode.Kind,
                properties: BuildBaseTelemetryProperties(new Dictionary<string, string>
                {
                    ["time_limit_minutes"] = specialMode.TimeLimitMinutes.ToString(CultureInfo.InvariantCulture),
                    ["max_positions"] = specialMode.MaxPositions.ToString(CultureInfo.InvariantCulture)
                }));
        }

        MoveInput = string.Empty;
        ResultText = string.Empty;
        ResetResults();
        ClearStudySelection();
        sessionController.LoadCurrentStep();
        SetPage(StudyPageIndex);
    }

    private static OpeningTrainingSessionTarget? BuildSessionTarget(TrainingPriorityItem? priority)
    {
        return priority is null
            ? null
            : new OpeningTrainingSessionTarget(
                priority.Id,
                priority.Action,
                priority.LineKey,
                priority.BranchKey,
                priority.PositionKey,
                priority.MoveSan,
                priority.MoveUci);
    }

    private static bool IsTargetedPosition(OpeningTrainingPosition position, OpeningTrainingSessionTarget target)
    {
        return target.Action switch
        {
            TrainingPriorityAction.RepairThisPosition => target.PositionKey.HasValue
                && position.OpeningPositionKey.Equals(target.PositionKey.Value),
            TrainingPriorityAction.TrainThisBranch or TrainingPriorityAction.ReviewOpponentReply =>
                (target.BranchKey.HasValue
                    && position.OpeningBranchKey.HasValue
                    && position.OpeningBranchKey.Value.Equals(target.BranchKey.Value))
                || (!string.IsNullOrWhiteSpace(target.OpponentMoveUci)
                    && position.CandidateMoves.Any(option => string.Equals(option.Uci, target.OpponentMoveUci, StringComparison.OrdinalIgnoreCase)))
                || (!string.IsNullOrWhiteSpace(target.OpponentMove)
                    && position.CandidateMoves.Any(option => string.Equals(option.DisplayText, target.OpponentMove, StringComparison.OrdinalIgnoreCase))),
            _ => false
        };
    }

    private void RestartStudy()
    {
        StartGuidedStudy();
    }

    private void EvaluateMove()
    {
        OpeningTrainingPosition? position = CurrentPosition;
        if (position is null)
        {
            return;
        }

        string submittedAnswer = position.AnswerKind == OpeningTrainingAnswerKind.Move
            ? MoveInput
            : SelectedAnswerOption?.Id ?? string.Empty;

        var evalResult = sessionController.EvaluateMove(submittedAnswer);
        if (!evalResult.HasValue)
        {
            return;
        }

        OpeningTrainingMoveOption? mainMove = position.CandidateMoves.FirstOrDefault(option => option.IsPreferred)
            ?? (position.CandidateMoves.Count > 0 ? position.CandidateMoves[0] : null);

        ResultText = evalResult.Attempt.Score switch
        {
            OpeningTrainingScore.Correct => "Good. This keeps your repertoire plan on track.",
            OpeningTrainingScore.Wrong => BuildWrongMoveFeedback(position, mainMove),
            _ when IsSubmittedMainRepertoireMove(evalResult.Attempt, mainMove) => "Good. This is the prepared repertoire move.",
            _ => mainMove is not null
                ? $"Accepted, but the main repertoire move is {mainMove.DisplayText}."
                : "Accepted. This is a useful theory alternative."
        };

        TriggerStudyFeedback(evalResult.Attempt);
        
        CurrentWhy = evalResult.Attempt.RecoverySuggestion
            ?? evalResult.Attempt.WhyThisMove?.ShortExplanation
            ?? position.BetterMoveReason
            ?? string.Empty;

        if (!string.IsNullOrWhiteSpace(evalResult.Attempt.RecoverySuggestion))
        {
            CurrentHintLevel = evalResult.Attempt.NextHintLevel.HasValue
                ? $"Next hint: {evalResult.Attempt.NextHintLevel.Value}"
                : "Recovery";
            CurrentHintText = evalResult.Attempt.RecoverySuggestion;
        }

        AddResultLine(evalResult.Attempt);

        if (!evalResult.Accepted)
        {
            ClearStudySelection();
            RaiseResultsStateChanged();
            RaiseStudyNavigationStateChanged();
            return;
        }

        MoveInput = string.Empty;
        SelectedAnswerOption = null;
        ClearStudySelection();
        
        sessionController.MoveNext();
        
        RaiseResultsStateChanged();
    }

    private bool CanEvaluateCurrentAnswer()
    {
        OpeningTrainingPosition? position = CurrentPosition;
        if (position is null)
        {
            return false;
        }

        return position.AnswerKind == OpeningTrainingAnswerKind.Move
            ? !string.IsNullOrWhiteSpace(MoveInput)
            : SelectedAnswerOption is not null;
    }

    private static bool IsSubmittedMainRepertoireMove(
        OpeningTrainingAttemptResult result,
        OpeningTrainingMoveOption? mainMove)
    {
        if (mainMove is null)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(mainMove.Uci)
            && !string.IsNullOrWhiteSpace(result.ResolvedUci)
            && string.Equals(mainMove.Uci, result.ResolvedUci, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(result.ResolvedSan)
            && string.Equals(
                SanNotation.NormalizeSan(mainMove.DisplayText),
                SanNotation.NormalizeSan(result.ResolvedSan),
                StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return !string.IsNullOrWhiteSpace(result.SubmittedMoveText)
            && string.Equals(
                SanNotation.NormalizeSan(mainMove.DisplayText),
                SanNotation.NormalizeSan(result.SubmittedMoveText),
                StringComparison.OrdinalIgnoreCase);
    }

    public void HandleStudyBoardSquarePressed(string squareName)
    {
        if (!IsStudyPageVisible || string.IsNullOrWhiteSpace(squareName))
        {
            return;
        }

        OpeningTrainingPosition? position = CurrentPosition;
        if (position is null)
        {
            return;
        }

        if (position.AnswerKind != OpeningTrainingAnswerKind.Move)
        {
            return;
        }

        ChessGame game = new();
        if (!game.TryLoadFen(position.Fen, out _))
        {
            return;
        }

        if (studySelectedSquare is null)
        {
            TrySelectStudySourceSquare(game, squareName);
            return;
        }

        if (string.Equals(studySelectedSquare, squareName, StringComparison.OrdinalIgnoreCase))
        {
            ClearStudySelection();
            return;
        }

        List<LegalMoveInfo> matchingMoves = game.GetLegalMoves()
            .Where(move => string.Equals(move.FromSquare, studySelectedSquare, StringComparison.OrdinalIgnoreCase)
                && string.Equals(move.ToSquare, squareName, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (matchingMoves.Count == 0)
        {
            ClearStudySelection();
            TrySelectStudySourceSquare(game, squareName);
            return;
        }

        string? uci = SelectStudyMoveToApply(matchingMoves, position);
        if (string.IsNullOrWhiteSpace(uci))
        {
            ResultText = "This move could not be resolved from the board selection.";
            ClearStudySelection();
            return;
        }

        StudyPreviewTargetSquare = squareName;
        MoveInput = uci;
        EvaluateMove();
    }

    private void MoveNext()
    {
        sessionController.MoveNext();
    }

    private void TriggerStudyFeedback(OpeningTrainingAttemptResult result)
    {
        _ = studyFeedbackAnimator.AnimateAsync(result, ApplyStudyFeedbackFrame);
    }

    private void ApplyStudyFeedbackFrame(OpeningStudyFeedbackFrame frame)
    {
        StudyFeedbackText = frame.Text;
        StudyFeedbackBrush = frame.Brush;
        StudyFeedbackBorderBrush = frame.BorderBrush;
        StudyFeedbackOpacity = frame.Opacity;
    }

    private void MovePrevious()
    {
        sessionController.MovePrevious();
    }

    private void ResetResults()
    {
        resultsViewModel.Reset();
        ResetCurrentHint();
        RaiseNextActionStateChanged();
        RaiseLearningPlanStateChanged();
        RaiseResultsStateChanged();
    }

    private void AddResultLine(OpeningTrainingAttemptResult result)
    {
        resultsViewModel.AddResultLine(result);
        OnPropertyChanged(nameof(HasAdvancedResultDetails));
    }

    private void SetPage(int pageIndex)
    {
        TrackAbandonmentIfLeavingStudy(pageIndex);
        if (!SetProperty(ref currentPageIndex, pageIndex, nameof(currentPageIndex)))
        {
            return;
        }

        OnPropertyChanged(nameof(StageTitle));
        OnPropertyChanged(nameof(StageDescription));
        OnPropertyChanged(nameof(StageProgressLabel));
        OnPropertyChanged(nameof(StageProgressPercent));
        OnPropertyChanged(nameof(IsSelectionPageVisible));
        OnPropertyChanged(nameof(IsOverviewPageVisible));
        OnPropertyChanged(nameof(IsStudyPageVisible));
        OnPropertyChanged(nameof(IsResultsPageVisible));
    }

    private void RaiseNavigationStateChanged()
    {
        GoToOverviewCommand.RaiseCanExecuteChanged();
        StartRecommendedStudyCommand.RaiseCanExecuteChanged();
        StartRecommendedPracticeNowCommand.RaiseCanExecuteChanged();
        StartGuidedStudyCommand.RaiseCanExecuteChanged();
        StartPriorityStudyCommand.RaiseCanExecuteChanged();
        StartSpecialModeCommand.RaiseCanExecuteChanged();
        RestartStudyCommand.RaiseCanExecuteChanged();
        OnPropertyChanged(nameof(HasSelectedOpening));
        OnPropertyChanged(nameof(SelectedOpeningName));
        OnPropertyChanged(nameof(SelectedOpeningSideText));
        RaiseSelectionCoachingTextChanged();
    }

    private void RaiseStudyNavigationStateChanged()
    {
        ShowHintCommand.RaiseCanExecuteChanged();
        DontKnowCommand.RaiseCanExecuteChanged();
        RevealStudyReferenceCommand.RaiseCanExecuteChanged();
        EvaluateMoveCommand.RaiseCanExecuteChanged();
        NextStepCommand.RaiseCanExecuteChanged();
        PreviousStepCommand.RaiseCanExecuteChanged();
        OnPropertyChanged(nameof(IsStudyReferenceVisible));
        OnPropertyChanged(nameof(CanRevealStudyReference));
        OnPropertyChanged(nameof(StudyReferenceButtonText));
        OnPropertyChanged(nameof(IsStudyReferenceHidden));
        OnPropertyChanged(nameof(StudyReferencePromptText));
        OnPropertyChanged(nameof(DontKnowButtonText));
        OnPropertyChanged(nameof(CanUseDontKnow));
        OnPropertyChanged(nameof(StudyProgressPercent));
        OnPropertyChanged(nameof(StudyProgressText));
        OnPropertyChanged(nameof(StudyTaskTitle));
        OnPropertyChanged(nameof(StudyTaskSubtitle));
        OnPropertyChanged(nameof(StudyBoardStatusText));
        OnPropertyChanged(nameof(StudySideToMoveText));
        OnPropertyChanged(nameof(StudyMovePromptText));
        OnPropertyChanged(nameof(StudyFeedbackSummaryText));
        OnPropertyChanged(nameof(StudySelectedSquare));
        OnPropertyChanged(nameof(StudyAvailableMoveSquares));
        OnPropertyChanged(nameof(StudyBoardHint));
        OnPropertyChanged(nameof(StudyInputModeText));
        OnPropertyChanged(nameof(CurrentPositionGoalText));
        OnPropertyChanged(nameof(CurrentAttemptHistoryText));
        OnPropertyChanged(nameof(CurrentMoveTrainingPurposeText));
        OnPropertyChanged(nameof(SessionCorrectCountText));
        OnPropertyChanged(nameof(SessionAcceptedAlternativesText));
        OnPropertyChanged(nameof(SessionNeedsReviewText));
        OnPropertyChanged(nameof(CurrentHintText));
        OnPropertyChanged(nameof(CurrentHintLevel));
        OnPropertyChanged(nameof(HasAnswerOptions));
    }

    private void RaiseResultsStateChanged()
    {
        OnPropertyChanged(nameof(ResultHeadline));
        OnPropertyChanged(nameof(ResultRecommendation));
        OnPropertyChanged(nameof(ResultsSummaryText));
        OnPropertyChanged(nameof(TranspositionSummaryText));
        OnPropertyChanged(nameof(ResultsBiggestWeaknessText));
        OnPropertyChanged(nameof(ResultCelebrationTitle));
        OnPropertyChanged(nameof(ResultCelebrationText));
        OnPropertyChanged(nameof(ResultOutcomeBadge));
        OnPropertyChanged(nameof(ResultNextStepSummary));
        OnPropertyChanged(nameof(ResultsCompletedMetricText));
        OnPropertyChanged(nameof(ResultsClearMetricText));
        OnPropertyChanged(nameof(ResultsAlternativesMetricText));
        OnPropertyChanged(nameof(ResultsHintsMetricText));
        OnPropertyChanged(nameof(ResultsRevisitMetricText));
        OnPropertyChanged(nameof(ResultsNextBestActionText));
        OnPropertyChanged(nameof(ResultsNextActionReasonText));
        OnPropertyChanged(nameof(HasAdvancedResultDetails));
        OnPropertyChanged(nameof(OutcomeSummary));
    }


    private void ExecutePrimaryNextAction()
    {
        if (PrimaryNextAction is null)
        {
            return;
        }

        ExecuteNextAction(PrimaryNextAction.Action);
    }

    private void ExecuteSecondaryNextAction(TrainingNextActionCardViewModel? nextAction)
    {
        if (nextAction is null)
        {
            return;
        }

        ExecuteNextAction(nextAction.Action);
    }

    private void ExecuteSelectedSecondaryNextAction()
    {
        if (SelectedSecondaryNextAction is null)
        {
            return;
        }

        ExecuteNextAction(SelectedSecondaryNextAction.Action);
    }

    private void ExecuteSelectedNextAction()
    {
        if (SelectedNextAction is null)
        {
            return;
        }

        ExecuteNextAction(SelectedNextAction);
    }

    private void ExecuteNextAction(TrainingNextAction action)
    {
        string? scheduledActionId;
        sessionController.CompleteNextAction(
            action.Id,
            sessionController.TryGetScheduledActionId(action.Id, out scheduledActionId) ? scheduledActionId : null,
            action.Kind);

        workspaceService.TrackTelemetry(
            OpeningTrainingTelemetryEvents.ResultsNextActionClicked,
            PlayerKey,
            SelectedOpening,
            guidedSession,
            recommendationId: sessionController.CurrentRecommendationId,
            properties: BuildBaseTelemetryProperties(new Dictionary<string, string>
            {
                ["next_action_id"] = action.Id,
                ["next_action_kind"] = action.Kind.ToString(),
                ["delay_minutes"] = action.DelayMinutes.ToString(CultureInfo.InvariantCulture)
            }));

        switch (action.Kind)
        {
            case TrainingNextActionKind.RepeatNow:
            case TrainingNextActionKind.PracticeMainLineOnly:
            case TrainingNextActionKind.ReviewWithHintsAllowed:
                RestartStudy();
                break;
            case TrainingNextActionKind.RepeatAfterBreak:
                ResultText = action.DelayMinutes > 0
                    ? $"Scheduled. Review this line in about {action.DelayMinutes} min. Starting another recommended opening now."
                    : "Scheduled for a later review.";
                StartAnotherRecommendedOpening();
                break;
            case TrainingNextActionKind.RepairWeakBranches:
                SetPage(OverviewPageIndex);
                break;
            case TrainingNextActionKind.BrowseAnotherOpening:
                if (string.Equals(action.Id, "train-another-opening", StringComparison.OrdinalIgnoreCase))
                {
                    StartAnotherRecommendedOpening();
                }
                else
                {
                    SetPage(SelectionPageIndex);
                }
                break;
            case TrainingNextActionKind.ReturnTomorrow:
            case TrainingNextActionKind.StopForNow:
                SetPage(SelectionPageIndex);
                break;
        }
    }

    private void StartAnotherRecommendedOpening()
    {
        OpeningLineCatalogItem? currentLine = SelectedOpening;
        RefreshTodayRecommendation();

        OpeningLineCatalogItem? nextLine = FindAnotherRecommendedOpening(currentLine);
        if (nextLine is null)
        {
            SetPage(SelectionPageIndex);
            return;
        }

        SelectedOpening = nextLine;
        LoadOverview();
        StartGuidedStudy(null, "next_recommended_opening", nextLine.LineKey.Value);
    }

    private OpeningLineCatalogItem? FindAnotherRecommendedOpening(OpeningLineCatalogItem? currentLine)
    {
        IEnumerable<string> recommendedEco = (PlayerOpeningPlan?.Today ?? [])
            .Concat(PlayerOpeningPlan?.ThisWeek ?? [])
            .Concat(PlayerOpeningPlan?.LongTermGaps ?? [])
            .Select(item => item.Eco)
            .Where(eco => !string.IsNullOrWhiteSpace(eco))
            .Cast<string>();

        foreach (string eco in recommendedEco)
        {
            OpeningLineCatalogItem? match = OpeningItems.FirstOrDefault(item =>
                !IsSameOpeningLine(item, currentLine)
                && !string.Equals(item.Eco, currentLine?.Eco, StringComparison.OrdinalIgnoreCase)
                && string.Equals(item.Eco, eco, StringComparison.OrdinalIgnoreCase));
            if (match is not null)
            {
                return match;
            }
        }

        return OpeningItems.FirstOrDefault(item => !IsSameOpeningLine(item, currentLine));
    }

    private static bool IsSameOpeningLine(OpeningLineCatalogItem? left, OpeningLineCatalogItem? right)
        => left is not null && right is not null && left.LineKey.Equals(right.LineKey);

    private void TrackAbandonmentIfLeavingStudy(int nextPageIndex)
    {
        sessionController.TrackAbandonmentIfLeavingStudy(currentPageIndex, nextPageIndex, StudyPageIndex, ResultsPageIndex);
    }

    private void RaiseNextActionStateChanged()
    {
        resultsViewModel.RaiseNextActionStateChanged();
        OnPropertyChanged(nameof(HasNextActions));
        OnPropertyChanged(nameof(NextActionsPlaceholder));
        OnPropertyChanged(nameof(SelectedNextActionButtonText));
        OnPropertyChanged(nameof(PrimaryNextAction));
        OnPropertyChanged(nameof(HasPrimaryNextAction));
        OnPropertyChanged(nameof(HasSecondaryNextActions));
        OnPropertyChanged(nameof(ResultNextStepSummary));
        OnPropertyChanged(nameof(ResultsNextBestActionText));
        OnPropertyChanged(nameof(ResultsNextActionReasonText));
        ExecuteNextActionCommand.RaiseCanExecuteChanged();
        ExecutePrimaryNextActionCommand.RaiseCanExecuteChanged();
        ExecuteSecondaryNextActionCommand.RaiseCanExecuteChanged();
        ExecuteSelectedSecondaryNextActionCommand.RaiseCanExecuteChanged();
    }

    private void RaiseLearningPlanStateChanged()
    {
        resultsViewModel.RaiseLearningPlanStateChanged();
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

    private void UseDontKnow()
    {
        var dontKnowResult = sessionController.UseDontKnow();
        if (!dontKnowResult.HasValue)
        {
            return;
        }

        ResultText = "Marked to revisit: use the hint, then play the prepared move on the board.";
        AddResultLine(dontKnowResult.Attempt);

        if (dontKnowResult.Hint.HasValue)
        {
            if (dontKnowResult.Hint.Hint is not null)
            {
                CurrentHintLevel = $"{dontKnowResult.Hint.Hint.Level}: {dontKnowResult.Hint.Hint.Title}";
                CurrentHintText = dontKnowResult.Hint.Hint.Text;
                if (dontKnowResult.Hint.Hint.Level >= TrainingCoachHintLevel.Structure)
                {
                    PreviewArrows = BuildArrows(CurrentPosition!);
                    OnPropertyChanged(nameof(PreviewArrows));
                }

                workspaceService.TrackTelemetry(
                    OpeningTrainingTelemetryEvents.GuidedHintUsed,
                    PlayerKey,
                    SelectedOpening,
                    guidedSession,
                    properties: BuildBaseTelemetryProperties(new Dictionary<string, string>
                    {
                        ["hint_level"] = dontKnowResult.Hint.Hint.Level.ToString(),
                        ["position_id"] = dontKnowResult.PositionId,
                        ["source"] = "dont_know",
                        ["hint_count"] = dontKnowResult.HintUseCount.ToString(CultureInfo.InvariantCulture)
                    }));
            }
            else
            {
                CurrentHintLevel = "No hint available";
                CurrentHintText = "This position does not have a coaching hint yet.";
            }
        }

        UnlockStudyReference();
        ClearStudySelection();
        RaiseResultsStateChanged();
        RaiseStudyNavigationStateChanged();

        workspaceService.TrackTelemetry(
            OpeningTrainingTelemetryEvents.GuidedDontKnowUsed,
            PlayerKey,
            SelectedOpening,
            guidedSession,
            recommendationId: sessionController.CurrentRecommendationId,
            properties: BuildBaseTelemetryProperties(new Dictionary<string, string>
            {
                ["position_id"] = dontKnowResult.PositionId,
                ["step_index"] = dontKnowResult.StepIndex.ToString(CultureInfo.InvariantCulture),
                ["hint_count"] = dontKnowResult.HintUseCount.ToString(CultureInfo.InvariantCulture),
                ["not_known_count"] = dontKnowResult.DontKnowCount.ToString(CultureInfo.InvariantCulture)
            }));
    }

    private void RevealStudyReference()
    {
        if (!CanRevealStudyReference)
        {
            return;
        }

        IsStudyReferenceVisible = true;
        sessionController.MarkReferenceRevealed();

        workspaceService.TrackTelemetry(
            OpeningTrainingTelemetryEvents.GuidedReferenceRevealed,
            PlayerKey,
            SelectedOpening,
            guidedSession,
            recommendationId: sessionController.CurrentRecommendationId,
            properties: BuildBaseTelemetryProperties(new Dictionary<string, string>
            {
                ["position_id"] = CurrentPosition?.PositionId ?? string.Empty,
                ["step_index"] = currentStepIndex.ToString(CultureInfo.InvariantCulture),
                ["reference_revealed_before_attempt"] = currentSessionAttempts.All(attempt => attempt.PositionId != CurrentPosition?.PositionId).ToString().ToLowerInvariant(),
                ["hint_count"] = hintUseCount.ToString(CultureInfo.InvariantCulture),
                ["not_known_count"] = sessionController.CountDontKnowAttempts().ToString(CultureInfo.InvariantCulture)
            }));
        RaiseStudyNavigationStateChanged();
    }

    private void UnlockStudyReference()
    {
        CanRevealStudyReference = true;
    }

    private void ResetStudyReference()
    {
        IsStudyReferenceVisible = false;
        CanRevealStudyReference = false;
    }

    private void ShowNextHint()
        => ShowNextHint(trackAsDontKnow: false);

    private void ShowNextHint(bool trackAsDontKnow)
    {
        var hintResult = sessionController.ShowNextHint(trackAsDontKnow);
        if (!hintResult.HasValue)
        {
            return;
        }

        if (hintResult.Hint is null)
        {
            CurrentHintLevel = "No hint available";
            CurrentHintText = "This position does not have a coaching hint yet.";
            RaiseStudyNavigationStateChanged();
            return;
        }

        CurrentHintLevel = $"{hintResult.Hint.Level}: {hintResult.Hint.Title}";
        CurrentHintText = hintResult.Hint.Text;
        if (hintResult.Hint.Level >= TrainingCoachHintLevel.Structure)
        {
            PreviewArrows = BuildArrows(CurrentPosition!);
            OnPropertyChanged(nameof(PreviewArrows));
        }

        workspaceService.TrackTelemetry(
            OpeningTrainingTelemetryEvents.GuidedHintUsed,
            PlayerKey,
            SelectedOpening,
            guidedSession,
            properties: BuildBaseTelemetryProperties(new Dictionary<string, string>
            {
                ["hint_level"] = hintResult.Hint.Level.ToString(),
                ["position_id"] = hintResult.PositionId ?? string.Empty,
                ["source"] = trackAsDontKnow ? "dont_know" : "hint_button",
                ["hint_count"] = hintResult.HintUseCount.ToString(CultureInfo.InvariantCulture)
            }));
        RaiseResultsStateChanged();
        RaiseStudyNavigationStateChanged();
    }

    private Dictionary<string, string> BuildRecommendationTelemetryProperties(TrainingRecommendationCard recommendation)
        => telemetryAdapter.BuildRecommendationProperties(
            recommendation,
            SelectedProfileChoice,
            SelectedSide,
            AdvancedPlayerKey);

    private Dictionary<string, string> BuildBaseTelemetryProperties(Dictionary<string, string>? properties = null)
    {
        return telemetryAdapter.BuildBaseProperties(
            SelectedProfileChoice,
            SelectedSide,
            AdvancedPlayerKey,
            properties);
    }

    private static string BuildRecommendationType(string startSource, SpecialTrainingModeDefinition? specialMode)
    {
        if (specialMode is not null)
        {
            return $"special_mode:{specialMode.Kind}";
        }

        return startSource switch
        {
            "today_recommendation" => "daily_lesson",
            "overview_priority" => "overview_priority",
            "manual" => "manual",
            _ => startSource
        };
    }

    private void ResetCurrentHint()
    {
        CurrentHintLevel = Localizer.Text(LocalizedStrings.OpeningTrainerNoHintUsed);
        CurrentHintText = Localizer.Text(LocalizedStrings.OpeningTrainerHintsPlaceholder);
    }

    private void RaisePriorityStateChanged()
    {
        OnPropertyChanged(nameof(HasPriorityItems));
        OnPropertyChanged(nameof(PriorityItemsPlaceholder));
        OnPropertyChanged(nameof(SelectedPriorityActionText));
        StartPriorityStudyCommand.RaiseCanExecuteChanged();
    }

    private void RaiseTodayRecommendationStateChanged()
    {
        OnPropertyChanged(nameof(HasTodayRecommendation));
        OnPropertyChanged(nameof(TodayRecommendationOpening));
        OnPropertyChanged(nameof(TodayRecommendationMeta));
        OnPropertyChanged(nameof(TodayRecommendationReason));
        OnPropertyChanged(nameof(TodayRecommendationAction));
        OnPropertyChanged(nameof(TodayLessonOpening));
        OnPropertyChanged(nameof(TodayLessonSideText));
        OnPropertyChanged(nameof(TodayLessonDurationText));
        OnPropertyChanged(nameof(TodayLessonMoveCountText));
        OnPropertyChanged(nameof(TodayLessonReason));
        OnPropertyChanged(nameof(TodayDecisionSummary));
        OnPropertyChanged(nameof(TodayStartSequenceText));
        OnPropertyChanged(nameof(TodayLessonReasonDetail));
        OnPropertyChanged(nameof(TodayTrainingReasonLabel));
        OnPropertyChanged(nameof(TodayLessonButtonText));
        OnPropertyChanged(nameof(HasTodayLesson));
        StartRecommendedStudyCommand.RaiseCanExecuteChanged();
        StartRecommendedPracticeNowCommand.RaiseCanExecuteChanged();
    }

    private void RaiseSelectionCoachingTextChanged()
    {
        OnPropertyChanged(nameof(OpeningContextStartsFrom));
        OnPropertyChanged(nameof(OpeningContextPlan));
        OnPropertyChanged(nameof(CoverageHumanText));
        OnPropertyChanged(nameof(CoverageMetricText));
        OnPropertyChanged(nameof(PracticeFocusText));
        OnPropertyChanged(nameof(TodayLessonReasonDetail));
        OnPropertyChanged(nameof(TodayDecisionSummary));
        OnPropertyChanged(nameof(TodayStartSequenceText));
    }

    private int GetRecommendedPositionCount()
    {
        if (overview is not null && TodayRecommendation is not null && Equals(SelectedOpening, TodayRecommendation.OpeningLine))
        {
            return Math.Max(1, overview.Coverage.WeakBranches > 0 ? overview.Coverage.WeakBranches : overview.MainLine.Count);
        }

        return TodayRecommendation is null
            ? 0
            : Math.Max(1, TodayRecommendation.OpeningLine.BookBranchCount);
    }

    private int GetReviewMoveCount()
    {
        string? targetEco = TodayRecommendation?.OpeningLine.Eco ?? SelectedOpening?.Eco;
        if (!string.IsNullOrWhiteSpace(targetEco))
        {
            PlayerOpeningPlanItem? matchingWeeklyItem = WeeklyPlanItems.FirstOrDefault(item =>
                string.Equals(item.Eco, targetEco, StringComparison.OrdinalIgnoreCase));
            int parsedCount = ExtractLeadingInt(matchingWeeklyItem?.Evidence);
            if (parsedCount > 0)
            {
                return parsedCount;
            }
        }

        if (overview is not null && overview.MainLine.Count > 0)
        {
            return overview.MainLine.Count;
        }

        return Math.Max(1, GetRecommendedPositionCount());
    }

    private static int ExtractLeadingInt(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return 0;
        }

        int value = 0;
        foreach (char character in text)
        {
            if (!char.IsDigit(character))
            {
                break;
            }

            value = value * 10 + character - '0';
        }

        return value;
    }

    private string GetEstimatedDurationText()
    {
        if (TodayRecommendation is null)
        {
            return "0 min";
        }

        if (SelectedIntensityChoice?.Id == "balanced" && (overview?.Coverage.WeakBranches ?? TodayRecommendation.OpeningLine.BookBranchCount) > 0)
        {
            int lowerBound = Math.Max(TodayRecommendation.EstimatedDurationMinutes, 8);
            int upperBound = Math.Max(lowerBound + 2, 10);
            return $"{lowerBound}-{upperBound} min";
        }

        return $"{TodayRecommendation.EstimatedDurationMinutes} min";
    }

    private static string FormatRepertoireSide(RepertoireSide side)
        => side switch
        {
            RepertoireSide.White => "White",
            RepertoireSide.Black => "Black",
            _ => "Both sides"
        };

    private static IReadOnlyList<BoardArrowViewModel> BuildArrows(OpeningTrainingPosition position)
    {
        OpeningTrainingMoveOption? expected = position.CandidateMoves.FirstOrDefault(option => option.IsPreferred)
            ?? (position.CandidateMoves.Count > 0 ? position.CandidateMoves[0] : null);
        if (expected is null || string.IsNullOrWhiteSpace(expected.Uci))
        {
            return [];
        }

        ChessGame game = new();
        if (!game.TryLoadFen(position.Fen, out _)
            || !game.TryApplyUci(expected.Uci, out AppliedMoveInfo? appliedMove, out _)
            || appliedMove is null)
        {
            return [];
        }

        return [new BoardArrowViewModel(appliedMove.FromSquare, appliedMove.ToSquare, Color.Parse("#2146FF"))];
    }

    private static void ReplaceItems<T>(ObservableCollection<T> collection, IReadOnlyList<T> items)
    {
        collection.Clear();
        foreach (T item in items)
        {
            collection.Add(item);
        }
    }

    private void TrySelectStudySourceSquare(ChessGame game, string squareName)
    {
        if (!TryGetPieceAt(game.GetFen(), squareName, out string? piece) || string.IsNullOrEmpty(piece))
        {
            return;
        }

        bool isWhitePiece = char.IsUpper(piece[0]);
        if (isWhitePiece != game.WhiteToMove)
        {
            return;
        }

        List<LegalMoveInfo> movesForPiece = game.GetLegalMoves()
            .Where(move => string.Equals(move.FromSquare, squareName, StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (movesForPiece.Count == 0)
        {
            return;
        }

        studySelectedSquare = squareName;
        studyAvailableTargets.Clear();
        foreach (LegalMoveInfo move in movesForPiece)
        {
            studyAvailableTargets.Add(move.ToSquare);
        }

        StudyPreviewTargetSquare = null;
        RaiseStudyNavigationStateChanged();
    }

    private void ClearStudySelection()
    {
        studySelectedSquare = null;
        studyAvailableTargets.Clear();
        StudyPreviewTargetSquare = null;
        OnPropertyChanged(nameof(StudySelectedSquare));
        OnPropertyChanged(nameof(StudyAvailableMoveSquares));
        OnPropertyChanged(nameof(StudyBoardHint));
    }

    private static string? SelectStudyMoveToApply(
        List<LegalMoveInfo> matchingMoves,
        OpeningTrainingPosition position)
    {
        if (matchingMoves.Count == 0)
        {
            return null;
        }

        if (matchingMoves.Count == 1)
        {
            return matchingMoves[0].Uci;
        }

        string? matchingBookMove = matchingMoves
            .Select(move => move.Uci)
            .FirstOrDefault(uci => position.CandidateMoves.Any(option =>
                !string.IsNullOrWhiteSpace(option.Uci)
                && string.Equals(option.Uci, uci, StringComparison.OrdinalIgnoreCase)));
        if (!string.IsNullOrWhiteSpace(matchingBookMove))
        {
            return matchingBookMove;
        }

        string queenPromotion = position.SideToMove == PlayerSide.White ? "Q" : "q";
        LegalMoveInfo? queenMove = matchingMoves
            .FirstOrDefault(move => string.Equals(move.PromotionPiece, queenPromotion, StringComparison.Ordinal));
        return queenMove?.Uci ?? matchingMoves[0].Uci;
    }

    private static bool TryGetPieceAt(string fen, string squareName, out string? piece)
    {
        piece = null;
        if (!FenPosition.TryParse(fen, out FenPosition? position, out _)
            || position is null
            || !TryParseSquare(squareName, out (int X, int Y) square))
        {
            return false;
        }

        piece = position.Board[square.X, square.Y];
        return !string.IsNullOrWhiteSpace(piece);
    }

    private static bool TryParseSquare(string squareName, out (int X, int Y) square)
    {
        square = default;
        if (string.IsNullOrWhiteSpace(squareName) || squareName.Length != 2)
        {
            return false;
        }

        char file = char.ToLowerInvariant(squareName[0]);
        char rank = squareName[1];
        if (file < 'a' || file > 'h' || rank < '1' || rank > '8')
        {
            return false;
        }

        square = (file - 'a', 8 - (rank - '0'));
        return true;
    }

    /// <summary>
    /// Forwarding adapter from <see cref="OpeningTrainerSessionController.ISessionFlowCallbacks"/> back to the
    /// ViewModel.  Currently a no-op stub – future passes will migrate callback implementations here and
    /// remove the corresponding duplicated ViewModel methods.
    /// </summary>
    private sealed class SessionCallbacks : OpeningTrainerSessionController.ISessionFlowCallbacks
    {
        private readonly OpeningTrainerWindowViewModel vm;

        internal SessionCallbacks(OpeningTrainerWindowViewModel vm) => this.vm = vm;

        public string PlayerKey => vm.PlayerKey;

        public void OnStepLoaded(
            OpeningTrainingPosition? position,
            int stepIndex,
            OpeningTrainingSession? session)
        {
            if (position is null || session is null)
            {
                vm.PreviewFen = string.Empty;
                vm.CurrentPrompt = Localizer.Text(LocalizedStrings.OpeningTrainerPracticeIdle);
                vm.CurrentWhy = Localizer.Text(LocalizedStrings.OpeningTrainerWhyPlaceholder);
                vm.PreviewArrows = [];
                ReplaceItems(vm.AnswerOptionItems, []);
                vm.SelectedAnswerOption = null;
                vm.ResetCurrentHint();
                vm.ResetStudyReference();
                vm.ClearStudySelection();
                vm.OnPropertyChanged(nameof(vm.PreviewArrows));
                vm.OnPropertyChanged(nameof(vm.HasAnswerOptions));
                vm.RaiseStudyNavigationStateChanged();
                return;
            }

            vm.PreviewFen = position.Fen;
            vm.CurrentPrompt = $"Step {stepIndex + 1}/{session.Positions.Count}: {position.Prompt}";
            vm.CurrentWhy = position.BetterMoveReason ?? position.CandidateMoves.FirstOrDefault(option => option.IsPreferred)?.Idea?.ShortExplanation ?? string.Empty;
            vm.PreviewArrows = [];
            ReplaceItems(vm.AnswerOptionItems, position.AnswerOptions ?? []);
            vm.SelectedAnswerOption = vm.AnswerOptionItems.FirstOrDefault();
            vm.ResetCurrentHint();
            vm.ResetStudyReference();
            vm.ClearStudySelection();
            vm.OnPropertyChanged(nameof(vm.PreviewArrows));
            vm.OnPropertyChanged(nameof(vm.HasAnswerOptions));
            vm.RaiseStudyNavigationStateChanged();
        }

        public void OnStudyNavigationStateChanged()
        {
            vm.RaiseStudyNavigationStateChanged();
        }

        public void OnSessionCompleted(
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
            IReadOnlyList<OpeningTrainingAttemptResult> attempts)
        {
            vm.RefreshTodayRecommendation();
            vm.LoadOverview();

            IReadOnlyList<TrainingNextAction> nextActions = vm.workspaceService.BuildNextActions(summary);
            TrainingResultLearningPlan learningPlan = vm.workspaceService.BuildLearningPlan(summary, attempts, nextActions);
            vm.resultsViewModel.CompleteSession(
                summary,
                session,
                vm.SelectedOpeningName,
                wrongAttempts,
                vm.playableAnswers,
                vm.transposedAnswers,
                nextActions,
                learningPlan);

            if (savedResult is not null)
            {
                IReadOnlyList<OpeningTrainingScheduledAction> scheduledActions = vm.workspaceService.SaveScheduledActions(savedResult, vm.NextActionItems.ToList());
                foreach ((string sourceActionId, string actionId) in scheduledActions
                    .Where(action => !string.IsNullOrWhiteSpace(action.SourceActionId))
                    .Select(action => (action.SourceActionId!, action.Id)))
                {
                    vm.sessionController.RecordScheduledAction(sourceActionId, actionId);
                }
            }

            vm.RaiseLearningPlanStateChanged();
            vm.RaiseNextActionStateChanged();
            vm.RaiseResultsStateChanged();

            Dictionary<string, string> completionProperties = vm.BuildBaseTelemetryProperties(new Dictionary<string, string>
            {
                ["start_source"] = startSource ?? "unknown",
                ["completed_steps"] = completedSteps.ToString(CultureInfo.InvariantCulture),
                ["wrong_attempts"] = wrongAttempts.ToString(CultureInfo.InvariantCulture),
                ["hint_count"] = hintUseCount.ToString(CultureInfo.InvariantCulture),
                ["not_known_count"] = dontKnowCount.ToString(CultureInfo.InvariantCulture),
                ["time_to_first_move_seconds"] = timeToFirstMoveSeconds.ToString(CultureInfo.InvariantCulture)
            });
            vm.workspaceService.TrackTelemetry(
                OpeningTrainingTelemetryEvents.GuidedSessionCompleted,
                vm.PlayerKey,
                vm.SelectedOpening,
                session,
                recommendationId: recommendationId,
                properties: completionProperties);
            vm.workspaceService.TrackTelemetry(
                OpeningTrainingTelemetryEvents.OpeningLearningPlanShown,
                vm.PlayerKey,
                vm.SelectedOpening,
                session,
                recommendationId: recommendationId,
                properties: completionProperties);

            vm.SetPage(ResultsPageIndex);
        }

        public void OnSessionAbandoned(
            OpeningTrainingSession session,
            string? recommendationId,
            string? startSource,
            int completedSteps,
            int timeToFirstMoveSeconds)
        {
            vm.RefreshTodayRecommendation();
            vm.LoadOverview();
            vm.workspaceService.TrackTelemetry(
                OpeningTrainingTelemetryEvents.GuidedSessionAbandoned,
                vm.PlayerKey,
                vm.SelectedOpening,
                session,
                recommendationId: recommendationId,
                properties: vm.BuildBaseTelemetryProperties(new Dictionary<string, string>
                {
                    ["start_source"] = startSource ?? "unknown",
                    ["completed_steps"] = completedSteps.ToString(CultureInfo.InvariantCulture),
                    ["position_count"] = session.Positions.Count.ToString(CultureInfo.InvariantCulture),
                    ["time_to_first_move_seconds"] = timeToFirstMoveSeconds.ToString(CultureInfo.InvariantCulture)
                }));
        }
    }
}

public sealed record OpeningTrainingProfileChoice(
    string Id,
    string Title,
    string Description,
    RepertoireSide Side,
    string PlayerKey);

public sealed record OpeningTrainingIntensityChoice(
    string Id,
    string Title,
    string Description,
    OpeningTrainingStrictness Strictness);

public sealed record TrainingNextActionCardViewModel(
    TrainingNextAction Action,
    string Title,
    string Reason,
    string TimingText,
    string ButtonText,
    bool IsPrimary)
{
    public static TrainingNextActionCardViewModel Create(TrainingNextAction action, bool isPrimary)
    {
        string timingText = action.DelayMinutes switch
        {
            _ when !isPrimary && action.Kind == TrainingNextActionKind.RepairWeakBranches => "Optional",
            <= 0 => "Ready now",
            < 60 => $"{action.DelayMinutes} min",
            1440 => "Tomorrow",
            _ => $"{Math.Round(action.DelayMinutes / 60d, 1):0.#} hours"
        };

        string buttonText = action.Kind switch
        {
            TrainingNextActionKind.RepeatAfterBreak when action.DelayMinutes > 0 && isPrimary => "Train another opening",
            TrainingNextActionKind.RepeatAfterBreak when action.DelayMinutes > 0 => "Schedule repeat",
            TrainingNextActionKind.RepeatNow => "Repeat now",
            TrainingNextActionKind.ReturnTomorrow => "Back to selection",
            TrainingNextActionKind.RepairWeakBranches => "Open priorities",
            TrainingNextActionKind.PracticeMainLineOnly => "Practice main line only",
            TrainingNextActionKind.ReviewWithHintsAllowed => "Review with hints",
            TrainingNextActionKind.StopForNow => "Stop for now",
            TrainingNextActionKind.BrowseAnotherOpening when string.Equals(action.Id, "train-another-opening", StringComparison.OrdinalIgnoreCase) => "Train another opening",
            TrainingNextActionKind.BrowseAnotherOpening => "Browse openings",
            _ => action.CommandLabel
        };

        return new TrainingNextActionCardViewModel(
            action,
            action.Title,
            action.Description,
            timingText,
            buttonText,
            isPrimary);
    }
}
