using System;
using System.IO;

namespace MoveMentorChess.Tracking;

public sealed class DefaultTrackingTemplatePathResolver : ITrackingTemplatePathResolver
{
    private readonly Func<string> baseDirectoryProvider;
    private readonly Func<string, bool> fileExists;

    public DefaultTrackingTemplatePathResolver()
        : this(() => AppContext.BaseDirectory, File.Exists)
    {
    }

    public DefaultTrackingTemplatePathResolver(Func<string> baseDirectoryProvider, Func<string, bool> fileExists)
    {
        this.baseDirectoryProvider = baseDirectoryProvider ?? throw new ArgumentNullException(nameof(baseDirectoryProvider));
        this.fileExists = fileExists ?? throw new ArgumentNullException(nameof(fileExists));
    }

    public string? Resolve(string fileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        DirectoryInfo? current = new(baseDirectoryProvider());
        while (current is not null)
        {
            string directCandidate = Path.Join(current.FullName, "TrackingTemplates", fileName);
            if (fileExists(directCandidate))
            {
                return directCandidate;
            }

            string projectCandidate = Path.Join(current.FullName, "MoveMentorChessServices", "TrackingTemplates", fileName);
            if (fileExists(projectCandidate))
            {
                return projectCandidate;
            }

            current = current.Parent;
        }

        return null;
    }
}
