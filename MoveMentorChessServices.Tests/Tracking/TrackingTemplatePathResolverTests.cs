using System;
using System.Collections.Generic;
using System.IO;
using MoveMentorChess.Tracking;
using Xunit;

namespace MoveMentorChessServices.Tests.Tracking;

public sealed class TrackingTemplatePathResolverTests
{
    [Fact]
    public void Resolve_ReturnsFirstExistingTemplateCandidate()
    {
        string root = Path.Join(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        string baseDirectory = Path.Join(root, "src", "MoveMentorChess.App", "bin", "Debug", "net8.0-windows");
        string fileName = "ChessComReference_d4.png";
        string expected = Path.Join(root, "src", "MoveMentorChessServices", "TrackingTemplates", fileName);
        List<string> checkedPaths = [];

        DefaultTrackingTemplatePathResolver resolver = new(
            () => baseDirectory,
            path =>
            {
                checkedPaths.Add(path);
                return string.Equals(path, expected, StringComparison.Ordinal);
            });

        string? resolved = resolver.Resolve(fileName);

        Assert.Equal(expected, resolved);
        Assert.Contains(Path.Join(baseDirectory, "TrackingTemplates", fileName), checkedPaths);
        Assert.Contains(Path.Join(root, "src", "MoveMentorChessServices", "TrackingTemplates", fileName), checkedPaths);
    }

    [Fact]
    public void Resolve_ReturnsNullWhenTemplateIsMissing()
    {
        string baseDirectory = Path.Join(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "bin");
        DefaultTrackingTemplatePathResolver resolver = new(() => baseDirectory, _ => false);

        string? resolved = resolver.Resolve("missing.png");

        Assert.Null(resolved);
    }
}
