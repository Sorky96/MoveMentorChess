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

    [Fact]
    public void ResolveIgnoresRuntimeEnvironmentPathReadFailures()
    {
        LlamaManagedProcessRootResolver resolver = new();

        IReadOnlySet<string> roots = resolver.Resolve(new ThrowingLlamaRuntimeEnvironment());

        Assert.Empty(roots);
    }

    private static string Normalize(string path)
        => path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;

    private sealed record TestLlamaRuntimeEnvironment(
        string BaseDirectory,
        string CurrentDirectory) : ILlamaRuntimeEnvironment
    {
        public string? GetEnvironmentVariable(string variable) => null;

        public LlamaGpuSettings LoadLlamaGpuSettings() => LlamaGpuSettings.Default;

        public bool FileExists(string path) => false;

        public bool DirectoryExists(string path) => false;

        public IEnumerable<string> EnumerateFiles(string path, string searchPattern)
            => [];
    }

    private sealed class ThrowingLlamaRuntimeEnvironment : ILlamaRuntimeEnvironment
    {
        public string BaseDirectory => throw new UnauthorizedAccessException();

        public string CurrentDirectory => throw new NotSupportedException();

        public string? GetEnvironmentVariable(string variable) => throw new NotSupportedException();

        public LlamaGpuSettings LoadLlamaGpuSettings() => throw new NotSupportedException();

        public bool FileExists(string path) => throw new NotSupportedException();

        public bool DirectoryExists(string path) => throw new NotSupportedException();

        public IEnumerable<string> EnumerateFiles(string path, string searchPattern)
            => throw new NotSupportedException();
    }
}
