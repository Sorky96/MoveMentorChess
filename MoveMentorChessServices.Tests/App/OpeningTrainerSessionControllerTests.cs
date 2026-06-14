using MoveMentorChess.App.ViewModels;
using MoveMentorChess.Persistence;
using Xunit;

namespace MoveMentorChessServices.Tests.App;

public sealed class OpeningTrainerSessionControllerTests
{
    private const string StartFen = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";

    [Fact]
    public void MoveNext_ResetsHintProgressionForNewPosition()
    {
        OpeningTrainerWorkspaceService workspace = new(
            new EmptyAnalysisStore(),
            new EmptyAnalysisStore(),
            new EmptyAnalysisStore());
        CapturingCallbacks callbacks = new();
        OpeningTrainerSessionController controller = new(workspace, callbacks);
        OpeningTrainingSession session = CreateSession(
            CreatePosition(
                "position-1",
                [
                    new TrainingCoachHint(TrainingCoachHintLevel.Light, "First light", "First light hint."),
                    new TrainingCoachHint(TrainingCoachHintLevel.Plan, "First plan", "First plan hint.")
                ]),
            CreatePosition(
                "position-2",
                [
                    new TrainingCoachHint(TrainingCoachHintLevel.Light, "Second light", "Second light hint."),
                    new TrainingCoachHint(TrainingCoachHintLevel.Plan, "Second plan", "Second plan hint.")
                ]));

        controller.StartSession(session, "test", recommendationId: null);
        Assert.Equal(TrainingCoachHintLevel.Light, controller.ShowNextHint().Hint?.Level);
        Assert.Equal(TrainingCoachHintLevel.Plan, controller.ShowNextHint().Hint?.Level);

        controller.MoveNext();

        OpeningTrainerSessionController.HintResult nextPositionHint = controller.ShowNextHint();
        Assert.Equal("position-2", callbacks.LastPositionId);
        Assert.Equal(TrainingCoachHintLevel.Light, nextPositionHint.Hint?.Level);
    }

    private static OpeningTrainingSession CreateSession(params OpeningTrainingPosition[] positions)
    {
        return new OpeningTrainingSession(
            "session-1",
            "player",
            "Player",
            new DateTime(2026, 5, 23, 12, 0, 0, DateTimeKind.Utc),
            [OpeningTrainingMode.LineRecall],
            [OpeningTrainingSourceKind.ExampleGame],
            [],
            [],
            positions);
    }

    private static OpeningTrainingPosition CreatePosition(string positionId, IReadOnlyList<TrainingCoachHint> hints)
    {
        return new OpeningTrainingPosition(
            positionId,
            OpeningTrainingMode.LineRecall,
            OpeningTrainingSourceKind.ExampleGame,
            "B01",
            "Scandinavian Defense",
            StartFen,
            1,
            1,
            PlayerSide.White,
            "Prompt",
            "Instruction",
            100,
            null,
            null,
            "e4",
            "Keep the repertoire connected.",
            [],
            [new OpeningTrainingMoveOption("e4", "e2e4", OpeningTrainingMoveRole.Expected, true, "Book move")],
            [],
            new OpeningTrainingReference("game", PlayerSide.White, "Opponent", null, null, "Example", null, null),
            positionId) with
        {
            CoachHints = hints
        };
    }

    private sealed class CapturingCallbacks : OpeningTrainerSessionController.ISessionFlowCallbacks
    {
        public string PlayerKey => "player";
        public string? LastPositionId { get; private set; }

        public void OnStepLoaded(OpeningTrainingPosition? position, int stepIndex, OpeningTrainingSession? session)
        {
            LastPositionId = position?.PositionId;
        }

        public void OnStudyNavigationStateChanged()
        {
        }

        public void OnSessionCompleted(
            TrainingSessionOutcomeSummary summary,
            OpeningTrainingSession? session,
            OpeningTrainingSessionResult? savedResult,
            string? recommendationId,
            string? startSource,
            int completedSteps,
            int wrongAttempts,
            int hintUseCount,
            int dontKnowCount,
            int timeToFirstMoveSeconds,
            IReadOnlyList<OpeningTrainingAttemptResult> attempts)
        {
        }

        public void OnSessionAbandoned(
            OpeningTrainingSession session,
            string? recommendationId,
            string? startSource,
            int completedSteps,
            int timeToFirstMoveSeconds)
        {
        }
    }

    private sealed class EmptyAnalysisStore :
        IImportedGameStore,
        IAnalysisResultStore,
        IStoredMoveAnalysisStore
    {
        public void SaveImportedGame(ImportedGame game) { }
        public void SaveImportedGames(IReadOnlyList<ImportedGame> games) { }
        public bool TryLoadImportedGame(string gameFingerprint, out ImportedGame? game) { game = null; return false; }
        public bool DeleteImportedGame(string gameFingerprint) => false;
        public void ClearImportedAnalysisData() { }
        public IReadOnlyList<SavedImportedGameSummary> ListImportedGames(string? filterText = null, int limit = 200) => [];
        public IReadOnlyList<GameAnalysisResult> ListResults(string? filterText = null, int limit = 500) => [];
        public bool TryLoadResult(GameAnalysisCacheKey key, out GameAnalysisResult? result) { result = null; return false; }
        public void SaveResult(GameAnalysisCacheKey key, GameAnalysisResult result) { }
        public IReadOnlyList<StoredMoveAnalysis> ListMoveAnalyses(string? filterText = null, int limit = 5000) => [];
    }
}
