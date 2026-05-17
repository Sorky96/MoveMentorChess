namespace MoveMentorChess.App.ViewModels;

public sealed class OpeningTrainerTelemetryAdapter
{
    public Dictionary<string, string> BuildRecommendationProperties(
        TrainingRecommendationCard recommendation,
        OpeningTrainingProfileChoice? selectedProfileChoice,
        RepertoireSide selectedSide,
        string advancedPlayerKey)
    {
        ArgumentNullException.ThrowIfNull(recommendation);

        return BuildBaseProperties(
            selectedProfileChoice,
            selectedSide,
            advancedPlayerKey,
            new Dictionary<string, string>
            {
                ["reason_code"] = recommendation.ReasonCode.ToString(),
                ["recommendation_type"] = recommendation.RecommendationType.ToString()
            });
    }

    public Dictionary<string, string> BuildBaseProperties(
        OpeningTrainingProfileChoice? selectedProfileChoice,
        RepertoireSide selectedSide,
        string advancedPlayerKey,
        Dictionary<string, string>? properties = null)
    {
        Dictionary<string, string> result = properties is null
            ? []
            : new Dictionary<string, string>(properties, StringComparer.OrdinalIgnoreCase);

        result["profile_choice"] = selectedProfileChoice?.Id ?? "unknown";
        result["side"] = selectedSide.ToString();
        result["advanced_history_key_active"] = (!string.IsNullOrWhiteSpace(advancedPlayerKey)).ToString().ToLowerInvariant();
        return result;
    }
}
