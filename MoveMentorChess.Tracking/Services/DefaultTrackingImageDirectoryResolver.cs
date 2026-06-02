using System;
using System.IO;

namespace MoveMentorChess.Tracking;

public sealed class DefaultTrackingImageDirectoryResolver : ITrackingImageDirectoryResolver
{
    private const string ProbePieceFileName = "wK.png";
    private readonly Func<string> baseDirectoryProvider;
    private readonly Func<string, bool> directoryExists;
    private readonly Func<string, bool> fileExists;

    public DefaultTrackingImageDirectoryResolver()
        : this(() => AppContext.BaseDirectory, Directory.Exists, File.Exists)
    {
    }

    public DefaultTrackingImageDirectoryResolver(
        Func<string> baseDirectoryProvider,
        Func<string, bool> directoryExists,
        Func<string, bool> fileExists)
    {
        this.baseDirectoryProvider = baseDirectoryProvider ?? throw new ArgumentNullException(nameof(baseDirectoryProvider));
        this.directoryExists = directoryExists ?? throw new ArgumentNullException(nameof(directoryExists));
        this.fileExists = fileExists ?? throw new ArgumentNullException(nameof(fileExists));
    }

    public string? Resolve()
    {
        DirectoryInfo? current = new(baseDirectoryProvider());
        while (current is not null)
        {
            string projectCandidate = Path.Join(current.FullName, "MoveMentorChessServices", "Images");
            if (HasPieceAssets(projectCandidate))
            {
                return projectCandidate;
            }

            string directCandidate = Path.Join(current.FullName, "Images");
            if (HasPieceAssets(directCandidate))
            {
                return directCandidate;
            }

            current = current.Parent;
        }

        return null;
    }

    private bool HasPieceAssets(string path)
    {
        return directoryExists(path) && fileExists(Path.Join(path, ProbePieceFileName));
    }
}
