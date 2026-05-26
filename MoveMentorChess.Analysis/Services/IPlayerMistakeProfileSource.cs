namespace MoveMentorChess.Analysis;

public interface IPlayerMistakeProfileSource
{
    PlayerMistakeProfile? TryBuild(string? playerName);
}
