using MoveMentorChess.Analysis;
using Xunit;

namespace MoveMentorChessServices.Tests.Analysis;

public sealed class LlamaManagedProcessRootResolverTests
{
    [Fact]
    public void ResolveNormalizesAndDeduplicatesRuntimeRoots()
    {
        string root = Path.GetFullPath(Path.Join(Path.GetTempPath(), "MoveMentorChessTests", "llama-root"));
        TestLlamaRuntimeEnvironment environment = new(root, root + Path.DirectorySeparatorChar);
        LlamaManagedProcessRootResolver resolver = new();

        IReadOnlySet<string> roots = resolver.Resolve(environment);

        Assert.Single(roots);
        Assert.Contains(Normalize(root), roots);
    }

    [Fact]
    public void ResolveIgnoresBlankRuntimeRoots()
    {
        TestLlamaRuntimeEnvironment environment = new(" ", string.Empty);
        LlamaManagedProcessRootResolver resolver = new();

        IReadOnlySet<string> roots = resolver.Resolve(environment);

        Assert.Empty(roots);
    }

    private static string Normalize(string path)
        => path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;

    private sealed record TestLlamaRuntimeEnvironment(
        string BaseDirectory,
        string CurrentDirectory) : ILlamaRuntimeEnvironment;
}
