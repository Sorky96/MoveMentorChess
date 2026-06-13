namespace MoveMentorChess.Profiles;

public sealed class ProfileMlPlayerStrengthEstimator : IPlayerStrengthEstimator
{
    public MoveMentorStrengthPoint Estimate(PlayerStrengthEstimateInput input)
    {
        throw new NotSupportedException("Per-profile ML strength estimation is planned but not implemented yet.");
    }
}
