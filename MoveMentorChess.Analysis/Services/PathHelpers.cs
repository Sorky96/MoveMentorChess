namespace MoveMentorChess.Analysis;

public static class PathHelpers
{
    public static string? NormalizePath(string? path)
        => string.IsNullOrWhiteSpace(path) ? null : path.Trim();
}
