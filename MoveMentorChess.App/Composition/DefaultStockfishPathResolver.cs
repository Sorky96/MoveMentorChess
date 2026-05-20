using System.IO;

namespace MoveMentorChess.App.Composition;

public sealed class DefaultStockfishPathResolver : IStockfishPathResolver
{
    public string? Resolve()
    {
        string baseDirectory = AppContext.BaseDirectory;
        string[] candidates =
        [
            Path.Combine(baseDirectory, "stockfish.exe"),
            Path.Combine(baseDirectory, "..", "..", "..", "..", "MoveMentorChessServices", "bin", "Debug", "net8.0-windows", "stockfish.exe"),
            Path.Combine(baseDirectory, "..", "..", "..", "..", "..", "MoveMentorChessServices", "bin", "Debug", "net8.0-windows", "stockfish.exe"),
            Path.Combine(Environment.CurrentDirectory, "MoveMentorChessServices", "bin", "Debug", "net8.0-windows", "stockfish.exe"),
            Path.Combine(Environment.CurrentDirectory, "stockfish.exe")
        ];

        foreach (string candidate in candidates)
        {
            string fullPath;
            try
            {
                fullPath = Path.GetFullPath(candidate);
            }
            catch (Exception ex) when (ex is ArgumentException or System.Security.SecurityException or NotSupportedException or PathTooLongException)
            {
                continue;
            }

            if (File.Exists(fullPath))
            {
                return fullPath;
            }
        }

        return null;
    }
}
