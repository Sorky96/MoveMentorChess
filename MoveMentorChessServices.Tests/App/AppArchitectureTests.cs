using Xunit;

namespace MoveMentorChessServices.Tests.App;

public sealed class AppArchitectureTests
{
    [Fact]
    public void P1LargeClassCleanupBoundariesDoNotRegress()
    {
        string root = FindRepositoryRoot();
        (string Path, int MaxLines)[] cleanupBudgets =
        [
            (Path.Combine(root, "MoveMentorChess.App", "ViewModels", "OpeningTrainerWindowViewModel.cs"), 2600),
            (Path.Combine(root, "MoveMentorChess.App", "Views", "AnalysisWindow.axaml.cs"), 550),
            (Path.Combine(root, "MoveMentorChess.App", "Views", "ProfilesWindow.axaml.cs"), 820),
            (Path.Combine(root, "MoveMentorChess.Training", "OpeningTrainerService.cs"), 500),
            (Path.Combine(root, "MoveMentorChess.Profiles", "PlayerProfileService.cs"), 220)
        ];

        string[] oversizedFiles = cleanupBudgets
            .Where(file => File.ReadLines(file.Path).Count() > file.MaxLines)
            .Select(file => $"{Path.GetRelativePath(root, file.Path)} exceeds {file.MaxLines} lines")
            .ToArray();

        Assert.Empty(oversizedFiles);
    }

    [Fact]
    public void PlayerProfileServiceStaysConcreteFacadeWithExtractedCollaborators()
    {
        string root = FindRepositoryRoot();
        string profilesRoot = Path.Combine(root, "MoveMentorChess.Profiles");
        string service = File.ReadAllText(Path.Combine(profilesRoot, "PlayerProfileService.cs"));

        Assert.DoesNotContain("partial class PlayerProfileService", service, StringComparison.Ordinal);
        Assert.Contains("PlayerProfileSnapshotLoader", service, StringComparison.Ordinal);
        Assert.Contains("PlayerProfileReportBuilder", service, StringComparison.Ordinal);

        string[] requiredCollaborators =
        [
            "PlayerProfileSnapshotLoader.cs",
            "PlayerProfileStatsAggregator.cs",
            "PlayerProfileProgressAnalyzer.cs",
            "PlayerRatingTrendAnalyzer.cs",
            "PlayerProfileMistakeExampleBuilder.cs",
            "PlayerProfileReportBuilder.cs"
        ];

        string[] missingCollaborators = requiredCollaborators
            .Where(fileName => !File.Exists(Path.Combine(profilesRoot, fileName)))
            .ToArray();

        Assert.Empty(missingCollaborators);
    }

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
