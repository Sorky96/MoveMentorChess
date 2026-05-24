using System;
using System.IO;

namespace MoveMentorChess.Tracking;

public sealed class DefaultTesseractDataPathResolver : ITesseractDataPathResolver
{
    private const string EnglishDataFileName = "eng.traineddata";
    private readonly Func<string> baseDirectoryProvider;
    private readonly Func<string, bool> fileExists;

    public DefaultTesseractDataPathResolver()
        : this(() => AppContext.BaseDirectory, File.Exists)
    {
    }

    public DefaultTesseractDataPathResolver(Func<string> baseDirectoryProvider, Func<string, bool> fileExists)
    {
        this.baseDirectoryProvider = baseDirectoryProvider ?? throw new ArgumentNullException(nameof(baseDirectoryProvider));
        this.fileExists = fileExists ?? throw new ArgumentNullException(nameof(fileExists));
    }

    public bool TryGetReadyDataPath(string? configuredDataPath, out string dataPath, out string? error)
    {
        dataPath = ResolveExpectedDataPath(configuredDataPath);
        string englishDataFile = Path.Join(dataPath, EnglishDataFileName);

        if (fileExists(englishDataFile))
        {
            error = null;
            return true;
        }

        error = $"Missing OCR language data: '{englishDataFile}'. Download 'eng.traineddata' from the official Tesseract tessdata repository and place it in a 'tessdata' folder next to the app.";
        return false;
    }

    private string ResolveExpectedDataPath(string? configuredDataPath)
    {
        if (!string.IsNullOrWhiteSpace(configuredDataPath))
        {
            return configuredDataPath;
        }

        return Path.Join(baseDirectoryProvider(), "tessdata");
    }
}
