using Xunit;

namespace MoveMentorChessServices.Tests.App;

public sealed class AppArchitectureTests
{
    [Fact]
    public void AppViewsAndViewModelsDoNotAccessGlobalAnalysisStoreProvider()
    {
        string appRoot = Path.Combine(FindRepositoryRoot(), "MoveMentorChess.App");
        string[] forbiddenFiles = Directory
            .EnumerateFiles(appRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path =>
                path.Contains($"{Path.DirectorySeparatorChar}Views{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                || path.Contains($"{Path.DirectorySeparatorChar}ViewModels{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(path => File.ReadAllText(path).Contains("AnalysisStoreProvider.GetStore", StringComparison.Ordinal))
            .Select(path => Path.GetRelativePath(appRoot, path))
            .Order()
            .ToArray();

        Assert.Empty(forbiddenFiles);
    }

    [Fact]
    public void SqliteAnalysisStoreFacadeDoesNotOwnSqlStatements()
    {
        string facadePath = Path.Combine(FindRepositoryRoot(), "MoveMentorChess.Persistence", "SqliteAnalysisStore.cs");
        string facade = File.ReadAllText(facadePath);

        Assert.DoesNotContain("CREATE TABLE", facade, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("INSERT INTO", facade, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("UPDATE ", facade, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DELETE FROM", facade, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SELECT ", facade, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("sqlite3_prepare", facade, StringComparison.OrdinalIgnoreCase);
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "MoveMentorChess.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not find repository root.");
    }
}
