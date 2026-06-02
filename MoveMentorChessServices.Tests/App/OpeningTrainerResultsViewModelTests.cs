using MoveMentorChess.App.ViewModels;
using Xunit;

namespace MoveMentorChessServices.Tests.App;

public sealed class OpeningTrainerResultsViewModelTests
{
    [Fact]
    public void InitialState_ExposesResultsPlaceholders()
    {
        OpeningTrainerResultsViewModel viewModel = new();

        Assert.Equal("Finish practice to see your review plan.", viewModel.ResultHeadline);
        Assert.Equal("0/0", viewModel.ResultsCompletedMetricText(null, 0));
        Assert.False(viewModel.HasNextActions);
        Assert.Equal("Finish a session to unlock the next action plan.", viewModel.NextActionsPlaceholder);
        Assert.Equal("No urgent review positions from this run.", viewModel.LearningPlanReviewPlaceholder);
    }

    [Fact]
    public void CompleteSession_BuildsNextActionCardsAndLearningPlanState()
    {
        OpeningTrainerResultsViewModel viewModel = new();
        TrainingSessionOutcomeSummary summary = new(
            "Almost stable",
            3,
            3,
            2,
            1,
            0,
            1,
            100,
            100);
        TrainingNextAction firstAction = new(
            "repeat-after-break",
            TrainingNextActionKind.RepeatAfterBreak,
            "Repeat after a short break",
            "Make the line automatic after spacing.",
            "Repeat later",
            90,
            10);
        TrainingNextAction secondAction = new(
            "stop-for-now",
            TrainingNextActionKind.StopForNow,
            "Stop for now",
            "Come back later.",
            "Stop",
            10);
        TrainingResultReviewItem reviewItem = new("p1", "Nf3", "Repeat this move.", 80);
        TrainingResultLearningPlan learningPlan = new(
            "Completed: 3/3",
            "To review: Nf3",
            "Next review: after break",
            "Reason: one hint used.",
            [reviewItem]);

        viewModel.CompleteSession(summary, null, "King's Pawn", 0, 1, 0, [firstAction, secondAction], learningPlan);

        Assert.Equal(summary, viewModel.OutcomeSummary);
        Assert.Equal(learningPlan, viewModel.LearningPlan);
        Assert.Equal("Practice finished.", viewModel.ResultHeadline);
        Assert.True(viewModel.HasNextActions);
        Assert.True(viewModel.HasPrimaryNextAction);
        Assert.True(viewModel.HasSecondaryNextActions);
        Assert.Equal(firstAction, viewModel.SelectedNextAction);
        Assert.Equal(secondAction, viewModel.SelectedSecondaryNextAction?.Action);
        Assert.Equal("Completed: 3/3", viewModel.LearningPlanMasteredText);
        Assert.True(viewModel.HasLearningPlanReviewItems);
        Assert.True(viewModel.HasAdvancedResultDetails);
    }

    [Fact]
    public void Reset_ClearsCompletedResults()
    {
        OpeningTrainerResultsViewModel viewModel = new();
        TrainingSessionOutcomeSummary summary = new("Stable", 1, 1, 1, 0, 0, 0, 100, 100);
        TrainingNextAction action = new(
            "stop-for-now",
            TrainingNextActionKind.StopForNow,
            "Stop for now",
            "Come back later.",
            "Stop",
            10);
        TrainingResultLearningPlan learningPlan = new(
            "Completed: 1/1",
            "To review: none",
            "Next review: tomorrow",
            "Reason: clean.",
            []);

        viewModel.CompleteSession(summary, null, "King's Pawn", 0, 0, 0, [action], learningPlan);
        viewModel.Reset();

        Assert.Null(viewModel.OutcomeSummary);
        Assert.Null(viewModel.LearningPlan);
        Assert.Empty(viewModel.NextActionItems);
        Assert.Empty(viewModel.LearningPlanReviewItems);
        Assert.Equal("Practice in progress.", viewModel.ResultHeadline);
        Assert.Equal("Finish the run to get your next review step.", viewModel.ResultRecommendation);
    }
}
