namespace MoveMentorChess.Tracking;

public interface ITesseractDataPathResolver
{
    bool TryGetReadyDataPath(string? configuredDataPath, out string dataPath, out string? error);
}
