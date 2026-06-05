using System;
using System.IO;
using MoveMentorChess.Tracking;
using Xunit;

namespace MoveMentorChessServices.Tests.Tracking;

public sealed class TesseractDataPathResolverTests
{
    [Fact]
    public void TryGetReadyDataPath_UsesConfiguredPathFirst()
    {
        string configuredPath = Path.Join(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "custom-tessdata");
        DefaultTesseractDataPathResolver resolver = new(
            () => Path.Join(Path.GetTempPath(), "app"),
            path => string.Equals(path, Path.Join(configuredPath, "eng.traineddata"), StringComparison.Ordinal));

        bool resolved = resolver.TryGetReadyDataPath(configuredPath, out string dataPath, out string? error);

        Assert.True(resolved);
        Assert.Equal(configuredPath, dataPath);
        Assert.Null(error);
    }

    [Fact]
    public void TryGetReadyDataPath_FallsBackToAppTessdataDirectory()
    {
        string baseDirectory = Path.Join(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "app");
        string expectedPath = Path.Join(baseDirectory, "tessdata");
        DefaultTesseractDataPathResolver resolver = new(
            () => baseDirectory,
            path => string.Equals(path, Path.Join(expectedPath, "eng.traineddata"), StringComparison.Ordinal));

        bool resolved = resolver.TryGetReadyDataPath(null, out string dataPath, out string? error);

        Assert.True(resolved);
        Assert.Equal(expectedPath, dataPath);
        Assert.Null(error);
    }

    [Fact]
    public void TryGetReadyDataPath_ReturnsErrorWhenEnglishDataIsMissing()
    {
        string baseDirectory = Path.Join(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "app");
        DefaultTesseractDataPathResolver resolver = new(() => baseDirectory, _ => false);

        bool resolved = resolver.TryGetReadyDataPath("", out string dataPath, out string? error);

        Assert.False(resolved);
        Assert.Equal(Path.Join(baseDirectory, "tessdata"), dataPath);
        Assert.Contains("eng.traineddata", error, StringComparison.Ordinal);
    }
}
