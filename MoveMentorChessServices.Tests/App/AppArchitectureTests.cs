using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using MoveMentorChess.Presentation.Models;
using Xunit;

namespace MoveMentorChessServices.Tests.App;

public sealed partial class AppArchitectureTests
{
    private static readonly HashSet<string> VisibleXamlTextAttributeNames = new(StringComparer.Ordinal)
    {
        "Title",
        "Content",
        "Header",
        "Text",
        "PlaceholderText",
        "ToolTip.Tip"
    };

    [Fact]
    public void P1LargeClassCleanupBoundariesDoNotRegress()
    {
        string root = FindRepositoryRoot();
        (string Path, int MaxLines)[] cleanupBudgets =
        [
            (Path.Join(root, "MoveMentorChess.App", "ViewModels", "OpeningTrainerWindowViewModel.cs"), 2275),
            (Path.Join(root, "MoveMentorChess.App", "ViewModels", "MainWindowViewModel.cs"), 1540),
            (Path.Join(root, "MoveMentorChess.App", "Views", "AnalysisWindow.axaml.cs"), 540),
            (Path.Join(root, "MoveMentorChess.App", "Views", "ProfilesWindow.axaml.cs"), 790),
            (Path.Join(root, "MoveMentorChess.Training", "OpeningTrainerService.cs"), 470),
            (Path.Join(root, "MoveMentorChess.Profiles", "PlayerProfileService.cs"), 190)
        ];

        string[] oversizedFiles = cleanupBudgets
            .Where(file => File.ReadLines(file.Path).Count() > file.MaxLines)
            .Select(file => $"{Path.GetRelativePath(root, file.Path)} exceeds {file.MaxLines} lines")
            .ToArray();

        Assert.Empty(oversizedFiles);
    }

    [Fact]
    public void LegacyMoveMentorChessServicesProjectShellStaysRemoved()
    {
        string root = FindRepositoryRoot();
        string[] obsoleteShellFiles =
        [
            Path.Join(root, "MoveMentorChessServices", "MoveMentorChessServices.csproj"),
            Path.Join(root, "MoveMentorChessServices", "MoveMentorChessServices.ico")
        ];

        string[] existingShellFiles = obsoleteShellFiles
            .Where(File.Exists)
            .Select(path => Path.GetRelativePath(root, path))
            .Order()
            .ToArray();

        Assert.Empty(existingShellFiles);

        string solution = File.ReadAllText(Path.Join(root, "MoveMentorChess.sln"));
        Assert.DoesNotContain(
            @"MoveMentorChessServices\MoveMentorChessServices.csproj",
            solution,
            StringComparison.Ordinal);
    }

    [Fact]
    public void ProductionCSharpFilesExposeOnlyOnePublicTopLevelTypeUnlessAllowListed()
    {
        string root = FindRepositoryRoot();
        Dictionary<string, PublicTopLevelTypeAllowListEntry> allowList = PublicTopLevelTypeAllowList();

        string[] undocumentedAllowListEntries = allowList.Values
            .Where(entry => string.IsNullOrWhiteSpace(entry.Owner)
                || string.IsNullOrWhiteSpace(entry.FollowUp)
                || !entry.FollowUp.Contains("Sprint ", StringComparison.Ordinal))
            .Select(entry => $"{entry.RelativePath} must name an owner and follow-up sprint.")
            .Order()
            .ToArray();
        Assert.Empty(undocumentedAllowListEntries);

        Dictionary<string, string[]> violations = EnumerateProductionCSharpFiles(root)
            .Select(path => (
                RelativePath: NormalizeRelativePath(Path.GetRelativePath(root, path)),
                PublicTypes: FindPublicTopLevelTypeNames(File.ReadAllText(path))))
            .Where(file => file.PublicTypes.Length > 1)
            .ToDictionary(file => file.RelativePath, file => file.PublicTypes, StringComparer.Ordinal);

        string[] unexpectedViolations = violations
            .Where(file => !allowList.ContainsKey(file.Key))
            .Select(file => $"{file.Key}: {string.Join(", ", file.Value)}")
            .Order()
            .ToArray();
        Assert.Empty(unexpectedViolations);

        string[] changedAllowListEntries = violations
            .Where(file => allowList.TryGetValue(file.Key, out PublicTopLevelTypeAllowListEntry? entry)
                && !file.Value
                    .OrderBy(type => type, StringComparer.Ordinal)
                    .SequenceEqual(entry.PublicTypes.OrderBy(type => type, StringComparer.Ordinal), StringComparer.Ordinal))
            .Select(file =>
            {
                PublicTopLevelTypeAllowListEntry entry = allowList[file.Key];
                return $"{file.Key}: expected [{string.Join(", ", entry.PublicTypes)}], found [{string.Join(", ", file.Value)}]";
            })
            .Order()
            .ToArray();
        Assert.Empty(changedAllowListEntries);

        string[] staleAllowListEntries = allowList.Keys
            .Where(path => !violations.ContainsKey(path))
            .Select(path => $"{path} no longer needs a public top-level type allow-list entry.")
            .Order()
            .ToArray();
        Assert.Empty(staleAllowListEntries);
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
    public void OpeningTrainerSelectionStateStaysExtractedFromWindowViewModel()
    {
        string root = FindRepositoryRoot();
        string viewModelsRoot = Path.Join(root, "MoveMentorChess.App", "ViewModels");
        string windowViewModel = File.ReadAllText(Path.Join(viewModelsRoot, "OpeningTrainerWindowViewModel.cs"));

        Assert.True(
            File.Exists(Path.Join(viewModelsRoot, "OpeningTrainerSelectionViewModel.cs")),
            "Opening trainer selection and recommendation state should stay in its extracted ViewModel.");
        Assert.Contains("OpeningTrainerSelectionViewModel", windowViewModel, StringComparison.Ordinal);
        Assert.DoesNotContain("private TrainingRecommendationCard? todayRecommendation", windowViewModel, StringComparison.Ordinal);
        Assert.DoesNotContain("private OpeningTrainingProfileChoice? selectedProfileChoice", windowViewModel, StringComparison.Ordinal);
        Assert.DoesNotContain("private OpeningTrainingIntensityChoice? selectedIntensityChoice", windowViewModel, StringComparison.Ordinal);
        Assert.DoesNotContain("private PlayerOpeningPlan? playerOpeningPlan", windowViewModel, StringComparison.Ordinal);
        Assert.DoesNotContain("private SpecialTrainingModeDefinition? selectedSpecialMode", windowViewModel, StringComparison.Ordinal);
    }

    [Fact]
    public void MainWindowImportReplayStateStaysExtractedFromWindowViewModel()
    {
        string root = FindRepositoryRoot();
        string viewModelsRoot = Path.Join(root, "MoveMentorChess.App", "ViewModels");
        string windowViewModel = File.ReadAllText(Path.Join(viewModelsRoot, "MainWindowViewModel.cs"));

        Assert.True(
            File.Exists(Path.Join(viewModelsRoot, "ImportedGameReplayController.cs")),
            "Main window imported game, replay cursor, and imported move projection should stay in the extracted controller.");
        Assert.Contains("ImportedGameReplayController", windowViewModel, StringComparison.Ordinal);
        Assert.DoesNotContain("private ImportedGame? importedGame", windowViewModel, StringComparison.Ordinal);
        Assert.DoesNotContain("private IReadOnlyList<ReplayPly> importedReplay", windowViewModel, StringComparison.Ordinal);
        Assert.DoesNotContain("private int importedCursor", windowViewModel, StringComparison.Ordinal);
        Assert.DoesNotContain("new GameReplayService().Replay", windowViewModel, StringComparison.Ordinal);
        Assert.DoesNotContain("TryLoadFirstReplayableImportedGame", windowViewModel, StringComparison.Ordinal);
    }

    [Fact]
    public void SettingsWindowUsesWorkflowForPersistenceAndRuntimeSideEffects()
    {
        string root = FindRepositoryRoot();
        string settingsWindow = File.ReadAllText(Path.Join(root, "MoveMentorChess.App", "Views", "SettingsWindow.axaml.cs"));

        Assert.Contains("ISettingsWorkflow", settingsWindow, StringComparison.Ordinal);
        Assert.DoesNotContain("ApplicationSettingsStore", settingsWindow, StringComparison.Ordinal);
        Assert.DoesNotContain("LlamaGpuSettingsStore", settingsWindow, StringComparison.Ordinal);
        Assert.DoesNotContain("StockfishSettingsStore", settingsWindow, StringComparison.Ordinal);
        Assert.DoesNotContain("LlamaCppServerManager", settingsWindow, StringComparison.Ordinal);
    }

    [Fact]
    public void VisibleXamlTextUsesLocalizationOrDocumentedAllowList()
    {
        string root = FindRepositoryRoot();
        Dictionary<string, string> allowList = VisibleXamlTextAllowList();

        string[] undocumentedAllowListEntries = allowList
            .Where(entry => string.IsNullOrWhiteSpace(entry.Value)
                || !entry.Value.Contains("Sprint ", StringComparison.Ordinal))
            .Select(entry => $"{entry.Key} must include an allow-list reason with a follow-up sprint.")
            .Order()
            .ToArray();
        Assert.Empty(undocumentedAllowListEntries);

        VisibleXamlTextLiteral[] literals = EnumerateAppXamlFiles(root)
            .SelectMany(path => FindVisibleXamlTextLiterals(root, path))
            .ToArray();
        HashSet<string> literalSites = literals
            .Select(literal => literal.Site)
            .ToHashSet(StringComparer.Ordinal);

        string[] unexpectedLiterals = literals
            .Where(literal => !allowList.ContainsKey(literal.Site))
            .Select(literal => $"{literal.Site} should use loc:Localize, a binding, or a documented allow-list entry.")
            .Order()
            .ToArray();
        Assert.Empty(unexpectedLiterals);

        string[] staleAllowListEntries = allowList.Keys
            .Where(site => !literalSites.Contains(site))
            .Select(site => $"{site} no longer needs a visible XAML text allow-list entry.")
            .Order()
            .ToArray();
        Assert.Empty(staleAllowListEntries);
    }

    [Fact]
    public void AppStartupAndExitRuntimeEffectsStayInCompositionRoot()
    {
        string root = FindRepositoryRoot();
        string app = File.ReadAllText(Path.Join(root, "MoveMentorChess.App", "App.axaml.cs"));
        string compositionRoot = File.ReadAllText(Path.Join(root, "MoveMentorChess.App", "Composition", "AppCompositionRoot.cs"));

        Assert.Contains("AppCompositionRoot.ConfigureDesktopApplication", app, StringComparison.Ordinal);
        Assert.DoesNotContain("ApplicationSettingsStore", app, StringComparison.Ordinal);
        Assert.DoesNotContain("LlamaCppProcessCleaner", app, StringComparison.Ordinal);
        Assert.DoesNotContain("LlamaCppServerManager", app, StringComparison.Ordinal);
        Assert.Contains("ISettingsWorkflow", compositionRoot, StringComparison.Ordinal);
        Assert.Contains("IAppRuntimeLifecycle", compositionRoot, StringComparison.Ordinal);
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
    public void ProfileCoachRendererStaysOutsideViewModels()
    {
        string root = FindRepositoryRoot();
        string rendererPath = Path.Join(root, "MoveMentorChess.App", "Renderers", "ProfileCoachSectionRenderer.cs");
        string formerViewModelPath = Path.Join(root, "MoveMentorChess.App", "ViewModels", "ProfileCoachSectionRenderer.cs");

        Assert.True(File.Exists(rendererPath), "Profile coach Avalonia rendering should live under App/Renderers.");
        Assert.False(File.Exists(formerViewModelPath), "Profile coach Avalonia rendering should not live under ViewModels.");

        string renderer = File.ReadAllText(rendererPath);
        Assert.Contains("namespace MoveMentorChess.App.Renderers", renderer, StringComparison.Ordinal);
    }

    [Fact]
    public void ProfileTrendChartPresentationUsesColorTokensInsteadOfAvaloniaBrushes()
    {
        string root = FindRepositoryRoot();
        string modelPath = Path.Join(root, "MoveMentorChess.Presentation", "Models", "ProfileTrendChartPresentation.cs");
        string model = File.ReadAllText(modelPath);

        Assert.NotNull(typeof(ProfileTrendChartSeries).GetProperty(nameof(ProfileTrendChartSeries.StrokeHex)));
        Assert.DoesNotContain("Avalonia", model, StringComparison.Ordinal);
    }

    [Fact]
    public void TrainingPlanServiceKeepsScoringAndNarrationExtracted()
    {
        string root = FindRepositoryRoot();
        string trainingRoot = Path.Join(root, "MoveMentorChess.Training");
        string service = File.ReadAllText(Path.Join(trainingRoot, "TrainingPlanService.cs"));

        string[] requiredCollaborators =
        [
            "OpeningTrainingOutcomeSummarizer.cs",
            "TrainingPlanOpeningWeaknessSelector.cs",
            "TrainingPlanTopicScorer.cs",
            "TrainingPlanTopicNarrativeBuilder.cs"
        ];

        string[] missingCollaborators = requiredCollaborators
            .Where(fileName => !File.Exists(Path.Join(trainingRoot, fileName)))
            .ToArray();

        Assert.Empty(missingCollaborators);
        Assert.Contains("TrainingPlanTopicScorer", service, StringComparison.Ordinal);
        Assert.Contains("TrainingPlanTopicNarrativeBuilder", service, StringComparison.Ordinal);
        Assert.Contains("OpeningTrainingOutcomeSummarizer", service, StringComparison.Ordinal);
        Assert.DoesNotContain("private static OpeningTrainingOutcomeSummary BuildTrainingSummary", service, StringComparison.Ordinal);
        Assert.DoesNotContain("private static TrainingPlanTopicStatus DetermineTopicStatus", service, StringComparison.Ordinal);
        Assert.DoesNotContain("private static string BuildWhyThisTopicNow", service, StringComparison.Ordinal);
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
    public void DirectAnalysisStoreConstructorParametersStayInCompatibilityAdapters()
    {
        string root = FindRepositoryRoot();
        HashSet<string> allowedSites = DirectAnalysisStoreConstructorAllowList()
            .Select(NormalizeRelativePath)
            .ToHashSet(StringComparer.Ordinal);

        string[] directConstructorSites = EnumerateProductionCSharpFiles(root)
            .SelectMany(path => FindDirectAnalysisStoreConstructorSites(root, path))
            .Order()
            .ToArray();

        string[] unexpectedSites = directConstructorSites
            .Where(site => !allowedSites.Contains(site))
            .ToArray();
        Assert.Empty(unexpectedSites);

        string[] staleAllowListEntries = allowedSites
            .Where(site => !directConstructorSites.Contains(site, StringComparer.Ordinal))
            .Order()
            .ToArray();
        Assert.Empty(staleAllowListEntries);
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
    public void PresentationProjectRoleIsDocumentedAndFrameworkNeutral()
    {
        string root = FindRepositoryRoot();
        string presentationRoot = Path.Join(root, "MoveMentorChess.Presentation");
        string readmePath = Path.Join(presentationRoot, "README.md");

        Assert.True(File.Exists(readmePath), "Presentation must document its intended architectural role.");

        string readme = File.ReadAllText(readmePath);
        Assert.Contains("framework-neutral presentation adapter layer", readme, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("must not own platform rendering", readme, StringComparison.OrdinalIgnoreCase);

        string projectFile = File.ReadAllText(Path.Join(presentationRoot, "MoveMentorChess.Presentation.csproj"));
        Assert.DoesNotContain("FrameworkReference", projectFile, StringComparison.Ordinal);

        string[] allowedReferences =
        [
            "MoveMentorChess.Analysis",
            "MoveMentorChess.Domain",
            "MoveMentorChess.Localization",
            "MoveMentorChess.Opening",
            "MoveMentorChess.Profiles"
        ];
        string[] unexpectedReferences = ReadProjectReferences(root, "MoveMentorChess.Presentation")
            .Except(allowedReferences, StringComparer.Ordinal)
            .Order()
            .ToArray();
        Assert.Empty(unexpectedReferences);

        string[] forbiddenTokens =
        [
            "System.Drawing",
            "Avalonia",
            "Microsoft.Data.Sqlite",
            "AnalysisStoreProvider",
            "StockfishEngine",
            "LlamaCpp"
        ];
        string[] frameworkLeaks = Directory
            .EnumerateFiles(presentationRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => !ContainsPathSegment(path, "bin"))
            .Where(path => !ContainsPathSegment(path, "obj"))
            .SelectMany(path =>
            {
                string source = File.ReadAllText(path);
                return forbiddenTokens
                    .Where(token => source.Contains(token, StringComparison.Ordinal))
                    .Select(token => $"{Path.GetRelativePath(root, path)} contains {token}");
            })
            .Order()
            .ToArray();
        Assert.Empty(frameworkLeaks);

        Assert.False(
            File.Exists(Path.Join(presentationRoot, "Helpers", "BoardThumbnailRenderer.cs")),
            "GDI thumbnail rendering belongs outside the framework-neutral Presentation project.");
        Assert.True(
            File.Exists(Path.Join(root, "MoveMentorChess.App", "Renderers", "BoardThumbnailRenderer.cs")),
            "The existing GDI thumbnail renderer should live in the App rendering boundary.");
    }

    [Fact]
    public void OpeningTrainingSessionBuilderDelegatesSnapshotAndSourcePipelines()
    {
        string root = FindRepositoryRoot();
        string trainingRoot = Path.Join(root, "MoveMentorChess.Training");
        string builder = File.ReadAllText(Path.Join(trainingRoot, "OpeningTrainingSessionBuilder.cs"));

        string[] requiredCollaborators =
        [
            "OpeningTrainingSnapshotLoader.cs",
            "OpeningTrainingExampleGamePositionBuilder.cs",
            "OpeningTrainingOpeningWeaknessPositionBuilder.cs",
            "OpeningTrainingFirstMistakePositionBuilder.cs"
        ];
        string[] missingCollaborators = requiredCollaborators
            .Where(fileName => !File.Exists(Path.Join(trainingRoot, fileName)))
            .ToArray();
        Assert.Empty(missingCollaborators);

        Assert.Contains("OpeningTrainingSnapshotLoader", builder, StringComparison.Ordinal);
        Assert.Contains("OpeningTrainingExampleGamePositionBuilder", builder, StringComparison.Ordinal);
        Assert.Contains("OpeningTrainingOpeningWeaknessPositionBuilder", builder, StringComparison.Ordinal);
        Assert.Contains("OpeningTrainingFirstMistakePositionBuilder", builder, StringComparison.Ordinal);
        Assert.DoesNotContain("analysisDataSource.Load(", builder, StringComparison.Ordinal);
        Assert.DoesNotContain("BuildExampleGamePositions", builder, StringComparison.Ordinal);
        Assert.DoesNotContain("BuildOpeningWeaknessPositions", builder, StringComparison.Ordinal);
        Assert.DoesNotContain("BuildFirstMistakePositions", builder, StringComparison.Ordinal);

        string snapshotLoader = File.ReadAllText(Path.Join(trainingRoot, "OpeningTrainingSnapshotLoader.cs"));
        Assert.Contains("analysisDataSource.Load", snapshotLoader, StringComparison.Ordinal);
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

    private static Dictionary<string, string> VisibleXamlTextAllowList()
    {
        List<(string Site, string Reason)> entries = [];

        AddVisibleXamlTextEntries(
            entries,
            Path.Join("MoveMentorChess.App", "Views", "MainWindow.axaml"),
            "Sprint 7 - Localization Completion: MainWindow startup literals are overwritten by ApplyLocalizedText after construction; move this window fully to XAML localization in a later sprint.",
            (18, "Title", "MoveMentor Chess"),
            (47, "Content", "ROTATE BOARD"),
            (48, "Content", "UNDO"),
            (82, "Content", "Configure engine"),
            (89, "Content", "Paste PGN"),
            (95, "Content", "Analyze game"),
            (103, "Content", "PASTE PGN"),
            (104, "Content", "LOAD PGN FILE"),
            (106, "Content", "APPLY NEXT"),
            (107, "Content", "APPLY SELECTED"),
            (116, "Content", "ANALYZE IMPORTED"),
            (118, "Content", "PLAYER COACH"),
            (127, "Content", "SAVED ANALYSES"),
            (135, "Content", "LOAD SAVED"),
            (144, "Content", "OPENING TRAINER"),
            (153, "Content", "SETTINGS"),
            (163, "Content", "OPENING COVERAGE"),
            (173, "Content", "CLOSE APP"),
            (355, "ToolTip.Tip", "Stop import"));

        AddVisibleXamlTextEntries(
            entries,
            Path.Join("MoveMentorChess.App", "Views", "SettingsWindow.axaml"),
            "Sprint 7 - Localization Completion: SettingsWindow startup literals are overwritten by ApplyLocalizedText after construction; move this window fully to XAML localization in a later sprint.",
            (10, "Title", "Settings"),
            (21, "Text", "Settings"),
            (22, "Content", "Close"),
            (28, "Text", "Choose how MoveMentor uses the local model and Stockfish on this machine."),
            (41, "Text", "Setup checklist"),
            (45, "Text", "1. Pick or auto-detect Stockfish. 2. Import a PGN. 3. Run analysis. 4. Follow the training recommendation."),
            (50, "Text", "Language"),
            (55, "Text", "Model"),
            (57, "Content", "Use full GPU power for the local model"),
            (58, "Text", "llama-server.exe path (optional)"),
            (61, "PlaceholderText", "Auto-detect llama-server.exe"),
            (65, "Content", "Browse"),
            (68, "Text", "Explanation level"),
            (70, "Text", "Narration style"),
            (78, "Text", "Stockfish"),
            (79, "Text", "stockfish.exe path"),
            (82, "PlaceholderText", "Auto-detect stockfish.exe"),
            (86, "Content", "Browse"),
            (89, "Text", "Engine threads"),
            (94, "Text", "Hash memory (MB)"),
            (99, "Text", "Bulk analysis depth"),
            (104, "Text", "Bulk analysis MultiPV"),
            (109, "Text", "Bulk analysis move time (ms)"),
            (133, "Text", "Save when Stockfish is detected or after choosing a custom path."),
            (139, "Content", "Cancel"),
            (140, "Content", "Save"));

        return entries.ToDictionary(entry => entry.Site, entry => entry.Reason, StringComparer.Ordinal);
    }

    private static void AddVisibleXamlTextEntries(
        List<(string Site, string Reason)> entries,
        string relativePath,
        string reason,
        params (int Line, string Attribute, string Value)[] sites)
    {
        string normalizedPath = NormalizeRelativePath(relativePath);
        foreach ((int line, string attribute, string value) in sites)
        {
            entries.Add(($"{normalizedPath}:{line}:{attribute}={value}", reason));
        }
    }

    private static IEnumerable<string> EnumerateAppXamlFiles(string root)
        => Directory
            .EnumerateFiles(Path.Join(root, "MoveMentorChess.App", "Views"), "*.axaml", SearchOption.AllDirectories)
            .Where(path => !ContainsPathSegment(path, "bin"))
            .Where(path => !ContainsPathSegment(path, "obj"))
            .Order(StringComparer.Ordinal);

    private static IEnumerable<VisibleXamlTextLiteral> FindVisibleXamlTextLiterals(string root, string path)
    {
        XDocument document = XDocument.Load(path, LoadOptions.SetLineInfo);
        string relativePath = NormalizeRelativePath(Path.GetRelativePath(root, path));

        foreach (XAttribute attribute in document.Descendants().Attributes())
        {
            string attributeName = attribute.Name.LocalName;
            if (!VisibleXamlTextAttributeNames.Contains(attributeName))
            {
                continue;
            }

            string value = attribute.Value;
            if (string.IsNullOrWhiteSpace(value)
                || IsAllowedVisibleTextExpression(value))
            {
                continue;
            }

            IXmlLineInfo lineInfo = attribute;
            int lineNumber = lineInfo.HasLineInfo() ? lineInfo.LineNumber : 0;
            yield return new VisibleXamlTextLiteral(relativePath, lineNumber, attributeName, value);
        }
    }

    private static bool IsAllowedVisibleTextExpression(string value)
    {
        string trimmed = value.TrimStart();
        return trimmed.StartsWith("{loc:Localize ", StringComparison.Ordinal)
            || trimmed.StartsWith("{Binding", StringComparison.Ordinal)
            || trimmed.StartsWith("{CompiledBinding", StringComparison.Ordinal);
    }

    private static Dictionary<string, PublicTopLevelTypeAllowListEntry> PublicTopLevelTypeAllowList()
    {
        PublicTopLevelTypeAllowListEntry[] entries =
        [
            new(
                Path.Join("MoveMentorChess.Analysis", "Services", "AdviceQualityEvaluator.cs"),
                ["AdviceQualityEvaluator", "AdviceQualityEvaluationResult"],
                "Architecture cleanup",
                "Sprint 2 - Public Type File Hygiene: split the evaluation result from the evaluator."),
            new(
                Path.Join("MoveMentorChess.Analysis", "Services", "ApplicationSettingsStore.cs"),
                ["ApplicationSettingsStore", "ApplicationSettingsSaveException"],
                "Architecture cleanup",
                "Sprint 6 - Settings And Runtime Composition: separate settings store exceptions from the static facade."),
            new(
                Path.Join("MoveMentorChess.Analysis", "Services", "JsonlDiagnosticsLogger.cs"),
                ["JsonlDiagnosticsLogger", "QualityGateDiagnosticsLogger", "AdviceFeedbackLogger"],
                "Architecture cleanup",
                "Sprint 7 - Localization Completion: split diagnostics logger facades before tightening file hygiene."),
            new(
                Path.Join("MoveMentorChess.Analysis", "Services", "RuntimeSettingsEnvironment.cs"),
                ["IRuntimeSettingsEnvironment", "SystemRuntimeSettingsEnvironment"],
                "Architecture cleanup",
                "Sprint 6 - Settings And Runtime Composition: move runtime settings environment types into separate files."),
            new(
                Path.Join("MoveMentorChess.App", "Controls", "ChessBoardView.cs"),
                ["ChessBoardView", "BoardSquarePressedEventArgs"],
                "Architecture cleanup",
                "Sprint 2 - Public Type File Hygiene: split board event args from the Avalonia control."),
            new(
                Path.Join("MoveMentorChess.App", "ViewModels", "OpeningCoverageWindowViewModel.cs"),
                ["OpeningCoverageWindowViewModel", "OpeningCoverageLineItemViewModel"],
                "Architecture cleanup",
                "Sprint 4 - Opening Trainer ViewModel Slice: split opening coverage list items from the window ViewModel."),
            new(
                Path.Join("MoveMentorChess.App", "ViewModels", "OpeningStudyFeedbackAnimator.cs"),
                ["OpeningStudyFeedbackAnimator", "OpeningStudyFeedbackFrame"],
                "Architecture cleanup",
                "Sprint 4 - Opening Trainer ViewModel Slice: split animation frame data from the animator."),
            new(
                Path.Join("MoveMentorChess.App", "ViewModels", "OpeningTrainerWindowViewModel.cs"),
                ["OpeningTrainerWindowViewModel", "TrainingNextActionCardViewModel"],
                "Architecture cleanup",
                "Sprint 4 - Opening Trainer ViewModel Slice: move the remaining result card record out after the selection slice."),
            new(
                Path.Join("MoveMentorChess.App", "ViewModels", "RelayCommand.cs"),
                ["RelayCommand", "RelayCommand"],
                "Architecture cleanup",
                "Sprint 2 - Public Type File Hygiene: split generic and non-generic relay commands."),
            new(
                Path.Join("MoveMentorChess.App", "Views", "SavedAnalysesWindow.axaml.cs"),
                ["SavedAnalysesWindow", "SavedAnalysisAction"],
                "Architecture cleanup",
                "Sprint 5 - Main Window Import And Replay Extraction: split saved-analysis action from the window code-behind."),
            new(
                Path.Join("MoveMentorChess.Domain", "IClock.cs"),
                ["IClock", "SystemClock"],
                "Architecture cleanup",
                "Sprint 6 - Settings And Runtime Composition: split the clock port and production implementation."),
            new(
                Path.Join("MoveMentorChess.Domain", "Models", "ChessGame.cs"),
                ["ChessGame", "AppliedMoveInfo", "LegalMoveInfo"],
                "Architecture cleanup",
                "Sprint 2 - Public Type File Hygiene: split public chess result records from the rules engine."),
            new(
                Path.Join("MoveMentorChess.Domain", "Models", "OpeningTrainingAnswerOption.cs"),
                ["OpeningTrainingAnswerOption", "OpeningTrainingAnswerKind"],
                "Architecture cleanup",
                "Sprint 2 - Public Type File Hygiene: split opening training answer enum from the option record."),
            new(
                Path.Join("MoveMentorChess.Domain", "Models", "OpeningTrainingScheduledAction.cs"),
                ["OpeningTrainingScheduledAction", "OpeningTrainingScheduledActionStatus"],
                "Architecture cleanup",
                "Sprint 2 - Public Type File Hygiene: split scheduled action status from the scheduled action record."),
            new(
                Path.Join("MoveMentorChess.Domain", "Models", "OpeningTrainingTelemetryEvent.cs"),
                ["OpeningTrainingTelemetryEvent", "OpeningTrainingTelemetryEvents"],
                "Architecture cleanup",
                "Sprint 2 - Public Type File Hygiene: split telemetry constants from the telemetry event record."),
            new(
                Path.Join("MoveMentorChess.Domain", "Models", "OpeningUnderstandingCard.cs"),
                ["OpeningUnderstandingCard", "OpeningUnderstandingCardKind"],
                "Architecture cleanup",
                "Sprint 2 - Public Type File Hygiene: split opening understanding card enum from the record."),
            new(
                Path.Join("MoveMentorChess.Domain", "Models", "PlayerMistakeProfile.cs"),
                ["PlayerMistakeProfile", "PlayerMistakePatternEntry"],
                "Architecture cleanup",
                "Sprint 2 - Public Type File Hygiene: split player mistake pattern entries from the profile record."),
            new(
                Path.Join("MoveMentorChess.Domain", "Models", "PlayerOpeningPlan.cs"),
                ["PlayerOpeningPlan", "PlayerOpeningPlanItem", "TrainingProgressSnapshot"],
                "Architecture cleanup",
                "Sprint 2 - Public Type File Hygiene: split player opening plan DTOs into separate files."),
            new(
                Path.Join("MoveMentorChess.Domain", "Models", "SpecialTrainingModeDefinition.cs"),
                ["SpecialTrainingModeDefinition", "SpecialTrainingModeKind"],
                "Architecture cleanup",
                "Sprint 2 - Public Type File Hygiene: split special training mode enum from its definition record."),
            new(
                Path.Join("MoveMentorChess.Domain", "Models", "TrainingCoachHint.cs"),
                ["TrainingCoachHint", "TrainingCoachHintLevel", "TrainingMistakeCategory"],
                "Architecture cleanup",
                "Sprint 2 - Public Type File Hygiene: split training coach hint enums from the hint record."),
            new(
                Path.Join("MoveMentorChess.Domain", "Models", "TrainingNextAction.cs"),
                ["TrainingNextAction", "TrainingNextActionKind"],
                "Architecture cleanup",
                "Sprint 2 - Public Type File Hygiene: split training next action enum from the record."),
            new(
                Path.Join("MoveMentorChess.Domain", "Models", "TrainingPriorityItem.cs"),
                ["TrainingPriorityItem", "TrainingPriorityAction", "TrainingPriorityReasonCode"],
                "Architecture cleanup",
                "Sprint 2 - Public Type File Hygiene: split training priority enums from the item record."),
            new(
                Path.Join("MoveMentorChess.Domain", "Models", "TrainingRecommendationCard.cs"),
                ["TrainingRecommendationCard", "TrainingRecommendationDifficulty", "TrainingRecommendationReasonCode", "TrainingRecommendationType"],
                "Architecture cleanup",
                "Sprint 2 - Public Type File Hygiene: split training recommendation enums from the card record."),
            new(
                Path.Join("MoveMentorChess.Domain", "Models", "TrainingResultLearningPlan.cs"),
                ["TrainingResultLearningPlan", "TrainingResultReviewItem"],
                "Architecture cleanup",
                "Sprint 2 - Public Type File Hygiene: split training review items from the learning plan record."),
            new(
                Path.Join("MoveMentorChess.Engine", "StockfishEngine.cs"),
                ["StockfishEngine", "StockfishEngineOptions", "EvaluationSummary"],
                "Architecture cleanup",
                "Sprint 2 - Public Type File Hygiene: split engine options and evaluation summary from the engine wrapper."),
            new(
                Path.Join("MoveMentorChess.Opening", "OpeningTreePruner.cs"),
                ["OpeningTreePruner", "OpeningTreePruningOptions"],
                "Architecture cleanup",
                "Sprint 2 - Public Type File Hygiene: split pruning options from the pruner."),
            new(
                Path.Join("MoveMentorChess.Persistence", "OpeningSeedBootstrapper.cs"),
                ["OpeningSeedBootstrapper", "IOpeningSeedRuntimeEnvironment", "SystemOpeningSeedRuntimeEnvironment", "OpeningSeedBootstrapResult"],
                "Architecture cleanup",
                "Sprint 6 - Settings And Runtime Composition: split seed runtime environment and result records from the bootstrapper."),
            new(
                Path.Join("MoveMentorChess.Persistence", "PersistenceDiagnostics.cs"),
                ["IPersistenceDiagnosticsLogger", "PersistenceDiagnostics", "TracePersistenceDiagnosticsLogger"],
                "Architecture cleanup",
                "Sprint 6 - Settings And Runtime Composition: split persistence diagnostics port, facade, and trace adapter."),
            new(
                Path.Join("MoveMentorChess.Presentation", "Models", "AnalysisMistakePresentation.cs"),
                ["SelectedMistakeViewItem", "AnalysisMistakePresentation"],
                "Architecture cleanup",
                "Sprint 8 - Presentation And Training Pipeline Split: split selected mistake view item from its presenter."),
            new(
                Path.Join("MoveMentorChess.Presentation", "Models", "AnalysisSelectedDetailsPresentation.cs"),
                ["AnalysisSelectedDetailsPresentation", "AnalysisSelectedDetailsPresenter"],
                "Architecture cleanup",
                "Sprint 8 - Presentation And Training Pipeline Split: split selected details model from its presenter."),
            new(
                Path.Join("MoveMentorChess.Presentation", "Models", "AnalysisSelectionState.cs"),
                ["AnalysisReviewFilter", "AnalysisFilterOption", "AnalysisFilterResult", "AnalysisSelectionState"],
                "Architecture cleanup",
                "Sprint 8 - Presentation And Training Pipeline Split: split selection filters/results from mutable selection state."),
            new(
                Path.Join("MoveMentorChess.Presentation", "Models", "AnalysisSnapshotPresentation.cs"),
                ["AnalysisSnapshotMode", "AnalysisSnapshotArrow", "AnalysisSnapshotPresentation"],
                "Architecture cleanup",
                "Sprint 8 - Presentation And Training Pipeline Split: split snapshot mode/arrow models from the presenter."),
            new(
                Path.Join("MoveMentorChess.Presentation", "Models", "AnalysisTimelinePresentation.cs"),
                ["AnalysisTimelinePresentation", "SimilarMistakeLink", "PhaseSegment"],
                "Architecture cleanup",
                "Sprint 8 - Presentation And Training Pipeline Split: split timeline DTOs from the presenter."),
            new(
                Path.Join("MoveMentorChess.Presentation", "Models", "ProfileTrendChartPresentation.cs"),
                ["ProfileTrendChartKind", "ProfileTrendChartPoint", "ProfileTrendChartSeries"],
                "Architecture cleanup",
                "Sprint 8 - Presentation And Training Pipeline Split: split profile trend chart DTOs into separate files."),
            new(
                Path.Join("MoveMentorChess.Profiles", "PlayerProfileService.cs"),
                ["PlayerProfileService", "ProfileDataAvailability"],
                "Architecture cleanup",
                "Sprint 2 - Public Type File Hygiene: split profile data availability from the profile service facade."),
            new(
                Path.Join("MoveMentorChess.Profiles", "PlayerStrengthEstimator.cs"),
                ["IPlayerStrengthEstimator", "PlayerStrengthEstimateInput", "HeuristicPlayerStrengthEstimator", "ProfileMlPlayerStrengthEstimator"],
                "Architecture cleanup",
                "Sprint 2 - Public Type File Hygiene: split estimator port, input, and implementations."),
            new(
                Path.Join("MoveMentorChess.Tracking", "Services", "MoveListOcrRecognizer.cs"),
                ["IMoveListRecognizer", "MoveListOcrRecognizer"],
                "Architecture cleanup",
                "Sprint 8 - Presentation And Training Pipeline Split: split OCR recognizer port from the implementation."),
            new(
                Path.Join("MoveMentorChess.Training", "OpeningTrainingPositionSelector.cs"),
                ["OpeningTrainingPositionSelector", "OpeningTrainingPositionSelection"],
                "Architecture cleanup",
                "Sprint 8 - Presentation And Training Pipeline Split: split selection result from the selector."),
            new(
                Path.Join("MoveMentorChess.Training", "TrainingPlanTopicNarrativeBuilder.cs"),
                ["TrainingPlanTopicNarrativeInput", "TrainingPlanTopicNarrative", "TrainingPlanTopicNarrativeBuilder"],
                "Architecture cleanup",
                "Sprint 8 - Presentation And Training Pipeline Split: split narrative input/result records from the builder."),
            new(
                Path.Join("MoveMentorChess.Training", "TrainingPlanTopicScorer.cs"),
                ["TrainingPlanTopicScoringInput", "TrainingPlanTopicScoringResult", "TrainingPlanTopicScorer"],
                "Architecture cleanup",
                "Sprint 8 - Presentation And Training Pipeline Split: split scoring input/result records from the scorer.")
        ];

        return entries.ToDictionary(entry => NormalizeRelativePath(entry.RelativePath), StringComparer.Ordinal);
    }

    private static string[] DirectAnalysisStoreConstructorAllowList()
    {
        return
        [
            $"{Path.Join("MoveMentorChess.App", "ViewModels", "OpeningCoverageWindowViewModel.cs")}: OpeningCoverageWindowViewModel(IAnalysisStore analysisStore)",
            $"{Path.Join("MoveMentorChess.App", "ViewModels", "OpeningTrainerWindowViewModel.cs")}: OpeningTrainerWindowViewModel(IAnalysisStore analysisStore)",
            $"{Path.Join("MoveMentorChess.App", "ViewModels", "StoreBackedSavedLibraryDataService.cs")}: StoreBackedSavedLibraryDataService(IAnalysisStore analysisStore)",
            $"{Path.Join("MoveMentorChess.App", "ViewModels", "StoreBackedSavedLibraryDataService.cs")}: StoreBackedSavedLibraryDataService(IAnalysisStore analysisStore, IAnalysisResultCache analysisResultCache)",
            $"{Path.Join("MoveMentorChess.App", "Views", "SavedAnalysesWindow.axaml.cs")}: SavedAnalysesWindow(IAnalysisStore analysisStore, bool canOpenAnalysis)",
            $"{Path.Join("MoveMentorChess.App", "Views", "SavedGamesWindow.axaml.cs")}: SavedGamesWindow(IAnalysisStore analysisStore)",
            $"{Path.Join("MoveMentorChess.Profiles", "PlayerProfileService.cs")}: PlayerProfileService(IAnalysisStore analysisStore)",
            $"{Path.Join("MoveMentorChess.Training", "OpeningTrainerService.cs")}: OpeningTrainerService(IAnalysisStore analysisStore)",
            $"{Path.Join("MoveMentorChess.Training", "OpeningTrainerService.cs")}: OpeningTrainerService(IAnalysisStore analysisStore, IClock clock)",
            $"{Path.Join("MoveMentorChess.Training", "OpeningTrainerWorkspaceService.cs")}: OpeningTrainerWorkspaceService(IAnalysisStore analysisStore)",
            $"{Path.Join("MoveMentorChess.Training", "OpeningTrainerWorkspaceService.cs")}: OpeningTrainerWorkspaceService(IAnalysisStore analysisStore, IClock clock)",
            $"{Path.Join("MoveMentorChess.Training", "OpeningWeaknessService.cs")}: OpeningWeaknessService(IAnalysisStore analysisStore)"
        ];
    }

    private static IEnumerable<string> EnumerateProductionCSharpFiles(string root)
    {
        return Directory
            .EnumerateDirectories(root, "MoveMentorChess.*", SearchOption.TopDirectoryOnly)
            .SelectMany(project => Directory.EnumerateFiles(project, "*.cs", SearchOption.AllDirectories))
            .Where(path => !ContainsPathSegment(path, "bin"))
            .Where(path => !ContainsPathSegment(path, "obj"))
            .Where(path => !IsGeneratedCSharpFile(path));
    }

    private static IEnumerable<string> FindDirectAnalysisStoreConstructorSites(string root, string path)
    {
        string source = RemoveCommentsAndLiterals(File.ReadAllText(path));
        if (!source.Contains("IAnalysisStore", StringComparison.Ordinal))
        {
            yield break;
        }

        string relativePath = NormalizeRelativePath(Path.GetRelativePath(root, path));
        HashSet<string> typeNames = TypeDeclarationRegex()
            .Matches(source)
            .Select(match => match.Groups["name"].Value)
            .ToHashSet(StringComparer.Ordinal);

        foreach (Match match in ConstructorDeclarationRegex().Matches(source).Cast<Match>())
        {
            string typeName = match.Groups["name"].Value;
            if (!typeNames.Contains(typeName))
            {
                continue;
            }

            string parameters = match.Groups["parameters"].Value;
            if (!DirectAnalysisStoreParameterRegex().IsMatch(parameters))
            {
                continue;
            }

            yield return $"{relativePath}: {typeName}({NormalizeParameterList(parameters)})";
        }
    }

    private static string NormalizeParameterList(string parameters)
        => WhitespaceRegex().Replace(parameters, " ").Trim();

    private static string[] FindPublicTopLevelTypeNames(string source)
    {
        string sanitizedSource = RemoveCommentsAndLiterals(source);
        (int Start, int End)[] typeBodyIntervals = FindTypeBodyIntervals(sanitizedSource);

        return PublicTypeDeclarationRegex()
            .Matches(sanitizedSource)
            .Where(match => !IsInsideTypeBody(match.Index, typeBodyIntervals))
            .Select(match => match.Groups["name"].Value)
            .ToArray();
    }

    private static (int Start, int End)[] FindTypeBodyIntervals(string sanitizedSource)
    {
        return TypeDeclarationRegex()
            .Matches(sanitizedSource)
            .Select(match => FindTypeBodyInterval(sanitizedSource, match.Index + match.Length))
            .Where(interval => interval.HasValue)
            .Select(interval => interval!.Value)
            .ToArray();
    }

    private static (int Start, int End)? FindTypeBodyInterval(string sanitizedSource, int declarationEnd)
    {
        for (int index = declarationEnd; index < sanitizedSource.Length; index++)
        {
            if (sanitizedSource[index] == ';')
            {
                return null;
            }

            if (sanitizedSource[index] == '{')
            {
                int end = FindMatchingBrace(sanitizedSource, index);
                return end < 0 ? null : (index, end);
            }
        }

        return null;
    }

    private static int FindMatchingBrace(string sanitizedSource, int openingBraceIndex)
    {
        int depth = 0;
        for (int index = openingBraceIndex; index < sanitizedSource.Length; index++)
        {
            if (sanitizedSource[index] == '{')
            {
                depth++;
            }
            else if (sanitizedSource[index] == '}')
            {
                depth--;
                if (depth == 0)
                {
                    return index;
                }
            }
        }

        return -1;
    }

    private static bool IsInsideTypeBody(int position, IReadOnlyList<(int Start, int End)> typeBodyIntervals)
    {
        return typeBodyIntervals.Any(interval => position > interval.Start && position < interval.End);
    }

    private static string RemoveCommentsAndLiterals(string source)
    {
        char[] characters = source.ToCharArray();

        for (int index = 0; index < characters.Length; index++)
        {
            if (characters[index] == '/' && index + 1 < characters.Length && characters[index + 1] == '/')
            {
                int end = index + 2;
                while (end < characters.Length && characters[end] != '\r' && characters[end] != '\n')
                {
                    end++;
                }

                ReplaceWithSpaces(characters, index, end);
                index = end;
            }
            else if (characters[index] == '/' && index + 1 < characters.Length && characters[index + 1] == '*')
            {
                int end = index + 2;
                while (end + 1 < characters.Length && (characters[end] != '*' || characters[end + 1] != '/'))
                {
                    end++;
                }

                end = Math.Min(end + 2, characters.Length);
                ReplaceWithSpaces(characters, index, end);
                index = end - 1;
            }
            else if (TryFindRawStringLiteralEnd(characters, index, out int rawStringEnd))
            {
                ReplaceWithSpaces(characters, index, rawStringEnd);
                index = rawStringEnd - 1;
            }
            else if (characters[index] == '@' && index + 1 < characters.Length && characters[index + 1] == '"')
            {
                int end = index + 2;
                while (end < characters.Length)
                {
                    if (characters[end] == '"' && end + 1 < characters.Length && characters[end + 1] == '"')
                    {
                        end += 2;
                        continue;
                    }

                    if (characters[end] == '"')
                    {
                        end++;
                        break;
                    }

                    end++;
                }

                ReplaceWithSpaces(characters, index, end);
                index = end - 1;
            }
            else if (characters[index] == '"')
            {
                int end = index + 1;
                while (end < characters.Length)
                {
                    if (characters[end] == '\\')
                    {
                        end += 2;
                        continue;
                    }

                    if (characters[end] == '"')
                    {
                        end++;
                        break;
                    }

                    end++;
                }

                ReplaceWithSpaces(characters, index, end);
                index = end - 1;
            }
            else if (characters[index] == '\'')
            {
                int end = index + 1;
                while (end < characters.Length)
                {
                    if (characters[end] == '\\')
                    {
                        end += 2;
                        continue;
                    }

                    if (characters[end] == '\'')
                    {
                        end++;
                        break;
                    }

                    end++;
                }

                ReplaceWithSpaces(characters, index, end);
                index = end - 1;
            }
        }

        return new string(characters);
    }

    private static bool TryFindRawStringLiteralEnd(char[] characters, int start, out int end)
    {
        end = -1;

        int quoteStart = start;
        while (quoteStart < characters.Length && characters[quoteStart] == '$')
        {
            quoteStart++;
        }

        if (quoteStart >= characters.Length
            || characters[quoteStart] != '"'
            || (quoteStart == start && characters[start] != '"'))
        {
            return false;
        }

        int quoteCount = CountQuoteRun(characters, quoteStart);
        if (quoteCount < 3)
        {
            return false;
        }

        int searchIndex = quoteStart + quoteCount;
        while (searchIndex < characters.Length)
        {
            if (characters[searchIndex] != '"')
            {
                searchIndex++;
                continue;
            }

            int closingQuoteCount = CountQuoteRun(characters, searchIndex);
            if (closingQuoteCount >= quoteCount)
            {
                end = searchIndex + quoteCount;
                return true;
            }

            searchIndex += closingQuoteCount;
        }

        return false;
    }

    private static int CountQuoteRun(char[] characters, int start)
    {
        int count = 0;
        while (start + count < characters.Length && characters[start + count] == '"')
        {
            count++;
        }

        return count;
    }

    private static void ReplaceWithSpaces(char[] characters, int start, int end)
    {
        for (int index = start; index < end; index++)
        {
            if (characters[index] != '\r' && characters[index] != '\n')
            {
                characters[index] = ' ';
            }
        }
    }

    private static bool ContainsPathSegment(string path, string segment)
    {
        return path
            .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Contains(segment, StringComparer.OrdinalIgnoreCase);
    }

    private static bool IsGeneratedCSharpFile(string path)
    {
        string fileName = Path.GetFileName(path);
        if (fileName.EndsWith(".g.cs", StringComparison.OrdinalIgnoreCase)
            || fileName.EndsWith(".g.i.cs", StringComparison.OrdinalIgnoreCase)
            || fileName.EndsWith(".generated.cs", StringComparison.OrdinalIgnoreCase)
            || fileName.EndsWith(".designer.cs", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return File.ReadLines(path)
            .Take(5)
            .Any(line => line.Contains("<auto-generated", StringComparison.OrdinalIgnoreCase));
    }

    private static string NormalizeRelativePath(string path)
    {
        return path.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
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

    [GeneratedRegex(@"\bpublic\s+(?:(?:new|abstract|sealed|static|partial|unsafe|readonly|ref)\s+)*(?:(?:record\s+(?:class\s+|struct\s+)?)|class\s+|interface\s+|struct\s+|enum\s+)(?<name>[A-Za-z_][A-Za-z0-9_]*)", RegexOptions.CultureInvariant)]
    private static partial Regex PublicTypeDeclarationRegex();

    [GeneratedRegex(@"\b(?:(?:public|private|protected|internal|file)\s+)?(?:(?:new|abstract|sealed|static|partial|unsafe|readonly|ref)\s+)*(?:(?:record\s+(?:class\s+|struct\s+)?)|class\s+|interface\s+|struct\s+|enum\s+)(?<name>[A-Za-z_][A-Za-z0-9_]*)", RegexOptions.CultureInvariant)]
    private static partial Regex TypeDeclarationRegex();

    [GeneratedRegex(@"\b(?:(?:public|private|protected|internal)\s+)+(?:static\s+|extern\s+)*(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*\((?<parameters>[^()]*)\)", RegexOptions.CultureInvariant | RegexOptions.Singleline)]
    private static partial Regex ConstructorDeclarationRegex();

    [GeneratedRegex(@"(^|,)\s*(?:\[[^\]]+\]\s*)*IAnalysisStore\??\s+[A-Za-z_][A-Za-z0-9_]*", RegexOptions.CultureInvariant | RegexOptions.Singleline)]
    private static partial Regex DirectAnalysisStoreParameterRegex();

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex WhitespaceRegex();

    private sealed record PublicTopLevelTypeAllowListEntry(
        string RelativePath,
        IReadOnlyList<string> PublicTypes,
        string Owner,
        string FollowUp);

    private sealed record VisibleXamlTextLiteral(
        string RelativePath,
        int Line,
        string Attribute,
        string Value)
    {
        public string Site => $"{RelativePath}:{Line}:{Attribute}={Value}";
    }
}
