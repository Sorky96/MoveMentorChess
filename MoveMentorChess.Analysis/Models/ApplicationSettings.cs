namespace MoveMentorChess.Analysis;

public sealed record ApplicationSettings(string? CultureName)
{
    public static ApplicationSettings Default { get; } = new(CultureName: null);
}
