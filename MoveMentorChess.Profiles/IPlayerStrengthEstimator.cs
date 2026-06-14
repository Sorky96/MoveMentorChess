namespace MoveMentorChess.Profiles;

public interface IPlayerStrengthEstimator
{
    MoveMentorStrengthPoint Estimate(PlayerStrengthEstimateInput input);
}
