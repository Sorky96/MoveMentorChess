using MoveMentorChess.Profiles;

namespace MoveMentorChess.App.ViewModels;

internal interface IProfileFormattingWorkflow
{
    Task<ProfileFormattingResult> FormatAsync(PlayerProfileReport report);

    ProfileFormattingResult FormatFallback(PlayerProfileReport report);
}

internal sealed record ProfileFormattingResult(
    PlayerProfileFormattedOutput Profile,
    TrainingPlanFormattedOutput TrainingPlan);
