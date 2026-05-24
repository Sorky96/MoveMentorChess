using System;
using System.Drawing;
using System.IO;

namespace MoveMentorChess.Tracking;

public sealed class DirectoryTrackingPieceImageRepository : ITrackingPieceImageRepository
{
    private readonly string? imagesDirectory;
    private readonly Func<string, bool> directoryExists;
    private readonly Func<string, bool> fileExists;
    private readonly Func<string, Image> loadImage;

    public DirectoryTrackingPieceImageRepository(string? imagesDirectory)
        : this(imagesDirectory, Directory.Exists, File.Exists, Image.FromFile)
    {
    }

    public DirectoryTrackingPieceImageRepository(
        string? imagesDirectory,
        Func<string, bool> directoryExists,
        Func<string, bool> fileExists,
        Func<string, Image> loadImage)
    {
        this.imagesDirectory = imagesDirectory;
        this.directoryExists = directoryExists ?? throw new ArgumentNullException(nameof(directoryExists));
        this.fileExists = fileExists ?? throw new ArgumentNullException(nameof(fileExists));
        this.loadImage = loadImage ?? throw new ArgumentNullException(nameof(loadImage));
    }

    public bool IsAvailable => !string.IsNullOrWhiteSpace(imagesDirectory) && directoryExists(imagesDirectory);

    public bool TryLoadPieceImage(string fileName, out Image? image, out string? path)
    {
        image = null;
        path = null;

        if (!IsAvailable)
        {
            return false;
        }

        path = Path.Join(imagesDirectory, fileName);
        if (!fileExists(path))
        {
            return false;
        }

        image = loadImage(path);
        return true;
    }
}
