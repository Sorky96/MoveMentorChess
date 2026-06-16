using MoveMentorChess.Analysis;
using MoveMentorChess.App.ViewModels;
using MoveMentorChess.Profiles;

namespace MoveMentorChess.App.Composition;

internal sealed class DefaultProfileFormattingWorkflow : IProfileFormattingWorkflow
{
    private readonly IPlayerProfileFormatter profileFormatter;
    private readonly ITrainingPlanFormatter trainingPlanFormatter;
    private readonly Func<LlamaGpuSettings> settingsProvider;

    public DefaultProfileFormattingWorkflow(
        IPlayerProfileFormatter? profileFormatter = null,
        ITrainingPlanFormatter? trainingPlanFormatter = null)
        : this(
            profileFormatter ?? PlayerProfileFormatterFactory.CreateDefault(),
            trainingPlanFormatter ?? TrainingPlanFormatterFactory.CreateDefault(),
            LlamaGpuSettingsStore.Load)
    {
    }

    internal DefaultProfileFormattingWorkflow(
        IPlayerProfileFormatter profileFormatter,
        ITrainingPlanFormatter trainingPlanFormatter,
        Func<LlamaGpuSettings> settingsProvider)
    {
        this.profileFormatter = profileFormatter ?? throw new ArgumentNullException(nameof(profileFormatter));
        this.trainingPlanFormatter = trainingPlanFormatter ?? throw new ArgumentNullException(nameof(trainingPlanFormatter));
        this.settingsProvider = settingsProvider ?? throw new ArgumentNullException(nameof(settingsProvider));
    }

    public Task<ProfileFormattingResult> FormatAsync(PlayerProfileReport report)
    {
        ArgumentNullException.ThrowIfNull(report);

        return Task.Run(() =>
        {
            LlamaGpuSettings settings = settingsProvider();
            PlayerProfileAudienceLevel audienceLevel = ToProfileAudienceLevel(settings.DefaultExplanationLevel);
            AdviceNarrationStyle narrationStyle = settings.NarrationStyle;

            return new ProfileFormattingResult(
                profileFormatter.Format(report, audienceLevel, narrationStyle),
                trainingPlanFormatter.Format(report.TrainingPlan, audienceLevel, narrationStyle));
        });
    }

    public ProfileFormattingResult FormatFallback(PlayerProfileReport report)
    {
        ArgumentNullException.ThrowIfNull(report);

        return new ProfileFormattingResult(
            new HeuristicPlayerProfileFormatter().Format(report),
            new HeuristicTrainingPlanFormatter().Format(report.TrainingPlan));
    }

    private static PlayerProfileAudienceLevel ToProfileAudienceLevel(ExplanationLevel explanationLevel)
    {
        return explanationLevel switch
        {
            ExplanationLevel.Beginner => PlayerProfileAudienceLevel.Beginner,
            ExplanationLevel.Advanced => PlayerProfileAudienceLevel.Advanced,
            _ => PlayerProfileAudienceLevel.Intermediate
        };
    }
}
