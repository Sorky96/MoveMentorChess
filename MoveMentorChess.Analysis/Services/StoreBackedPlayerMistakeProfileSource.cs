namespace MoveMentorChess.Analysis;

public sealed class StoreBackedPlayerMistakeProfileSource : IPlayerMistakeProfileSource
{
    public static StoreBackedPlayerMistakeProfileSource Instance { get; } = new();

    private StoreBackedPlayerMistakeProfileSource()
    {
    }

    public PlayerMistakeProfile? TryBuild(string? playerName)
    {
        return PlayerMistakeProfileProvider.TryBuild(playerName);
    }
}
