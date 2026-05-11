namespace MoveMentorChess.Training;

public sealed class LocalModelTrainingPlanFormatter : ITrainingPlanFormatter
{
    private readonly ILocalAdviceModel? model;
    private readonly ITrainingPlanFormatter fallbackFormatter;

    public LocalModelTrainingPlanFormatter(
        ITrainingPlanFormatter? fallbackFormatter = null)
        : this(null, fallbackFormatter)
    {
    }

    public LocalModelTrainingPlanFormatter(
        ILocalAdviceModel? model,
        ITrainingPlanFormatter? fallbackFormatter = null)
    {
        this.model = model;
        this.fallbackFormatter = fallbackFormatter ?? new HeuristicTrainingPlanFormatter();
    }

    public bool UsedFallback { get; private set; }

    public string? FallbackReason { get; private set; }

    public TrainingPlanFormattedOutput Format(
        TrainingPlanReport report,
        PlayerProfileAudienceLevel audienceLevel = PlayerProfileAudienceLevel.Intermediate,
        AdviceNarrationStyle trainerStyle = AdviceNarrationStyle.RegularTrainer)
    {
        ArgumentNullException.ThrowIfNull(report);

        UsedFallback = false;
        FallbackReason = null;

        if (model is null || !model.IsAvailable)
        {
            return FormatFallback(report, audienceLevel, trainerStyle, "Local training plan model is unavailable in this build.");
        }

        try
        {
            string prompt = TrainingPlanLlmPromptFormatter.BuildPrompt(report, audienceLevel, trainerStyle);
            LocalModelAdviceRequest request = new(
                CreateSyntheticReplayPly(),
                MoveQualityBucket.Good,
                null,
                null,
                null,
                MapExplanationLevel(audienceLevel),
                null,
                prompt,
                trainerStyle,
                TrainingPlanLlmPromptFormatter.OutputKeys);

            string? response = model.Generate(request);
            if (!LocalModelTrainingPlanResponseParser.TryParse(response, out TrainingPlanFormattedOutput? output) || output is null)
            {
                return FormatFallback(report, audienceLevel, trainerStyle, "Local training plan model returned invalid training plan JSON.");
            }

            if (!TrainingPlanFormattedOutputValidator.IsValid(output, report))
            {
                return FormatFallback(report, audienceLevel, trainerStyle, "Local training plan model violated the training plan data contract.");
            }

            return output;
        }
        catch (Exception ex)
        {
            return FormatFallback(report, audienceLevel, trainerStyle, $"Local training plan model failed: {ex.Message}");
        }
    }

    private TrainingPlanFormattedOutput FormatFallback(
        TrainingPlanReport report,
        PlayerProfileAudienceLevel audienceLevel,
        AdviceNarrationStyle trainerStyle,
        string reason)
    {
        UsedFallback = true;
        FallbackReason = reason;
        return fallbackFormatter.Format(report, audienceLevel, trainerStyle);
    }

    private static ExplanationLevel MapExplanationLevel(PlayerProfileAudienceLevel audienceLevel)
    {
        return audienceLevel switch
        {
            PlayerProfileAudienceLevel.Beginner => ExplanationLevel.Beginner,
            PlayerProfileAudienceLevel.Advanced => ExplanationLevel.Advanced,
            _ => ExplanationLevel.Intermediate
        };
    }

    private static ReplayPly CreateSyntheticReplayPly()
    {
        return new ReplayPly(
            1,
            1,
            PlayerSide.White,
            "e4",
            "e4",
            "e2e4",
            "startpos",
            "startpos",
            "startpos",
            "startpos",
            GamePhase.Opening,
            "Pawn",
            null,
            "e2",
            "e4",
            false,
            false,
            false);
    }
}
