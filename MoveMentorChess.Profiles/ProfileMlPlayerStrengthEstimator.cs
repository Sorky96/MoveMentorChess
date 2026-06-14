namespace MoveMentorChess.Profiles;

[Obsolete("Profile ML strength estimation is disabled until the ML model is implemented; this estimator returns a low-confidence fallback.", false)]
public sealed class ProfileMlPlayerStrengthEstimator : IPlayerStrengthEstimator
{
    public MoveMentorStrengthPoint Estimate(PlayerStrengthEstimateInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        const int fallbackStrength = 1200;
        int estimatedStrength = Math.Clamp(input.PlayerRating ?? fallbackStrength, 100, 3200);
        const int range = 250;

        return new MoveMentorStrengthPoint(
            input.GameFingerprint,
            input.GameDate,
            input.TimeControlCategory,
            estimatedStrength,
            Math.Max(100, estimatedStrength - range),
            Math.Min(3200, estimatedStrength + range),
            MoveMentorStrengthConfidence.Low,
            MoveMentorStrengthEstimatorKind.ProfileMl,
            "Profile ML estimator is disabled; returned a low-confidence fallback anchored to the known player rating when available.");
    }
}
