using MoveMentorChess.Profiles;
using MoveMentorChessServices;
using Xunit;

namespace MoveMentorChessServices.Tests;

public sealed class TrainingPlanDecompositionTests
{
    [Fact]
    public void OutcomeSummarizer_UsesOnlyCompletedTrainingSessions()
    {
        DateTime firstCompleted = new(2026, 1, 2, 12, 0, 0, DateTimeKind.Utc);
        DateTime lastCompleted = new(2026, 1, 3, 12, 0, 0, DateTimeKind.Utc);

        OpeningTrainingOutcomeSummary summary = OpeningTrainingOutcomeSummarizer.Build(
        [
            CreateResult(OpeningTrainingSessionOutcome.Completed, firstCompleted, 4, 2, 1, 1, ["C20"], ["opening_principles"]),
            CreateResult(OpeningTrainingSessionOutcome.Abandoned, firstCompleted.AddHours(1), 5, 0, 0, 5, ["B12"], ["hanging_piece"]),
            CreateResult(OpeningTrainingSessionOutcome.Completed, lastCompleted, 2, 1, 0, 1, ["c20"], ["king_safety"])
        ]);

        Assert.Equal(2, summary.SessionCount);
        Assert.Equal(6, summary.AttemptCount);
        Assert.Equal(3, summary.CorrectCount);
        Assert.Equal(1, summary.PlayableCount);
        Assert.Equal(2, summary.WrongCount);
        Assert.Equal(4d / 6d, summary.Accuracy, 3);
        Assert.Equal(2d / 6d, summary.WrongRate, 3);
        Assert.Equal(lastCompleted, summary.LastCompletedUtc);
        Assert.Equal(["C20"], summary.RelatedOpenings);
        Assert.Equal(["opening_principles", "king_safety"], summary.ThemeLabels);
    }

    [Fact]
    public void TopicScorer_AddsOpeningAndTrainingPressureWithoutFormattingText()
    {
        TrainingPlanTopicScorer scorer = new();
        OpeningTrainingOutcomeSummary summary = new(
            1,
            5,
            1,
            1,
            3,
            0.4,
            0.6,
            new DateTime(2026, 1, 5, 12, 0, 0, DateTimeKind.Utc),
            ["C20"],
            ["opening_principles"]);
        OpeningWeaknessReport openingReport = CreateOpeningReport();

        TrainingPlanTopicScoringResult result = scorer.Score(new TrainingPlanTopicScoringInput(
            "opening_principles",
            2,
            120,
            60,
            ProfileProgressDirection.Stable,
            GamePhase.Opening,
            [new ProfilePhaseStat(GamePhase.Opening, 3)],
            ["C20"],
            openingReport,
            summary));

        Assert.Equal(200, result.PriorityBreakdown.FrequencyScore);
        Assert.Equal(420, result.PriorityBreakdown.CostScore);
        Assert.Equal(60, result.PriorityBreakdown.TrendScore);
        Assert.Equal(90, result.PriorityBreakdown.PhaseScore);
        Assert.Equal(260, result.PriorityBreakdown.TrainingScore);
        Assert.Equal(1_250, result.PriorityBreakdown.TotalScore);
        Assert.Equal(TrainingPlanTopicStatus.Urgent, result.Status);
    }

    [Fact]
    public void TopicNarrativeBuilder_KeepsTrainingPlanTextOutsideServiceOrchestration()
    {
        OpeningTrainingOutcomeSummary summary = new(
            1,
            4,
            1,
            1,
            2,
            0.5,
            0.5,
            new DateTime(2026, 1, 5, 12, 0, 0, DateTimeKind.Utc),
            ["C20"],
            ["opening_principles"]);

        TrainingPlanTopicNarrative narrative = TrainingPlanTopicNarrativeBuilder.Build(new TrainingPlanTopicNarrativeInput(
            "opening_principles",
            2,
            120,
            60,
            ProfileProgressDirection.Regressing,
            GamePhase.Opening,
            GamePhase.Opening,
            ["C20"],
            CreateOpeningReport(),
            summary));

        Assert.Contains("Frequency: this theme appeared 2 times", narrative.WhyThisTopicNow, StringComparison.Ordinal);
        Assert.Contains("Opening trainer: add focused sessions", narrative.WhyThisTopicNow, StringComparison.Ordinal);
        Assert.Contains("Training results: 1 correct, 1 playable and 2 wrong", narrative.WhyThisTopicNow, StringComparison.Ordinal);
        Assert.Contains("Recent games suggest this problem is becoming more urgent", narrative.Rationale, StringComparison.Ordinal);
    }

    private static OpeningTrainingSessionResult CreateResult(
        OpeningTrainingSessionOutcome outcome,
        DateTime completedUtc,
        int attempts,
        int correct,
        int playable,
        int wrong,
        IReadOnlyList<string> relatedOpenings,
        IReadOnlyList<string> themeLabels)
    {
        return new OpeningTrainingSessionResult(
            Guid.NewGuid().ToString("N"),
            "alpha",
            "Alpha",
            completedUtc.AddMinutes(-10),
            completedUtc,
            outcome,
            attempts,
            attempts,
            correct,
            playable,
            wrong,
            relatedOpenings,
            themeLabels,
            []);
    }

    private static OpeningWeaknessReport CreateOpeningReport()
    {
        return new OpeningWeaknessReport(
            "alpha",
            "Alpha",
            4,
            4,
            120,
            [
                new OpeningWeaknessEntry(
                    "C20",
                    "King's Pawn Game",
                    "C20 King's Pawn Game",
                    3,
                    120,
                    "opening_principles",
                    2,
                    OpeningWeaknessCategory.FixNow,
                    ProfileProgressDirection.Regressing,
                    "Opening instability is costly.",
                    [],
                    [],
                    [])
            ],
            []);
    }
}
