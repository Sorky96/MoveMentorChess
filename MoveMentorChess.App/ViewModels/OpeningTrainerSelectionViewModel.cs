using System.Collections.ObjectModel;
using MoveMentorChess.Localization;

namespace MoveMentorChess.App.ViewModels;

public sealed class OpeningTrainerSelectionViewModel : ViewModelBase
{
    private readonly OpeningTrainerWorkspaceService workspaceService;
    private string advancedPlayerKey = string.Empty;
    private OpeningTrainingProfileChoice? selectedProfileChoice;
    private RepertoireSide selectedSide = RepertoireSide.Both;
    private OpeningTrainingIntensityChoice? selectedIntensityChoice;
    private OpeningTrainingStrictness selectedStrictness = OpeningTrainingStrictness.BookFlexible;
    private TrainingRecommendationCard? todayRecommendation;
    private PlayerOpeningPlan? playerOpeningPlan;
    private SpecialTrainingModeDefinition? selectedSpecialMode;

    public OpeningTrainerSelectionViewModel(OpeningTrainerWorkspaceService workspaceService)
    {
        this.workspaceService = workspaceService ?? throw new ArgumentNullException(nameof(workspaceService));
        selectedProfileChoice = AvailableProfileChoices.First(choice => choice.Id == "both");
        selectedIntensityChoice = AvailableIntensityChoices.First(choice => choice.Id == "balanced");
        selectedSide = selectedProfileChoice.Side;
        selectedStrictness = selectedIntensityChoice.Strictness;
    }

    public IReadOnlyList<OpeningTrainingProfileChoice> AvailableProfileChoices { get; } =
    [
        new("white", Localizer.Text(LocalizedStrings.OpeningTrainerProfileWhiteName), Localizer.Text(LocalizedStrings.OpeningTrainerProfileWhiteDescription), RepertoireSide.White, "opening-coach:white"),
        new("black", Localizer.Text(LocalizedStrings.OpeningTrainerProfileBlackName), Localizer.Text(LocalizedStrings.OpeningTrainerProfileBlackDescription), RepertoireSide.Black, "opening-coach:black"),
        new("both", Localizer.Text(LocalizedStrings.OpeningTrainerProfileBothName), Localizer.Text(LocalizedStrings.OpeningTrainerProfileBothDescription), RepertoireSide.Both, "opening-coach:both")
    ];

    public IReadOnlyList<OpeningTrainingIntensityChoice> AvailableIntensityChoices { get; } =
    [
        new("safe", Localizer.Text(LocalizedStrings.OpeningTrainerIntensitySafeName), Localizer.Text(LocalizedStrings.OpeningTrainerIntensitySafeDescription), OpeningTrainingStrictness.StrictRepertoire),
        new("balanced", Localizer.Text(LocalizedStrings.OpeningTrainerIntensityBalancedName), Localizer.Text(LocalizedStrings.OpeningTrainerIntensityBalancedDescription), OpeningTrainingStrictness.BookFlexible),
        new("challenge", Localizer.Text(LocalizedStrings.OpeningTrainerIntensityChallengeName), Localizer.Text(LocalizedStrings.OpeningTrainerIntensityChallengeDescription), OpeningTrainingStrictness.Exploration)
    ];

    public IReadOnlyList<RepertoireSide> AvailableSides { get; } = Enum.GetValues<RepertoireSide>();

    public IReadOnlyList<OpeningTrainingStrictness> AvailableStrictnessOptions { get; } = Enum.GetValues<OpeningTrainingStrictness>();

    public ObservableCollection<PlayerOpeningPlanItem> TodayPlanItems { get; } = [];

    public ObservableCollection<PlayerOpeningPlanItem> WeeklyPlanItems { get; } = [];

    public ObservableCollection<PlayerOpeningPlanItem> LongTermGapItems { get; } = [];

    public ObservableCollection<SpecialTrainingModeDefinition> SpecialTrainingModes { get; } = [];

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
            }
        }
    }

    public string SelectedProfileSummary => SelectedProfileChoice is null
        ? Localizer.Text(LocalizedStrings.OpeningTrainerSelectedProfilePlaceholder)
        : SelectedProfileChoice.Description;

    public RepertoireSide SelectedSide
    {
        get => selectedSide;
        set => SetProperty(ref selectedSide, value);
    }

    public OpeningTrainingStrictness SelectedStrictness
    {
        get => selectedStrictness;
        set => SetProperty(ref selectedStrictness, value);
    }

    public OpeningTrainingIntensityChoice? SelectedIntensityChoice
    {
        get => selectedIntensityChoice;
        set
        {
            if (SetProperty(ref selectedIntensityChoice, value))
            {
                SelectedStrictness = value?.Strictness ?? OpeningTrainingStrictness.BookFlexible;
                OnPropertyChanged(nameof(SelectedIntensitySummary));
                OnPropertyChanged(nameof(TodayStartSequenceText));
            }
        }
    }

    public string SelectedIntensitySummary => SelectedIntensityChoice?.Description
        ?? Localizer.Text(LocalizedStrings.OpeningTrainerSelectedIntensityPlaceholder);

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

    public PlayerOpeningPlan? PlayerOpeningPlan
    {
        get => playerOpeningPlan;
        private set
        {
            if (SetProperty(ref playerOpeningPlan, value))
            {
                RaisePlayerOpeningPlanStateChanged();
            }
        }
    }

    public bool HasTodayRecommendation => TodayRecommendation is not null;

    public string TodayRecommendationOpening => TodayRecommendation?.OpeningLine.DisplayName ?? Localizer.Text(LocalizedStrings.OpeningTrainerNoRecommendation);

    public string TodayRecommendationMeta => TodayRecommendation is null
        ? Localizer.Text(LocalizedStrings.OpeningTrainerImportTheoryForRecommendations)
        : Localizer.Format(
            LocalizedStrings.OpeningTrainerRecommendationMeta,
            FormatRepertoireSide(TodayRecommendation.OpeningLine.RepertoireSide),
            TodayRecommendation.Difficulty,
            TodayRecommendation.EstimatedDurationMinutes);

    public string TodayRecommendationReason => TodayRecommendation?.Reason ?? Localizer.Text(LocalizedStrings.OpeningTrainerNeedsOpeningLine);

    public string TodayRecommendationAction => TodayRecommendation?.RecommendedAction ?? Localizer.Text(LocalizedStrings.OpeningTrainerStartPractice);

    public string TodayLessonOpening => TodayRecommendation?.OpeningLine.DisplayName ?? Localizer.Text(LocalizedStrings.OpeningTrainerChooseOpeningFirst);

    public string TodayLessonSideText => TodayRecommendation is null
        ? Localizer.Text(LocalizedStrings.OpeningTrainerNoActiveTheory)
        : TodayRecommendation.OpeningLine.RepertoireSide switch
        {
            RepertoireSide.White => Localizer.Text(LocalizedStrings.OpeningTrainerWhiteRepertoire),
            RepertoireSide.Black => Localizer.Text(LocalizedStrings.OpeningTrainerBlackRepertoire),
            _ => Localizer.Text(LocalizedStrings.OpeningTrainerBothSides)
        };

    public string TodayLessonDurationText => TodayRecommendation is null
        ? Localizer.Text(LocalizedStrings.OpeningTrainerDurationAfterImport)
        : Localizer.Format(LocalizedStrings.OpeningTrainerAboutMinutes, TodayRecommendation.EstimatedDurationMinutes);

    public string TodayLessonMoveCountText => TodayRecommendation is null
        ? Localizer.Text(LocalizedStrings.OpeningTrainerNoPositionsToTrain)
        : TodayRecommendation.OpeningLine.BookBranchCount > 0
            ? Localizer.Format(LocalizedStrings.OpeningTrainerPositionsBranches, TodayRecommendation.OpeningLine.BookBranchCount)
            : Localizer.Format(LocalizedStrings.OpeningTrainerTheoryGames, Math.Max(1, TodayRecommendation.OpeningLine.BookGameCount));

    public string TodayLessonReason => TodayRecommendation?.Reason ?? Localizer.Text(LocalizedStrings.OpeningTrainerImportOrChooseOpening);

    public string TodayStartSequenceText => SelectedIntensityChoice?.Id switch
    {
        "safe" => Localizer.Text(LocalizedStrings.OpeningTrainerStartSequenceSafe),
        "challenge" => Localizer.Text(LocalizedStrings.OpeningTrainerStartSequenceChallenge),
        _ => Localizer.Text(LocalizedStrings.OpeningTrainerStartSequenceBalanced)
    };

    public string TodayTrainingReasonLabel => HasTodayLesson
        ? Localizer.Text(LocalizedStrings.OpeningTrainerRecommendedBecause)
        : Localizer.Text(LocalizedStrings.OpeningTrainerReadyWhenYouAre);

    public string TodayLessonButtonText => HasTodayLesson
        ? Localizer.Text(LocalizedStrings.OpeningTrainerStartGuidedTraining)
        : Localizer.Text(LocalizedStrings.OpeningTrainerImportOpeningsFirst);

    public bool HasTodayLesson => TodayRecommendation is not null;

    public string PlayerOpeningPlanTitle => Localizer.Text(LocalizedStrings.OpeningTrainerTrainingRhythmTitle);

    public string PlayerOpeningPlanSummary => PlayerOpeningPlan?.Summary ?? Localizer.Text(LocalizedStrings.OpeningTrainerTrainingRhythmPlaceholder);

    public string PlayerOpeningProgressText => PlayerOpeningPlan is null
        ? Localizer.Text(LocalizedStrings.OpeningTrainerNoPracticeHistory)
        : PlayerOpeningPlan.Progress.SessionCount == 0
            ? Localizer.Text(LocalizedStrings.OpeningTrainerStartSessionForProgress)
            : Localizer.Format(
                LocalizedStrings.OpeningTrainerProgressMovesAccepted,
                PlayerOpeningPlan.Progress.AttemptCount,
                PlayerOpeningPlan.Progress.AccuracyPercent);

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
                OnPropertyChanged(nameof(SelectedSpecialModeDescription));
                OnPropertyChanged(nameof(SelectedSpecialModeButtonText));
            }
        }
    }

    public string SelectedSpecialModeDescription => SelectedSpecialMode?.Description ?? "Choose a special mode to start a focused preset.";

    public string SelectedSpecialModeButtonText => SelectedSpecialMode?.CommandLabel ?? "Start special mode";

    public void RefreshTodayRecommendation()
    {
        TodayRecommendation = workspaceService.GetRecommendationForToday(PlayerKey, SelectedSide, 120);
        PlayerOpeningPlan = workspaceService.GetPlayerOpeningPlan(PlayerKey, SelectedSide, 120);
        ReplaceItems(TodayPlanItems, PlayerOpeningPlan.Today);
        ReplaceItems(WeeklyPlanItems, PlayerOpeningPlan.ThisWeek);
        ReplaceItems(LongTermGapItems, PlayerOpeningPlan.LongTermGaps);
        ReplaceItems(SpecialTrainingModes, workspaceService.ListSpecialTrainingModes());
        SelectedSpecialMode ??= SpecialTrainingModes.FirstOrDefault();
    }

    public string BuildTodayDecisionSummary(OpeningTrainerOverview? overview, OpeningLineCatalogItem? selectedOpening)
    {
        if (TodayRecommendation is null)
        {
            return Localizer.Text(LocalizedStrings.OpeningTrainerRecommendedTodayUnavailable);
        }

        return Localizer.Format(
            LocalizedStrings.OpeningTrainerTodayDecisionSummary,
            GetRecommendedPositionCount(overview, selectedOpening),
            CountReviewMoves(overview, selectedOpening),
            GetEstimatedDurationText(overview),
            FormatRepertoireSide(TodayRecommendation.OpeningLine.RepertoireSide));
    }

    public string BuildTodayLessonReasonDetail(OpeningTrainerOverview? overview)
    {
        if (TodayRecommendation is null)
        {
            return Localizer.Text(LocalizedStrings.OpeningTrainerTheoryReasonPlaceholder);
        }

        int weakBranches = overview?.Coverage.WeakBranches ?? TodayRecommendation.OpeningLine.BookBranchCount;
        string reason = TodayRecommendation.Reason.Trim();
        if (TodayRecommendation.ReasonCode == TrainingRecommendationReasonCode.RevisitDue && TodayRecommendation.Priority >= 10_000)
        {
            reason = Localizer.Text(LocalizedStrings.OpeningTrainerReviewDueScheduled);
        }

        string modeContext = SelectedIntensityChoice?.Id switch
        {
            "safe" => Localizer.Text(LocalizedStrings.OpeningTrainerModeContextSafe),
            "challenge" => Localizer.Text(LocalizedStrings.OpeningTrainerModeContextChallenge),
            _ => Localizer.Text(LocalizedStrings.OpeningTrainerModeContextBalanced)
        };
        string goal = weakBranches > 0
            ? Localizer.Format(LocalizedStrings.OpeningTrainerGoalRepairWeakBranches, weakBranches)
            : Localizer.Text(LocalizedStrings.OpeningTrainerGoalStableRecall);

        return $"{reason} {modeContext}{Environment.NewLine}{goal}";
    }

    private string GetEstimatedDurationText(OpeningTrainerOverview? overview)
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

    private int GetRecommendedPositionCount(OpeningTrainerOverview? overview, OpeningLineCatalogItem? selectedOpening)
    {
        if (overview is not null && TodayRecommendation is not null && Equals(selectedOpening, TodayRecommendation.OpeningLine))
        {
            return Math.Max(1, overview.Coverage.WeakBranches > 0 ? overview.Coverage.WeakBranches : overview.MainLine.Count);
        }

        return TodayRecommendation is null
            ? 0
            : Math.Max(1, TodayRecommendation.OpeningLine.BookBranchCount);
    }

    public int CountReviewMoves(OpeningTrainerOverview? overview, OpeningLineCatalogItem? selectedOpening)
    {
        string? targetEco = TodayRecommendation?.OpeningLine.Eco ?? selectedOpening?.Eco;
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

        return Math.Max(1, GetRecommendedPositionCount(overview, selectedOpening));
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
        OnPropertyChanged(nameof(TodayTrainingReasonLabel));
        OnPropertyChanged(nameof(TodayLessonButtonText));
        OnPropertyChanged(nameof(HasTodayLesson));
    }

    private void RaisePlayerOpeningPlanStateChanged()
    {
        OnPropertyChanged(nameof(PlayerOpeningPlanTitle));
        OnPropertyChanged(nameof(PlayerOpeningPlanSummary));
        OnPropertyChanged(nameof(PlayerOpeningProgressText));
        OnPropertyChanged(nameof(PlayerOpeningProgressInterpretation));
    }

    private static string FormatRepertoireSide(RepertoireSide side)
        => side switch
        {
            RepertoireSide.White => Localizer.Text(LocalizedStrings.CommonWhite),
            RepertoireSide.Black => Localizer.Text(LocalizedStrings.CommonBlack),
            _ => Localizer.Text(LocalizedStrings.OpeningTrainerBothSides)
        };

    private static void ReplaceItems<T>(ObservableCollection<T> collection, IEnumerable<T> items)
    {
        collection.Clear();
        foreach (T item in items)
        {
            collection.Add(item);
        }
    }
}
