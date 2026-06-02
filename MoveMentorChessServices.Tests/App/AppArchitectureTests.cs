using System.Text.RegularExpressions;
using Xunit;

namespace MoveMentorChessServices.Tests.App;

public sealed partial class AppArchitectureTests
{
    [Fact]
    public void P1LargeClassCleanupBoundariesDoNotRegress()
    {
        string root = FindRepositoryRoot();
        (string Path, int MaxLines)[] cleanupBudgets =
        [
            (Path.Join(root, "MoveMentorChess.App", "ViewModels", "OpeningTrainerWindowViewModel.cs"), 2500),
            (Path.Join(root, "MoveMentorChess.App", "Views", "AnalysisWindow.axaml.cs"), 550),
            (Path.Join(root, "MoveMentorChess.App", "Views", "ProfilesWindow.axaml.cs"), 820),
            (Path.Join(root, "MoveMentorChess.Training", "OpeningTrainerService.cs"), 500),
            (Path.Join(root, "MoveMentorChess.Profiles", "PlayerProfileService.cs"), 220)
        ];

        string[] oversizedFiles = cleanupBudgets
            .Where(file => File.ReadLines(file.Path).Count() > file.MaxLines)
            .Select(file => $"{Path.GetRelativePath(root, file.Path)} exceeds {file.MaxLines} lines")
            .ToArray();

        Assert.Empty(oversizedFiles);
    }

    [Fact]
    public void OpeningTrainerResultsStateStaysExtractedFromWindowViewModel()
    {
        string root = FindRepositoryRoot();
        string viewModelsRoot = Path.Join(root, "MoveMentorChess.App", "ViewModels");
        string windowViewModel = File.ReadAllText(Path.Join(viewModelsRoot, "OpeningTrainerWindowViewModel.cs"));

        Assert.True(
            File.Exists(Path.Join(viewModelsRoot, "OpeningTrainerResultsViewModel.cs")),
            "Opening trainer results state should stay in its extracted ViewModel.");
        Assert.Contains("OpeningTrainerResultsViewModel", windowViewModel, StringComparison.Ordinal);
        Assert.DoesNotContain("private TrainingResultLearningPlan? learningPlan", windowViewModel, StringComparison.Ordinal);
        Assert.DoesNotContain("private TrainingSessionOutcomeSummary? outcomeSummary", windowViewModel, StringComparison.Ordinal);
    }

    [Fact]
    public void PlayerProfileServiceStaysConcreteFacadeWithExtractedCollaborators()
    {
        string root = FindRepositoryRoot();
        string profilesRoot = Path.Join(root, "MoveMentorChess.Profiles");
        string service = File.ReadAllText(Path.Join(profilesRoot, "PlayerProfileService.cs"));

        bool declaresPartialPlayerProfileService = PartialPlayerProfileServiceRegex().IsMatch(service);
        Assert.False(declaresPartialPlayerProfileService, "PlayerProfileService must remain non-partial.");
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
            .Where(fileName => !File.Exists(Path.Join(profilesRoot, fileName)))
            .ToArray();

        Assert.Empty(missingCollaborators);
    }

    [Fact]
    public void AppViewsAndViewModelsDoNotAccessGlobalAnalysisStoreProvider()
    {
        string appRoot = Path.Join(FindRepositoryRoot(), "MoveMentorChess.App");
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
    public void AppViewModelsUseCachePortForGameAnalysisCache()
    {
        string viewModelsRoot = Path.Join(FindRepositoryRoot(), "MoveMentorChess.App", "ViewModels");
        string[] forbiddenFiles = Directory
            .EnumerateFiles(viewModelsRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => Path.GetFileName(path) != "GameAnalysisResultCacheAdapter.cs")
            .Where(path =>
            {
                string content = File.ReadAllText(path);
                return content.Contains("GameAnalysisCache.CreateKey", StringComparison.Ordinal)
                    || content.Contains("GameAnalysisCache.RemoveGame", StringComparison.Ordinal);
            })
            .Select(path => Path.GetRelativePath(viewModelsRoot, path))
            .Order()
            .ToArray();

        Assert.Empty(forbiddenFiles);
    }

    [Fact]
    public void GameAnalysisServiceUsesPlayerMistakeProfileSourcePort()
    {
        string servicePath = Path.Join(
            FindRepositoryRoot(),
            "MoveMentorChess.Analysis",
            "Services",
            "GameAnalysisService.cs");
        string service = File.ReadAllText(servicePath);

        Assert.Contains("IPlayerMistakeProfileSource", service, StringComparison.Ordinal);
        Assert.DoesNotContain("PlayerMistakeProfileProvider.TryBuild", service, StringComparison.Ordinal);
        Assert.DoesNotContain("AnalysisStoreProvider.GetStore", service, StringComparison.Ordinal);
    }

    [Fact]
    public void UseCaseProjectsDoNotReferencePersistence()
    {
        string root = FindRepositoryRoot();
        string[] useCaseProjects =
        [
            "MoveMentorChess.Analysis",
            "MoveMentorChess.Training",
            "MoveMentorChess.Profiles"
        ];

        string[] persistenceReferences = useCaseProjects
            .SelectMany(project => ReadProjectReferences(root, project)
                .Where(reference => string.Equals(reference, "MoveMentorChess.Persistence", StringComparison.Ordinal))
                .Select(_ => project))
            .Order()
            .ToArray();

        Assert.Empty(persistenceReferences);
    }

    [Fact]
    public void GlobalAnalysisStoreProviderAccessStaysInCompositionOrPersistenceAdapters()
    {
        string root = FindRepositoryRoot();
        string[] allowedFiles =
        [
            Path.Join("MoveMentorChess.App", "Composition", "AppCompositionRoot.cs")
        ];

        string[] sourceRoots = Directory
            .EnumerateDirectories(root, "MoveMentorChess.*", SearchOption.TopDirectoryOnly)
            .Where(path => !Path.GetFileName(path).Equals("MoveMentorChessServices.Tests", StringComparison.Ordinal))
            .ToArray();

        string[] forbiddenFiles = sourceRoots
            .SelectMany(path => Directory.EnumerateFiles(path, "*.cs", SearchOption.AllDirectories))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .Where(path => File.ReadAllText(path).Contains("AnalysisStoreProvider.GetStore", StringComparison.Ordinal))
            .Select(path => Path.GetRelativePath(root, path))
            .Where(path => !path.StartsWith($"MoveMentorChess.Persistence{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                && !path.StartsWith($"MoveMentorChess.Diagnostics{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                && !allowedFiles.Contains(path, StringComparer.Ordinal))
            .Order()
            .ToArray();

        Assert.Empty(forbiddenFiles);
    }

    [Fact]
    public void StockfishPathResolverUsesRuntimeEnvironmentPort()
    {
        string resolverPath = Path.Join(
            FindRepositoryRoot(),
            "MoveMentorChess.App",
            "Composition",
            "DefaultStockfishPathResolver.cs");
        string resolver = File.ReadAllText(resolverPath);

        Assert.DoesNotContain("AppContext.BaseDirectory", resolver, StringComparison.Ordinal);
        Assert.DoesNotContain("Environment.CurrentDirectory", resolver, StringComparison.Ordinal);
        Assert.DoesNotContain("File.Exists", resolver, StringComparison.Ordinal);
    }

    [Fact]
    public void LlamaRuntimeResolversUseSharedPathCandidates()
    {
        string analysisRoot = Path.Join(FindRepositoryRoot(), "MoveMentorChess.Analysis");
        string[] resolverFiles =
        [
            "LlamaCppAdviceRuntimeResolver.cs",
            "LlamaCppServerResolver.cs"
        ];

        string[] directRuntimeDirectoryReads = resolverFiles
            .Where(fileName =>
            {
                string resolver = File.ReadAllText(Path.Join(analysisRoot, "Services", fileName));
                return resolver.Contains("AppContext.BaseDirectory", StringComparison.Ordinal)
                    || resolver.Contains("Directory.GetCurrentDirectory", StringComparison.Ordinal);
            })
            .ToArray();

        Assert.Empty(directRuntimeDirectoryReads);
    }

    [Fact]
    public void P3ProjectReferenceBoundariesDoNotRegress()
    {
        string root = FindRepositoryRoot();
        string[] presentationReferences = ReadProjectReferences(root, "MoveMentorChess.Presentation");
        string[] trainingReferences = ReadProjectReferences(root, "MoveMentorChess.Training");
        string[] profilesReferences = ReadProjectReferences(root, "MoveMentorChess.Profiles");

        string[] forbiddenPresentationReferences =
        [
            "MoveMentorChess.App",
            "MoveMentorChess.Persistence",
            "MoveMentorChess.Training",
            "MoveMentorChess.Tracking"
        ];

        string[] presentationBoundaryLeaks = presentationReferences
            .Intersect(forbiddenPresentationReferences, StringComparer.Ordinal)
            .Order()
            .ToArray();

        Assert.Empty(presentationBoundaryLeaks);
        Assert.True(trainingReferences.Length <= 3, "Training should not grow beyond its current P3 project reference budget.");
        Assert.True(profilesReferences.Length <= 5, "Profiles should not grow beyond its current P3 project reference budget.");
        Assert.Contains("MoveMentorChess.Domain", presentationReferences);
    }

    [Fact]
    public void SqliteAnalysisStoreFacadeDoesNotOwnSqlStatements()
    {
        string facadePath = Path.Join(FindRepositoryRoot(), "MoveMentorChess.Persistence", "SqliteAnalysisStore.cs");
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
            if (File.Exists(Path.Join(directory.FullName, "MoveMentorChess.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not find repository root.");
    }

    private static string[] ReadProjectReferences(string root, string projectName)
    {
        string projectFile = Path.Join(root, projectName, $"{projectName}.csproj");
        string projectText = File.ReadAllText(projectFile);

        return ProjectReferenceRegex()
            .Matches(projectText)
            .Select(match => match.Groups["project"].Value)
            .Order()
            .ToArray();
    }

    [GeneratedRegex(@"<ProjectReference\s+Include=""[^""]*(?<project>MoveMentorChess(?:\.[^""\\]+)+)\.csproj""", RegexOptions.CultureInvariant)]
    private static partial Regex ProjectReferenceRegex();

    [GeneratedRegex(@"\bpartial\s+class\s+PlayerProfileService\b", RegexOptions.CultureInvariant)]
    private static partial Regex PartialPlayerProfileServiceRegex();
}
