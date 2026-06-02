using System;
using System.Collections.Generic;
using System.IO;
using MoveMentorChess.Tracking;
using Xunit;

namespace MoveMentorChessServices.Tests;

public sealed class TrackingImageDirectoryResolverTests
{
    [Fact]
    public void Resolve_ReturnsFirstDirectoryWithPieceAssets()
    {
        string root = Path.Join(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        string baseDirectory = Path.Join(root, "src", "MoveMentorChess.App", "bin", "Debug", "net8.0-windows");
        string expected = Path.Join(root, "src", "MoveMentorChessServices", "Images");
        List<string> checkedFiles = [];

        DefaultTrackingImageDirectoryResolver resolver = new(
            () => baseDirectory,
            _ => true,
            path =>
            {
                checkedFiles.Add(path);
                return string.Equals(path, Path.Join(expected, "wK.png"), StringComparison.Ordinal);
            });

        string? resolved = resolver.Resolve();

        Assert.Equal(expected, resolved);
        Assert.Contains(Path.Join(root, "src", "MoveMentorChessServices", "Images", "wK.png"), checkedFiles);
    }

    [Fact]
    public void Resolve_FallsBackToDirectImagesDirectory()
    {
        string root = Path.Join(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        string baseDirectory = Path.Join(root, "app");
        string expected = Path.Join(baseDirectory, "Images");

        DefaultTrackingImageDirectoryResolver resolver = new(
            () => baseDirectory,
            _ => true,
            path => string.Equals(path, Path.Join(expected, "wK.png"), StringComparison.Ordinal));

        string? resolved = resolver.Resolve();

        Assert.Equal(expected, resolved);
    }

    [Fact]
    public void Resolve_ReturnsNullWhenPieceAssetsAreMissing()
    {
        string baseDirectory = Path.Join(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "bin");
        DefaultTrackingImageDirectoryResolver resolver = new(() => baseDirectory, _ => true, _ => false);

        string? resolved = resolver.Resolve();

        Assert.Null(resolved);
    }
}
