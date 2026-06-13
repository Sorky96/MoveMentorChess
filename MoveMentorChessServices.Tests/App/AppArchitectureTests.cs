using System.Text.RegularExpressions;
using MoveMentorChess.Presentation.Models;
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
            (Path.Join(root, "MoveMentorChess.App", "ViewModels", "OpeningTrainerWindowViewModel.cs"), 2498),
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
                Path.Join("MoveMentorChess.App", "ViewModels", "MainWindowViewModel.cs"),
                ["MainWindowViewModel", "PgnFileImportResult", "BulkPgnAnalysisResult"],
                "Architecture cleanup",
                "Sprint 5 - Main Window Import And Replay Extraction: move import result records out with the import workflow."),
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
                ["OpeningTrainerWindowViewModel", "OpeningTrainingProfileChoice", "OpeningTrainingIntensityChoice", "TrainingNextActionCardViewModel"],
                "Architecture cleanup",
                "Sprint 4 - Opening Trainer ViewModel Slice: move choice/card records with the extracted selection ViewModel."),
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
                Path.Join("MoveMentorChess.Domain", "Models", "IAnalysisStore.cs"),
                [
                    "IImportedGameStore",
                    "IAnalysisResultStore",
                    "IStoredMoveAnalysisStore",
                    "IAdviceFeedbackStore",
                    "IAnalysisWindowStateStore",
                    "IAnalysisStore",
                    "IOpeningTreeStore",
                    "IOpeningTheoryStore",
                    "IOpeningLineContextStore",
                    "IOpeningTrainingHistoryStore",
                    "IOpeningTrainingTelemetryStore"
                ],
                "Architecture cleanup",
                "Sprint 2 - Public Type File Hygiene: split store ports before Sprint 3 narrows store injection."),
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
                Path.Join("MoveMentorChess.Domain", "Models", "PgnGameParser.cs"),
                ["PgnGameParser", "PgnBatchParseResult", "PgnBatchParseError"],
                "Architecture cleanup",
                "Sprint 5 - Main Window Import And Replay Extraction: split PGN parse result records from the parser."),
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

    private static IEnumerable<string> EnumerateProductionCSharpFiles(string root)
    {
        return Directory
            .EnumerateDirectories(root, "MoveMentorChess.*", SearchOption.TopDirectoryOnly)
            .SelectMany(project => Directory.EnumerateFiles(project, "*.cs", SearchOption.AllDirectories))
            .Where(path => !ContainsPathSegment(path, "bin"))
            .Where(path => !ContainsPathSegment(path, "obj"))
            .Where(path => !IsGeneratedCSharpFile(path));
    }

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

    private sealed record PublicTopLevelTypeAllowListEntry(
        string RelativePath,
        IReadOnlyList<string> PublicTypes,
        string Owner,
        string FollowUp);
}
