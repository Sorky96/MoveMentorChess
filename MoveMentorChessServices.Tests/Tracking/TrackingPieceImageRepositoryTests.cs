using System;
using System.Drawing;
using System.IO;
using MoveMentorChess.Tracking;
using Xunit;

namespace MoveMentorChessServices.Tests.Tracking;

public sealed class TrackingPieceImageRepositoryTests
{
    [Fact]
    public void IsAvailable_ReturnsTrueWhenImagesDirectoryExists()
    {
        string imagesDirectory = Path.Join(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "Images");
        DirectoryTrackingPieceImageRepository repository = new(
            imagesDirectory,
            path => string.Equals(path, imagesDirectory, StringComparison.Ordinal),
            _ => false,
            _ => new Bitmap(1, 1));

        Assert.True(repository.IsAvailable);
    }

    [Fact]
    public void TryLoadPieceImage_ReturnsFalseWhenPieceFileIsMissing()
    {
        DirectoryTrackingPieceImageRepository repository = new(
            Path.Join(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "Images"),
            _ => true,
            _ => false,
            _ => throw new InvalidOperationException("Loader should not run for missing files."));

        bool loaded = repository.TryLoadPieceImage("wK.png", out Image? image, out string? path);

        Assert.False(loaded);
        Assert.Null(image);
        Assert.EndsWith(Path.Join("Images", "wK.png"), path);
    }

    [Fact]
    public void TryLoadPieceImage_LoadsExpectedPiecePath()
    {
        string imagesDirectory = Path.Join(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "Images");
        string expectedPath = Path.Join(imagesDirectory, "wK.png");
        string? loadedPath = null;
        DirectoryTrackingPieceImageRepository repository = new(
            imagesDirectory,
            _ => true,
            path => string.Equals(path, expectedPath, StringComparison.Ordinal),
            path =>
            {
                loadedPath = path;
                return new Bitmap(1, 1);
            });

        bool loaded = repository.TryLoadPieceImage("wK.png", out Image? image, out string? path);

        using (image)
        {
            Assert.True(loaded);
            Assert.NotNull(image);
            Assert.Equal(expectedPath, path);
            Assert.Equal(expectedPath, loadedPath);
        }
    }
}
