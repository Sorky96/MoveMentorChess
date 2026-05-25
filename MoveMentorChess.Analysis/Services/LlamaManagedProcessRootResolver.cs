namespace MoveMentorChess.Analysis;

public sealed class LlamaManagedProcessRootResolver
{
    public IReadOnlySet<string> Resolve(ILlamaRuntimeEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(environment);

        HashSet<string> roots = new(StringComparer.OrdinalIgnoreCase);

        AddRoot(roots, environment.BaseDirectory);
        AddRoot(roots, environment.CurrentDirectory);

        return roots;
    }

    private static void AddRoot(HashSet<string> roots, string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        try
        {
            string normalized = NormalizeDirectorySeparator(Path.GetFullPath(path));
            roots.Add(normalized);
        }
        catch (Exception ex) when (ex is IOException or ArgumentException or NotSupportedException)
        {
            // Ignore invalid paths.
        }
    }

    private static string NormalizeDirectorySeparator(string path)
    {
        return path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
    }
}
