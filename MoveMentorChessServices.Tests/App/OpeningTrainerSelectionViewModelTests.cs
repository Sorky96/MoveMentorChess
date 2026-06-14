using MoveMentorChess.App.ViewModels;
using Xunit;

namespace MoveMentorChessServices.Tests.App;

public sealed class OpeningTrainerSelectionViewModelTests
{
    private const string StartFen = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";

    [Fact]
    public void InitialState_UsesBothProfileAndBalancedIntensity()
    {
        OpeningTrainerSelectionViewModel viewModel = new(CreateWorkspace());

        Assert.Equal("opening-coach:both", viewModel.PlayerKey);
        Assert.Equal(RepertoireSide.Both, viewModel.SelectedSide);
        Assert.Equal(OpeningTrainingStrictness.BookFlexible, viewModel.SelectedStrictness);
        Assert.Equal("both", viewModel.SelectedProfileChoice?.Id);
        Assert.Equal("balanced", viewModel.SelectedIntensityChoice?.Id);
        Assert.Null(viewModel.TodayRecommendation);
    }

    [Fact]
    public void SelectedProfile_UpdatesPlayerKeyAndSide()
    {
        OpeningTrainerSelectionViewModel viewModel = new(CreateWorkspace());
        OpeningTrainingProfileChoice blackProfile = viewModel.AvailableProfileChoices.First(choice => choice.Id == "black");

        viewModel.SelectedProfileChoice = blackProfile;

        Assert.Equal("opening-coach:black", viewModel.PlayerKey);
        Assert.Equal(RepertoireSide.Black, viewModel.SelectedSide);

        viewModel.AdvancedPlayerKey = "  custom-player  ";

        Assert.Equal("custom-player", viewModel.PlayerKey);
        Assert.Contains("custom-player", viewModel.ActiveHistoryKeyText, StringComparison.Ordinal);
    }

    [Fact]
    public void SelectedIntensity_UpdatesStrictnessAndCoachingTextState()
    {
        OpeningTrainerSelectionViewModel viewModel = new(CreateWorkspace());
        OpeningTrainingIntensityChoice challenge = viewModel.AvailableIntensityChoices.First(choice => choice.Id == "challenge");
        List<string?> propertyNames = [];
        viewModel.PropertyChanged += (_, args) => propertyNames.Add(args.PropertyName);

        viewModel.SelectedIntensityChoice = challenge;

        Assert.Equal(OpeningTrainingStrictness.Exploration, viewModel.SelectedStrictness);
        Assert.Equal(challenge.Description, viewModel.SelectedIntensitySummary);
        Assert.Contains(nameof(OpeningTrainerSelectionViewModel.SelectedStrictness), propertyNames);
        Assert.Contains(nameof(OpeningTrainerSelectionViewModel.TodayStartSequenceText), propertyNames);
    }

    [Fact]
    public void RefreshTodayRecommendation_UsesSelectedSideAndBuildsPlayerPlan()
    {
        OpeningLineCatalogItem whiteLine = CreateLine("C20", "King's Pawn Game", "Main Line", RepertoireSide.White, 18, 4);
        OpeningLineCatalogItem blackLine = CreateLine("B01", "Scandinavian Defense", "Main Line", RepertoireSide.Black, 22, 5);
        OpeningTrainerSelectionViewModel viewModel = new(CreateWorkspace([whiteLine, blackLine]));
        viewModel.SelectedProfileChoice = viewModel.AvailableProfileChoices.First(choice => choice.Id == "black");

        viewModel.RefreshTodayRecommendation();

        Assert.Equal(blackLine, viewModel.TodayRecommendation?.OpeningLine);
        Assert.Equal(RepertoireSide.Black, viewModel.TodayRecommendation?.OpeningLine.RepertoireSide);
        Assert.NotNull(viewModel.PlayerOpeningPlan);
        Assert.NotEmpty(viewModel.PlayerOpeningPlan.Today);
        Assert.NotEmpty(viewModel.TodayPlanItems);
        Assert.NotEmpty(viewModel.SpecialTrainingModes);
        Assert.NotNull(viewModel.SelectedSpecialMode);
    }

    private static OpeningTrainerWorkspaceService CreateWorkspace(IReadOnlyList<OpeningLineCatalogItem>? lines = null)
    {
        SelectionStore store = new(lines ?? [CreateLine("C20", "King's Pawn Game", "Main Line", RepertoireSide.White, 18, 4)]);
        return new OpeningTrainerWorkspaceService(store, store, store, store, store, null);
    }

    private static OpeningLineCatalogItem CreateLine(
        string eco,
        string openingName,
        string variationName,
        RepertoireSide side,
        int bookGameCount,
        int bookBranchCount)
    {
        return OpeningLineCatalogBuilder.CreateItem(
            eco,
            openingName,
            variationName,
            side,
            new OpeningPositionKey($"{eco}:{side}"),
            StartFen,
            bookGameCount,
            bookBranchCount);
    }

    private sealed class SelectionStore :
        IImportedGameStore,
        IAnalysisResultStore,
        IStoredMoveAnalysisStore,
        IOpeningTheoryStore,
        IOpeningTrainingHistoryStore
    {
        private readonly IReadOnlyList<OpeningLineCatalogItem> lines;

        public SelectionStore(IReadOnlyList<OpeningLineCatalogItem> lines)
        {
            this.lines = lines;
        }

        public IReadOnlyList<OpeningLineCatalogItem> ListOpeningLines(string? filterText = null, RepertoireSide? repertoireSide = null, int limit = 100)
        {
            return lines
                .Where(line =>
                    repertoireSide is null
                    || repertoireSide == RepertoireSide.Both
                    || line.RepertoireSide == repertoireSide)
                .Where(line => string.IsNullOrWhiteSpace(filterText)
                    || line.DisplayName.Contains(filterText, StringComparison.OrdinalIgnoreCase))
                .Take(limit)
                .ToArray();
        }

        public bool TryGetOpeningPositionByKey(string positionKey, out OpeningTheoryPosition? position)
        {
            position = null;
            return false;
        }

        public IReadOnlyList<OpeningTheoryMove> GetOpeningMovesByPositionKey(string positionKey, int limit = 10, bool playableOnly = false)
            => [];

        public void SaveOpeningTrainingSessionResult(OpeningTrainingSessionResult result) { }

        public IReadOnlyList<OpeningTrainingSessionResult> ListOpeningTrainingSessionResults(string? playerKey = null, int limit = 200)
            => [];

        public void SaveImportedGame(ImportedGame game) { }

        public void SaveImportedGames(IReadOnlyList<ImportedGame> games) { }

        public bool TryLoadImportedGame(string gameFingerprint, out ImportedGame? game)
        {
            game = null;
            return false;
        }

        public bool DeleteImportedGame(string gameFingerprint) => false;

        public IReadOnlyList<SavedImportedGameSummary> ListImportedGames(string? filterText = null, int limit = 200)
            => [];

        public IReadOnlyList<GameAnalysisResult> ListResults(string? filterText = null, int limit = 500)
            => [];

        public bool TryLoadResult(GameAnalysisCacheKey key, out GameAnalysisResult? result)
        {
            result = null;
            return false;
        }

        public void SaveResult(GameAnalysisCacheKey key, GameAnalysisResult result) { }

        public IReadOnlyList<StoredMoveAnalysis> ListMoveAnalyses(string? filterText = null, int limit = 5000)
            => [];
    }
}
