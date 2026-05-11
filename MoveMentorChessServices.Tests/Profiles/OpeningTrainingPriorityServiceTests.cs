using MoveMentorChess.Opening;
using Xunit;

namespace MoveMentorChessServices.Tests;

public sealed class OpeningTrainingPriorityServiceTests
{
    [Fact]
    public void BuildPriorities_RanksWeakPositionsAboveUnseenBranches()
    {
        OpeningLineCatalogItem line = CreateLine();
        OpeningTrainerOverview overview = CreateOverview(
            line,
            branches:
            [
                CreateBranch("d5", "d7d5", 3),
                CreateBranch("Nf6", "g8f6", 2)
            ],
            weakPositions:
            [
                CreateWeakPosition(line, "weak-1", 5)
            ]);
        OpeningTrainingPriorityService service = new();

        IReadOnlyList<TrainingPriorityItem> priorities = service.BuildPriorities(overview, [], []);

        Assert.NotEmpty(priorities);
        Assert.Equal(TrainingPriorityAction.RepairThisPosition, priorities[0].Action);
        Assert.Equal("Repair a missed position", priorities[0].Title);
    }

    [Fact]
    public void BuildPriorities_UsesOpponentMistakesForReplyReview()
    {
        OpeningLineCatalogItem line = CreateLine();
        OpeningTrainingBranch branch = CreateBranch("d5", "d7d5", 3);
        OpeningTrainerOverview overview = CreateOverview(
            line,
            [branch],
            [],
            mistakeCount: 2);
        OpeningTrainingPriorityService service = new();

        TrainingPriorityItem priority = Assert.Single(service.BuildPriorities(overview, [], []));

        Assert.Equal(TrainingPriorityAction.ReviewOpponentReply, priority.Action);
        Assert.Equal(TrainingPriorityReasonCode.DangerousOpponentReply, priority.ReasonCode);
        Assert.Contains("mistake", priority.Evidence, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildGuidedStudySession_TargetsBranchAwarenessPosition()
    {
        OpeningLineCatalogItem line = CreateLine();
        OpeningTrainingBranch branch = CreateBranch("d5", "d7d5", 3);
        OpeningTrainerOverview overview = CreateOverview(line, [branch], []);
        OpeningTrainerWorkspaceService workspace = new(new MinimalAnalysisStore());
        OpeningTrainingSessionTarget target = new(
            "branch:d7d5",
            TrainingPriorityAction.TrainThisBranch,
            line.LineKey,
            branch.BranchKey,
            branch.ResultingPositionKey,
            branch.OpponentMove,
            branch.OpponentMoveUci);

        OpeningTrainingSession session = workspace.BuildGuidedStudySession(
            line,
            overview,
            "Alpha",
            OpeningTrainingStrictness.BookFlexible,
            null,
            target);

        OpeningTrainingPosition position = Assert.Single(session.Positions);
        Assert.Equal(OpeningTrainingMode.BranchAwareness, position.Mode);
        Assert.Equal(branch.BranchKey, position.OpeningBranchKey);
        Assert.Contains(position.Branches!, item => item.BranchKey == branch.BranchKey);
        Assert.Contains(position.CandidateMoves, option => option.Uci == branch.OpponentMoveUci);
    }

    [Fact]
    public void BuildGuidedStudySession_TargetsWeakPosition()
    {
        OpeningLineCatalogItem line = CreateLine();
        OpeningTrainingPosition weakPosition = CreateWeakPosition(line, "weak-1", 5);
        OpeningTrainerOverview overview = CreateOverview(line, [], [weakPosition]);
        OpeningTrainerWorkspaceService workspace = new(new MinimalAnalysisStore());
        OpeningTrainingSessionTarget target = new(
            "position:weak-1",
            TrainingPriorityAction.RepairThisPosition,
            line.LineKey,
            null,
            weakPosition.OpeningPositionKey);

        OpeningTrainingSession session = workspace.BuildGuidedStudySession(
            line,
            overview,
            "Alpha",
            OpeningTrainingStrictness.StrictRepertoire,
            null,
            target);

        OpeningTrainingPosition position = Assert.Single(session.Positions);
        Assert.Equal(weakPosition.PositionId, position.PositionId);
        Assert.Equal(OpeningTrainingMode.MistakeRepair, position.Mode);
        Assert.Equal(OpeningTrainingStrictness.StrictRepertoire, position.Strictness);
        Assert.Contains(position.Tags, tag => tag.Equals("target:RepairThisPosition", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void BuildGuidedStudySession_IncludesPlanSelectionWhenModeRequested()
    {
        OpeningLineCatalogItem line = CreateLine();
        OpeningMoveIdea idea = new(
            "Nf3",
            [OpeningMoveIdeaTag.DevelopPiece],
            "Develop the kingside knight toward the center.");
        OpeningLineMove lineMove = new(
            1,
            1,
            PlayerSide.White,
            "Nf3",
            "g1f3",
            line.RootPositionKey,
            new OpeningPositionKey("after-nf3"),
            true,
            idea);
        OpeningTrainerOverview overview = CreateOverview(line, [], []) with
        {
            MainLine = [lineMove]
        };
        SpecialTrainingModeDefinition mode = new(
            SpecialTrainingModeKind.FiveMinutePrep,
            "Plan drill",
            "Choose the plan.",
            "Start",
            5,
            3,
            OpeningTrainingStrictness.BookFlexible,
            [OpeningTrainingMode.PlanSelection]);
        OpeningTrainerWorkspaceService workspace = new(new MinimalAnalysisStore());

        OpeningTrainingSession session = workspace.BuildGuidedStudySession(
            line,
            overview,
            "Alpha",
            OpeningTrainingStrictness.BookFlexible,
            mode);

        OpeningTrainingPosition plan = Assert.Single(session.Positions, position => position.Mode == OpeningTrainingMode.PlanSelection);
        Assert.Equal(OpeningTrainingAnswerKind.SingleChoice, plan.AnswerKind);
        Assert.NotEmpty(plan.AnswerOptions!);
        Assert.Contains(plan.AnswerOptions!, option => option.IsCorrect);
    }

    [Fact]
    public void BuildGuidedStudySession_RebuildsContinuationAfterPlayableAlternative()
    {
        OpeningLineCatalogItem line = CreateLine();
        ChessGame game = new();
        string rootFen = game.GetFen();
        Assert.True(game.TryApplyUci("e2e4", out AppliedMoveInfo? e4, out _));
        Assert.NotNull(e4);
        ChessGame alternativeGame = new();
        Assert.True(alternativeGame.TryApplyUci("d2d4", out AppliedMoveInfo? d4, out _));
        Assert.NotNull(d4);
        OpeningTheoryMove e4Move = CreateTheoryMove("e4", "e2e4", e4!.FenAfter, true);
        OpeningTheoryMove d4Move = CreateTheoryMove("d4", "d2d4", d4!.FenAfter, false);
        OpeningTheoryMove d5Move = CreateTheoryMove("d5", "d7d5", ApplyMove(d4.FenAfter, "d7d5"), true);
        MinimalAnalysisStore store = new(
            new Dictionary<string, IReadOnlyList<OpeningTheoryMove>>(StringComparer.Ordinal)
            {
                [OpeningPositionKeyBuilder.Build(rootFen)] = [e4Move, d4Move],
                [OpeningPositionKeyBuilder.Build(d4.FenAfter)] = [d5Move]
            });
        OpeningTrainerWorkspaceService workspace = new(store);
        OpeningTrainingPosition first = CreateWeakPosition(line, "line-1", 1) with
        {
            Mode = OpeningTrainingMode.LineRecall,
            Fen = rootFen,
            CandidateMoves =
            [
                new OpeningTrainingMoveOption("e4", "e2e4", OpeningTrainingMoveRole.Expected, true, ToPositionKey: e4Move.ToOpeningPositionKey),
                new OpeningTrainingMoveOption("d4", "d2d4", OpeningTrainingMoveRole.Alternative, false, ToPositionKey: d4Move.ToOpeningPositionKey)
            ]
        };
        OpeningTrainingPosition staleNext = first with
        {
            PositionId = "stale-next",
            Fen = e4.FenAfter,
            BetterMove = "e5"
        };
        OpeningTrainingSession session = new(
            "session-1",
            "alpha",
            "Alpha",
            DateTime.UtcNow,
            OpeningTrainingStyle.Memorization,
            OpeningTrainingStrictness.BookFlexible,
            RepertoireSide.White,
            [OpeningTrainingMode.LineRecall],
            [OpeningTrainingSourceKind.ExampleGame],
            [],
            [],
            [first, staleNext]);
        OpeningTrainingAttemptResult result = workspace.Evaluate(first, "d2d4");

        OpeningTrainingSession rebuilt = workspace.RebuildContinuationAfterAcceptedMove(session, 0, first, result);

        Assert.Equal(OpeningTrainingScore.Playable, result.Score);
        Assert.Equal(d4.FenAfter, rebuilt.Positions[1].Fen);
        Assert.Equal("d5", rebuilt.Positions[1].BetterMove);
    }

    private static OpeningTrainerOverview CreateOverview(
        OpeningLineCatalogItem line,
        IReadOnlyList<OpeningTrainingBranch> branches,
        IReadOnlyList<OpeningTrainingPosition> weakPositions,
        int mistakeCount = 0)
    {
        OpponentReplyProfile opponentProfile = new(
            line.LineKey,
            line.RepertoireSide,
            branches.Select(branch => new OpponentMoveFrequency(
                branch.OpponentMove,
                branch.OpponentMoveUci,
                branch.Frequency,
                branch.Frequency,
                0,
                mistakeCount,
                false,
                OpponentMoveFrequencySourceKind.BookFrequency,
                branch.SourceSummary)).ToList(),
            "Opponent replies.");

        return new OpeningTrainerOverview(
            line.OpeningKey,
            line.LineKey,
            line.RepertoireSide,
            line.Eco,
            line.OpeningName,
            line.VariationName,
            [],
            branches,
            opponentProfile,
            new OpeningCoverageSummary(branches.Count, 0, branches.Count, branches.Count, 0, 0, 0, 0),
            [],
            weakPositions,
            []);
    }

    private static OpeningLineCatalogItem CreateLine()
    {
        return new OpeningLineCatalogItem(
            new OpeningKey("B12"),
            new OpeningLineKey("B12:advance"),
            RepertoireSide.White,
            "B12",
            "Caro-Kann",
            "Advance",
            "Caro-Kann Advance",
            new OpeningPositionKey("root"),
            new ChessGame().GetFen(),
            20,
            3);
    }

    private static OpeningTrainingBranch CreateBranch(string san, string uci, int frequency)
    {
        return new OpeningTrainingBranch(
            new OpeningBranchKey(uci),
            san,
            uci,
            frequency,
            "Book branch",
            new OpeningTrainingMoveOption("c4", "c2c4", OpeningTrainingMoveRole.Expected, true),
            [],
            [],
            new OpeningPositionKey($"{uci}:position"));
    }

    private static OpeningTrainingPosition CreateWeakPosition(OpeningLineCatalogItem line, string id, int priority)
    {
        return new OpeningTrainingPosition(
            id,
            line.OpeningKey,
            line.LineKey,
            null,
            new OpeningPositionKey($"{id}:position"),
            OpeningTrainingMode.MistakeRepair,
            OpeningTrainingSourceKind.FirstOpeningMistake,
            line.Eco,
            line.OpeningName,
            line.RootFen,
            1,
            1,
            PlayerSide.White,
            "Repair this position.",
            "Previously missed opening position.",
            priority,
            line.RepertoireSide,
            OpeningTrainingStrictness.BookFlexible,
            null,
            "h4",
            "Nf3",
            "Develop before pushing flank pawns.",
            ["history-review"],
            [],
            [],
            new OpeningTrainingReference(string.Empty, PlayerSide.White, "History", null, null, "Training history", 1, null),
            line.LineKey.Value);
    }

    private static OpeningTheoryMove CreateTheoryMove(string san, string uci, string fenAfter, bool isMain)
    {
        OpeningPositionKey toKey = OpeningPositionKeyBuilder.BuildKey(fenAfter);
        return new OpeningTheoryMove(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            uci,
            san,
            1,
            1,
            isMain,
            true,
            isMain ? 1 : 2,
            toKey.Value,
            toKey,
            fenAfter,
            new OpeningGameMetadata("B12", "Caro-Kann", "Advance"));
    }

    private static string ApplyMove(string fen, string uci)
    {
        ChessGame game = new();
        Assert.True(game.TryLoadFen(fen, out _));
        Assert.True(game.TryApplyUci(uci, out AppliedMoveInfo? applied, out _));
        Assert.NotNull(applied);
        return applied!.FenAfter;
    }

    private sealed class MinimalAnalysisStore : IAnalysisStore, IOpeningTheoryStore
    {
        private readonly IReadOnlyDictionary<string, IReadOnlyList<OpeningTheoryMove>> theoryMoves;

        public MinimalAnalysisStore()
            : this(new Dictionary<string, IReadOnlyList<OpeningTheoryMove>>(StringComparer.Ordinal))
        {
        }

        public MinimalAnalysisStore(IReadOnlyDictionary<string, IReadOnlyList<OpeningTheoryMove>> theoryMoves)
        {
            this.theoryMoves = theoryMoves;
        }

        public bool TryGetOpeningPositionByKey(string positionKey, out OpeningTheoryPosition? position)
        {
            position = null;
            return false;
        }

        public IReadOnlyList<OpeningTheoryMove> GetOpeningMovesByPositionKey(string positionKey, int limit = 10, bool playableOnly = false)
            => theoryMoves.TryGetValue(positionKey, out IReadOnlyList<OpeningTheoryMove>? moves)
                ? moves.Take(limit).ToList()
                : [];

        public void SaveImportedGame(ImportedGame game) => throw new NotSupportedException();
        public void SaveImportedGames(IReadOnlyList<ImportedGame> games) => throw new NotSupportedException();
        public bool TryLoadImportedGame(string gameFingerprint, out ImportedGame? game) => throw new NotSupportedException();
        public bool DeleteImportedGame(string gameFingerprint) => throw new NotSupportedException();
        public IReadOnlyList<SavedImportedGameSummary> ListImportedGames(string? filterText = null, int limit = 200) => [];
        public IReadOnlyList<GameAnalysisResult> ListResults(string? filterText = null, int limit = 500) => [];
        public bool TryLoadResult(GameAnalysisCacheKey key, out GameAnalysisResult? result) => throw new NotSupportedException();
        public void SaveResult(GameAnalysisCacheKey key, GameAnalysisResult result) => throw new NotSupportedException();
        public IReadOnlyList<StoredMoveAnalysis> ListMoveAnalyses(string? filterText = null, int limit = 5000) => [];
        public bool TryLoadWindowState(string gameFingerprint, out AnalysisWindowState? state) => throw new NotSupportedException();
        public void SaveWindowState(string gameFingerprint, AnalysisWindowState state) => throw new NotSupportedException();
    }
}
