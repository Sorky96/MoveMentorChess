using MoveMentorChess.Analysis;
using MoveMentorChess.App.Composition;
using Xunit;

namespace MoveMentorChessServices.Tests.App;

public sealed class DefaultStockfishPathResolverTests
{
    [Fact]
    public void ResolvePrefersStockfishNextToApp()
    {
        string baseDirectory = TestPath("app");
        string currentDirectory = TestPath("repo");
        string expected = Path.GetFullPath(Path.Join(baseDirectory, "stockfish.exe"));
        FakeAppRuntimeEnvironment environment = new(baseDirectory, currentDirectory, [expected]);
        DefaultStockfishPathResolver resolver = new(environment);

        string? resolved = resolver.Resolve();

        Assert.Equal(expected, resolved);
    }

    [Fact]
    public void ResolvePrefersConfiguredStockfishPath()
    {
        string baseDirectory = TestPath("app");
        string currentDirectory = TestPath("repo");
        string configured = Path.GetFullPath(Path.Join(TestPath("custom"), "stockfish.exe"));
        string nextToApp = Path.GetFullPath(Path.Join(baseDirectory, "stockfish.exe"));
        FakeAppRuntimeEnvironment environment = new(baseDirectory, currentDirectory, [configured, nextToApp]);
        DefaultStockfishPathResolver resolver = new(
            environment,
            () => StockfishSettings.Default with { ExecutablePath = configured });

        string? resolved = resolver.Resolve();

        Assert.Equal(configured, resolved);
    }

    [Fact]
    public void ResolveChecksCurrentDirectoryFallback()
    {
        string baseDirectory = TestPath("app");
        string currentDirectory = TestPath("repo");
        string expected = Path.GetFullPath(Path.Join(currentDirectory, "stockfish.exe"));
        FakeAppRuntimeEnvironment environment = new(baseDirectory, currentDirectory, [expected]);
        DefaultStockfishPathResolver resolver = new(environment);

        string? resolved = resolver.Resolve();

        Assert.Equal(expected, resolved);
    }

    [Fact]
    public void ResolveReturnsNullWhenNoCandidateExists()
    {
        FakeAppRuntimeEnvironment environment = new(
            TestPath("app"),
            TestPath("repo"),
            []);
        DefaultStockfishPathResolver resolver = new(environment);

        string? resolved = resolver.Resolve();

        Assert.Null(resolved);
    }

    private sealed class FakeAppRuntimeEnvironment(
        string baseDirectory,
        string currentDirectory,
        IReadOnlyCollection<string> existingFiles) : IAppRuntimeEnvironment
    {
        public string BaseDirectory => baseDirectory;

        public string CurrentDirectory => currentDirectory;

        public bool FileExists(string path)
            => existingFiles.Contains(path, StringComparer.OrdinalIgnoreCase);
    }

    private static string TestPath(string name)
        => Path.GetFullPath(Path.Join(Path.GetTempPath(), "MoveMentorChessTests", name));
}
