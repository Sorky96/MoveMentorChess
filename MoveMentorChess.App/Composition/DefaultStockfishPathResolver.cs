using System.IO;

namespace MoveMentorChess.App.Composition;

public sealed class DefaultStockfishPathResolver : IStockfishPathResolver
{
    private readonly IAppRuntimeEnvironment environment;

    public DefaultStockfishPathResolver()
        : this(SystemAppRuntimeEnvironment.Instance)
    {
    }

    internal DefaultStockfishPathResolver(IAppRuntimeEnvironment environment)
    {
        this.environment = environment ?? throw new ArgumentNullException(nameof(environment));
    }

    public string? Resolve()
    {
        string baseDirectory = environment.BaseDirectory;
        string currentDirectory = environment.CurrentDirectory;
        string[] candidates =
        [
            Path.Combine(baseDirectory, "stockfish.exe"),
            Path.Combine(baseDirectory, "..", "..", "..", "..", "MoveMentorChessServices", "bin", "Debug", "net8.0-windows", "stockfish.exe"),
            Path.Combine(baseDirectory, "..", "..", "..", "..", "..", "MoveMentorChessServices", "bin", "Debug", "net8.0-windows", "stockfish.exe"),
            Path.Combine(currentDirectory, "MoveMentorChessServices", "bin", "Debug", "net8.0-windows", "stockfish.exe"),
            Path.Combine(currentDirectory, "stockfish.exe")
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

            if (environment.FileExists(fullPath))
            {
                return fullPath;
            }
        }

        return null;
    }
}
