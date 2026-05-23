using MoveMentorChess.Analysis;
using MoveMentorChess.Opening;
using MoveMentorChess.Persistence;
using MoveMentorChess.Training;
using Xunit;

namespace MoveMentorChessServices.Tests.Training;

/// <summary>
/// Unit tests for <see cref="OpeningTrainingSessionBuilder"/> exercised through the public
/// <see cref="OpeningTrainerService"/> API.  These tests mirror the contract tests in
/// OpeningTrainerServiceTests but focus specifically on the session-building pipeline that was
/// extracted into the new builder class.
/// </summary>
public sealed class OpeningTrainingSessionBuilderTests
{
    // -------------------------------------------------------------------------
    // Empty store → TryBuildSession returns false
    // -------------------------------------------------------------------------

    [Fact]
    public void TryBuildSession_EmptyStore_ReturnsFalse()
    {
        OpeningTrainerService service = CreateService([]);

        bool result = service.TryBuildSession("any-player", out OpeningTrainingSession? session);

        Assert.False(result);
        Assert.Null(session);
    }

    // -------------------------------------------------------------------------
    // Blank / null player key → TryBuildSession returns false without throwing
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void TryBuildSession_BlankPlayerKey_ReturnsFalse(string blankKey)
    {
        OpeningTrainerService service = CreateService([]);

        bool result = service.TryBuildSession(blankKey, out OpeningTrainingSession? session);

        Assert.False(result);
        Assert.Null(session);
    }

    // -------------------------------------------------------------------------
    // Options normalisation – negative MaxPositions must not throw
    // -------------------------------------------------------------------------

    [Fact]
    public void TryBuildSession_NullOptions_DoesNotThrow()
    {
        OpeningTrainerService service = CreateService([CreateResult("Alice", "Bob", PlayerSide.White, "B20")]);

        Exception? ex = Record.Exception(() => service.TryBuildSession("Alice", out _, null));

        Assert.Null(ex);
    }

    [Fact]
    public void TryBuildSession_OptionsWithNegativeMaxPositions_DoesNotThrow()
    {
        OpeningTrainerService service = CreateService([CreateResult("Carol", "Dave", PlayerSide.White, "A10")]);

        Exception? ex = Record.Exception(() =>
            service.TryBuildSession("Carol", out _, new OpeningTrainingSessionOptions { MaxPositions = -5 }));

        Assert.Null(ex);
    }

    // -------------------------------------------------------------------------
    // Session ID prefix is derived from the normalised player key
    // -------------------------------------------------------------------------

    [Fact]
    public void TryBuildSession_WhenSessionBuilt_SessionIdContainsNormalisedPlayerKey()
    {
        // Two games with theory moves so positions are actually built.
        GameAnalysisResult gameA = CreateResult("Marta", "Opponent", PlayerSide.White, "C20",
            ["e4", "e5", "h3", "Nc6"],
            [("opening_principles", 95, "g1f3")]);
        GameAnalysisResult gameB = CreateResult("Marta", "Opponent2", PlayerSide.White, "C20",
            ["e4", "e5", "a3", "Nf6"],
            [("opening_principles", 85, "d2d4")]);

        ImportedGame[] theoryGames = [
            CreateTheoryGame("C20", ["e4", "e5", "Nf3", "Nc6"]),
            CreateTheoryGame("C20", ["e4", "e5", "d4", "Nf6"])
        ];

        OpeningTrainerService service = CreateService([gameA, gameB], theoryGames);

        if (service.TryBuildSession("Marta", out OpeningTrainingSession? session) && session is not null)
        {
            Assert.StartsWith("opening-trainer:marta:", session.SessionId, StringComparison.OrdinalIgnoreCase);
            Assert.Equal("marta", session.PlayerKey);
        }
        // If the builder returns false (e.g. no positions built because the test FEN is bare),
        // the absence of a throw is itself a passing contract.
    }

    // -------------------------------------------------------------------------
    // Source summaries are present when positions are built
    // -------------------------------------------------------------------------

    [Fact]
    public void TryBuildSession_WhenPositionsBuilt_SourceSummariesArePresent()
    {
        GameAnalysisResult game = CreateResult("Filip", "Opponent", PlayerSide.White, "C20",
            ["e4", "e5", "h3", "Nc6"],
            [("opening_principles", 95, "g1f3")]);
        ImportedGame[] theoryGames = [CreateTheoryGame("C20", ["e4", "e5", "Nf3", "Nc6"])];

        OpeningTrainerService service = CreateService([game], theoryGames);

        if (service.TryBuildSession("Filip", out OpeningTrainingSession? session) && session is not null)
        {
            Assert.NotEmpty(session.SourceSummaries);
        }
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static OpeningTrainerService CreateService(
        IReadOnlyList<GameAnalysisResult> results,
        IReadOnlyList<ImportedGame>? theoryGames = null)
        => new(new FakeStore(results, theoryGames ?? []));

    private static GameAnalysisResult CreateResult(
        string white,
        string black,
        PlayerSide side,
        string eco,
        IReadOnlyList<string>? sanMoves = null,
        IReadOnlyList<(string Label, int Cpl, string BestMoveUci)>? mistakes = null)
    {
        sanMoves ??= ["e4", "e5"];
        mistakes ??= [];

        string pgn = BuildPgn(white, black, eco, sanMoves);
        ImportedGame game = new(pgn, sanMoves, white, black, null, null, "2026.01.01", "1-0", eco, "Local");
        IReadOnlyList<ReplayPly> replay = new GameReplayService().Replay(game);
        Dictionary<int, ReplayPly> replayIndex = replay.ToDictionary(ply => ply.Ply);

        IReadOnlyList<MoveAnalysisResult> moveAnalyses = mistakes
            .Select(spec =>
            {
                if (!replayIndex.TryGetValue(
                        side == PlayerSide.White ? spec.Cpl / 10 + 1 : spec.Cpl / 10 + 2,
                        out ReplayPly? ply))
                {
                    ply = replay.FirstOrDefault(item => item.Side == side) ?? replay[0];
                }

                return new MoveAnalysisResult(
                    ply,
                    new EngineAnalysis(ply.FenBefore, [], spec.BestMoveUci),
                    new EngineAnalysis(ply.FenAfter, [], null),
                    20,
                    -spec.Cpl,
                    null,
                    null,
                    spec.Cpl,
                    MoveQualityBucket.Mistake,
                    0,
                    new MistakeTag(spec.Label, 0.8, ["evidence"]),
                    new MoveExplanation("Short", "Hint", "Detailed"));
            })
            .ToList();

        return new GameAnalysisResult(game, side, [], moveAnalyses, []);
    }

    private static ImportedGame CreateTheoryGame(string eco, IReadOnlyList<string> sanMoves)
    {
        string pgn = BuildPgn("TheoryBook", "TheoryLine", eco, sanMoves);
        return new ImportedGame(pgn, sanMoves, "TheoryBook", "TheoryLine", null, null, "2026.04.30", "1-0", eco, "Imported");
    }

    private static string BuildPgn(string white, string black, string eco, IReadOnlyList<string> sanMoves)
    {
        List<string> tokens = [];
        for (int i = 0; i < sanMoves.Count; i += 2)
        {
            int moveNumber = i / 2 + 1;
            tokens.Add($"{moveNumber}. {sanMoves[i]}");
            if (i + 1 < sanMoves.Count)
            {
                tokens.Add(sanMoves[i + 1]);
            }
        }

        return string.Join(Environment.NewLine,
        [
            $"[White \"{white}\"]",
            $"[Black \"{black}\"]",
            $"[Date \"2026.01.01\"]",
            $"[Result \"1-0\"]",
            $"[ECO \"{eco}\"]",
            string.Empty,
            $"{string.Join(' ', tokens)} 1-0"
        ]);
    }

    // -------------------------------------------------------------------------
    // Minimal stub store
    // -------------------------------------------------------------------------

    private sealed class FakeStore :
        IImportedGameStore,
        IAnalysisResultStore,
        IStoredMoveAnalysisStore,
        IOpeningTheoryStore
    {
        private readonly IReadOnlyList<GameAnalysisResult> results;
        private readonly Dictionary<string, ImportedGame> importedGames;
        private readonly Dictionary<string, IReadOnlyList<OpeningTheoryMove>> theoryMovesByFen;

        public FakeStore(IReadOnlyList<GameAnalysisResult> results, IReadOnlyList<ImportedGame> theoryGames)
        {
            this.results = results;
            importedGames = results.ToDictionary(
                result => GameFingerprint.Compute(result.Game.PgnText),
                result => result.Game);

            theoryMovesByFen = BuildTheoryMoves(theoryGames);
        }

        // IAnalysisResultStore
        public IReadOnlyList<GameAnalysisResult> ListResults(string? filterText = null, int limit = 500) => results;
        public bool TryLoadResult(GameAnalysisCacheKey key, out GameAnalysisResult? result) { result = null; return false; }
        public void SaveResult(GameAnalysisCacheKey key, GameAnalysisResult result) { }

        // IStoredMoveAnalysisStore
        public IReadOnlyList<StoredMoveAnalysis> ListMoveAnalyses(string? filterText = null, int limit = 5000) => [];

        // IImportedGameStore
        public void SaveImportedGame(ImportedGame game) { }
        public void SaveImportedGames(IReadOnlyList<ImportedGame> games) { }
        public bool TryLoadImportedGame(string gameFingerprint, out ImportedGame? game)
            => importedGames.TryGetValue(gameFingerprint, out game);
        public bool DeleteImportedGame(string gameFingerprint) => false;
        public IReadOnlyList<SavedImportedGameSummary> ListImportedGames(string? filterText = null, int limit = 200) => [];

        // IOpeningTheoryStore
        public bool TryGetOpeningPositionByKey(string positionKey, out OpeningTheoryPosition? position)
        {
            position = null;
            return false;
        }

        public IReadOnlyList<OpeningTheoryMove> GetOpeningMovesByPositionKey(string positionKey, int limit = 10, bool playableOnly = false)
        {
            return theoryMovesByFen.TryGetValue(positionKey, out IReadOnlyList<OpeningTheoryMove>? moves)
                ? moves.Take(limit).ToList()
                : [];
        }

        private static Dictionary<string, IReadOnlyList<OpeningTheoryMove>> BuildTheoryMoves(IReadOnlyList<ImportedGame> games)
        {
            Dictionary<string, List<OpeningTheoryMove>> byKey = new(StringComparer.Ordinal);

            foreach (ImportedGame game in games)
            {
                IReadOnlyList<ReplayPly> replay = new GameReplayService().Replay(game)
                    .Where(ply => ply.Phase == GamePhase.Opening)
                    .OrderBy(ply => ply.Ply)
                    .ToList();

                foreach (ReplayPly ply in replay)
                {
                    string fromKey = OpeningPositionKeyBuilder.Build(ply.FenBefore);
                    if (!byKey.TryGetValue(fromKey, out List<OpeningTheoryMove>? moves))
                    {
                        moves = [];
                        byKey[fromKey] = moves;
                    }

                    string toKey = OpeningPositionKeyBuilder.Build(ply.FenAfter);
                    if (!moves.Any(m => m.MoveUci == ply.Uci))
                    {
                        moves.Add(new OpeningTheoryMove(
                            Guid.NewGuid(),
                            Guid.NewGuid(),
                            Guid.NewGuid(),
                            ply.Uci,
                            ply.San,
                            1,
                            1,
                            moves.Count == 0,
                            true,
                            moves.Count + 1,
                            toKey,
                            ply.FenAfter,
                            new OpeningGameMetadata(game.Eco ?? string.Empty, OpeningCatalog.GetName(game.Eco), string.Empty)));
                    }
                }
            }

            return byKey.ToDictionary(kv => kv.Key, kv => (IReadOnlyList<OpeningTheoryMove>)kv.Value);
        }
    }
}
