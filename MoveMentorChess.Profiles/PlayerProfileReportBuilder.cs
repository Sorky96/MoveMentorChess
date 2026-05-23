using static MoveMentorChess.Profiles.PlayerProfileProgressAnalyzer;
using static MoveMentorChess.Profiles.PlayerProfileStatsAggregator;

namespace MoveMentorChess.Profiles;

internal sealed class PlayerProfileReportBuilder
{
    private const string PreparingTrainingPlanText = "Training plan is being prepared from the current profile.";
    private const string PreparingTrainingBudgetText = "Budget will be calculated after training priorities are ranked.";

    private readonly PlayerRatingTrendAnalyzer ratingTrendAnalyzer;
    private readonly TrainingPlanService trainingPlanService;

    public PlayerProfileReportBuilder(
        PlayerRatingTrendAnalyzer ratingTrendAnalyzer,
        TrainingPlanService? trainingPlanService = null)
    {
        this.ratingTrendAnalyzer = ratingTrendAnalyzer ?? throw new ArgumentNullException(nameof(ratingTrendAnalyzer));
        this.trainingPlanService = trainingPlanService ?? new TrainingPlanService();
    }

    public PlayerProfileReport Build(
        List<PlayerProfileSnapshot> snapshots,
        OpeningWeaknessReport? openingReport,
        IReadOnlyList<OpeningTrainingSessionResult> trainingHistory)
    {
        string playerKey = snapshots[0].PlayerKey;
        string displayName = SelectDisplayName(snapshots);

        List<HighlightedGroup> highlightedGroups = snapshots
            .SelectMany(GetHighlightedGroups)
            .ToList();
        IReadOnlyList<StoredMoveAnalysis> mistakeMoves = snapshots
            .SelectMany(snapshot => snapshot.Moves)
            .Where(move => move.Quality.IsProblem() && !string.IsNullOrWhiteSpace(move.MistakeLabel))
            .ToList();
        PlayerProfileAggregateStats aggregateStats = BuildReportStats(snapshots, highlightedGroups, mistakeMoves);

        ProfileProgressSignal progressSignal = BuildProgressSignal(snapshots);
        IReadOnlyList<ProfileLabelTrend> labelTrends = BuildLabelTrends(snapshots);
        PlayerRatingTrendReport overallRatingTrend = ratingTrendAnalyzer.Build(snapshots, null);
        IReadOnlyList<PlayerRatingTrendReport> ratingTrendsByTimeControl = ratingTrendAnalyzer.BuildByTimeControl(snapshots);
        IReadOnlyList<ProfileMistakeExample> allExamples = PlayerProfileMistakeExampleBuilder.Build(snapshots, aggregateStats.TopLabels, 9);

        PlayerProfileReport draftReport = BuildReport(
            playerKey,
            displayName,
            snapshots,
            highlightedGroups,
            aggregateStats,
            overallRatingTrend,
            ratingTrendsByTimeControl,
            progressSignal,
            labelTrends,
            [],
            BuildPreparingWeeklyPlan(displayName),
            allExamples,
            null);
        TrainingPlanReport trainingPlan = trainingPlanService.Build(draftReport, openingReport, trainingHistory);

        return BuildReport(
            playerKey,
            displayName,
            snapshots,
            highlightedGroups,
            aggregateStats,
            overallRatingTrend,
            ratingTrendsByTimeControl,
            progressSignal,
            labelTrends,
            trainingPlan.Recommendations,
            trainingPlan.WeeklyPlan,
            allExamples,
            trainingPlan);
    }

    private static PlayerProfileReport BuildReport(
        string playerKey,
        string displayName,
        List<PlayerProfileSnapshot> snapshots,
        List<HighlightedGroup> highlightedGroups,
        PlayerProfileAggregateStats aggregateStats,
        PlayerRatingTrendReport overallRatingTrend,
        IReadOnlyList<PlayerRatingTrendReport> ratingTrendsByTimeControl,
        ProfileProgressSignal progressSignal,
        IReadOnlyList<ProfileLabelTrend> labelTrends,
        IReadOnlyList<TrainingRecommendation> recommendations,
        WeeklyTrainingPlan weeklyPlan,
        IReadOnlyList<ProfileMistakeExample> allExamples,
        TrainingPlanReport? trainingPlan)
    {
        return new PlayerProfileReport(
            playerKey,
            displayName,
            snapshots.Count,
            snapshots.Sum(snapshot => snapshot.Moves.Count),
            highlightedGroups.Count,
            TryAverage(snapshots.SelectMany(snapshot => snapshot.Moves).Select(move => move.CentipawnLoss)),
            aggregateStats.TopLabels,
            aggregateStats.CostliestLabels,
            aggregateStats.MistakesByPhase,
            aggregateStats.MistakesByOpening,
            aggregateStats.GamesBySide,
            aggregateStats.MonthlyTrend,
            aggregateStats.QuarterlyTrend,
            overallRatingTrend,
            ratingTrendsByTimeControl,
            progressSignal,
            labelTrends,
            recommendations,
            weeklyPlan,
            allExamples,
            trainingPlan ?? new TrainingPlanReport(
                playerKey,
                displayName,
                progressSignal.Direction,
                PreparingTrainingPlanText,
                [],
                [],
                weeklyPlan));
    }

    private static WeeklyTrainingPlan BuildPreparingWeeklyPlan(string displayName)
    {
        return new WeeklyTrainingPlan(
            $"{displayName} Weekly Training Plan",
            PreparingTrainingPlanText,
            new WeeklyTrainingBudget(
                0,
                0,
                0,
                0,
                0,
                PreparingTrainingBudgetText),
            []);
    }
}
