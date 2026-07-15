using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media.Imaging;
using Avalonia.Skia;
using Avalonia.Threading;
using System.Globalization;
using MoveMentorChess.App;
using MoveMentorChess.App.Composition;
using MoveMentorChess.App.ViewModels;
using MoveMentorChess.App.Views;
using MoveMentorChess.Domain;
using MoveMentorChess.Persistence;
using MoveMentorChess.Profiles;
using MoveMentorChess.Training;

namespace MoveMentorChess.App.Snapshots;

internal static class Program
{
    private static readonly SnapshotSize[] DefaultSizes =
    [
        new("desktop-1366x768", 1366, 768),
        new("desktop-1440x900", 1440, 900),
        new("desktop-1920x1080", 1920, 1080),
        new("desktop-2560x1440", 2560, 1440)
    ];

    public static int Main(string[] args)
    {
        SnapshotOptions options = SnapshotOptions.Parse(args);
        Directory.CreateDirectory(options.OutputDirectory);

        IAnalysisStore? store = TryCreateStore(options.DatabasePath);
        SnapshotData data = SnapshotData.Load(store);
        List<string> savedFiles = [];

        using HeadlessUnitTestSession session = HeadlessUnitTestSession.StartNew(typeof(SnapshotApplicationHost));
        session.Dispatch(() =>
        {
            foreach (string view in options.Views)
            {
                foreach (SnapshotSize size in options.Sizes)
                {
                    Window? window = SnapshotWindowFactory.Create(view, data, store);
                    if (window is null)
                    {
                        Console.WriteLine($"Skipped {view}: no scenario could be created.");
                        continue;
                    }

                    string outputPath = Path.Combine(options.OutputDirectory, $"{view}-{size.Name}.png");
                    RenderWindow(window, size, outputPath, options.SettleMilliseconds);
                    savedFiles.Add(outputPath);
                    Console.WriteLine($"Saved {outputPath}");
                }
            }
        }, CancellationToken.None).GetAwaiter().GetResult();

        Console.WriteLine($"Rendered {savedFiles.Count} snapshot(s) into {options.OutputDirectory}.");
        return savedFiles.Count == 0 ? 1 : 0;
    }

    private static IAnalysisStore? TryCreateStore(string? databasePath)
    {
        bool hasExplicitDatabasePath = !string.IsNullOrWhiteSpace(databasePath);
        string resolvedPath = string.IsNullOrWhiteSpace(databasePath)
            ? SqliteAnalysisStore.GetDefaultDatabasePath()
            : databasePath;

        if (!File.Exists(resolvedPath))
        {
            if (hasExplicitDatabasePath)
            {
                throw new FileNotFoundException(
                    $"The analysis database '{resolvedPath}' was not found.",
                    resolvedPath);
            }

            Console.WriteLine("No analysis database was found. Rendering built-in snapshot scenarios.");
            return SnapshotFixtureStore.Create();
        }

        try
        {
            return new SqliteAnalysisStore(
                resolvedPath,
                applyDerivedAnalysisDataVersionPolicy: false);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            if (hasExplicitDatabasePath)
            {
                throw new InvalidOperationException(
                    $"Could not open analysis database '{resolvedPath}': {ex.Message}",
                    ex);
            }

            Console.WriteLine($"Could not open analysis database: {ex.Message}");
            Console.WriteLine("Rendering built-in snapshot scenarios instead.");
            return SnapshotFixtureStore.Create();
        }
    }

    private static void RenderWindow(Window window, SnapshotSize size, string outputPath, int settleMilliseconds)
    {
        window.WindowState = WindowState.Normal;
        window.WindowDecorations = WindowDecorations.None;
        window.Width = size.Width;
        window.Height = size.Height;
        window.Show();

        try
        {
            WaitForRenderWork(settleMilliseconds);

            using WriteableBitmap frame = window.CaptureRenderedFrame()
                ?? throw new InvalidOperationException($"Could not capture {window.GetType().Name}.");
            frame.Save(outputPath, PngBitmapEncoderOptions.Default);
        }
        finally
        {
            window.Close();
            Dispatcher.UIThread.RunJobs();
        }
    }

    private static void WaitForRenderWork(int settleMilliseconds)
    {
        int iterations = Math.Max(1, settleMilliseconds / 100);
        for (int i = 0; i < iterations; i++)
        {
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            Thread.Sleep(100);
        }
    }

    private sealed record SnapshotSize(string Name, int Width, int Height);

    private sealed class SnapshotOptions
    {
        private SnapshotOptions(
            string outputDirectory,
            string? databasePath,
            IReadOnlyList<string> views,
            IReadOnlyList<SnapshotSize> sizes,
            int settleMilliseconds)
        {
            OutputDirectory = outputDirectory;
            DatabasePath = databasePath;
            Views = views;
            Sizes = sizes;
            SettleMilliseconds = settleMilliseconds;
        }

        public string OutputDirectory { get; }

        public string? DatabasePath { get; }

        public IReadOnlyList<string> Views { get; }

        public IReadOnlyList<SnapshotSize> Sizes { get; }

        public int SettleMilliseconds { get; }

        public static SnapshotOptions Parse(string[] args)
        {
            string output = Path.Combine("artifacts", "view-snapshots");
            string? database = null;
            IReadOnlyList<string> views = SnapshotWindowFactory.KnownViews;
            IReadOnlyList<SnapshotSize> sizes = DefaultSizes;
            int settleMilliseconds = 8000;

            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i];
                if (arg == "--output" && i + 1 < args.Length)
                {
                    output = args[++i];
                }
                else if (arg == "--database" && i + 1 < args.Length)
                {
                    database = args[++i];
                }
                else if (arg == "--views" && i + 1 < args.Length)
                {
                    views = args[++i]
                        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                        .ToList();
                }
                else if (arg == "--sizes" && i + 1 < args.Length)
                {
                    sizes = ParseSizes(args[++i]);
                }
                else if (arg == "--settle-ms" && i + 1 < args.Length)
                {
                    string raw = args[++i];
                    if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)
                        || parsed < 0)
                    {
                        throw new ArgumentException("Invalid value for --settle-ms. Use a positive integer.");
                    }

                    settleMilliseconds = Math.Max(100, parsed);
                }
            }

            return new SnapshotOptions(output, database, views, sizes, settleMilliseconds);
        }

        private static IReadOnlyList<SnapshotSize> ParseSizes(string value)
        {
            List<SnapshotSize> parsed = [];
            foreach (string part in value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                string[] dimensions = part.Split('x', 'X');
                if (dimensions.Length != 2
                    || !int.TryParse(dimensions[0], out int width)
                    || !int.TryParse(dimensions[1], out int height)
                    || width <= 0
                    || height <= 0)
                {
                    throw new ArgumentException($"Invalid size '{part}'. Use WIDTHxHEIGHT.");
                }

                parsed.Add(new SnapshotSize($"{width}x{height}", width, height));
            }

            return parsed.Count == 0 ? DefaultSizes : parsed;
        }
    }
}

internal static class SnapshotApplicationHost
{
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions
            {
                UseHeadlessDrawing = false
            });
}

internal sealed class SnapshotData
{
    private SnapshotData(
        ImportedGame? richestImportedGame,
        GameAnalysisResult? richestAnalysisResult)
    {
        RichestImportedGame = richestImportedGame;
        RichestAnalysisResult = richestAnalysisResult;
    }

    public ImportedGame? RichestImportedGame { get; }

    public GameAnalysisResult? RichestAnalysisResult { get; }

    public static SnapshotData Load(IAnalysisStore? store)
    {
        if (store is null)
        {
            return new SnapshotData(null, null);
        }

        GameAnalysisResult? richestResult = TryListResults(store)
            .OrderByDescending(result => result.HighlightedMistakes.Count)
            .ThenByDescending(result => result.MoveAnalyses.Count)
            .ThenByDescending(result => result.Replay.Count)
            .FirstOrDefault();

        ImportedGame? richestGame = richestResult?.Game ?? TryListImportedGames(store)
            .Select(summary =>
            {
                store.TryLoadImportedGame(summary.GameFingerprint, out ImportedGame? game);
                return game;
            })
            .Where(game => game is not null)
            .OrderByDescending(game => game!.SanMoves.Count)
            .FirstOrDefault();

        return new SnapshotData(richestGame, richestResult);
    }

    private static IReadOnlyList<GameAnalysisResult> TryListResults(IAnalysisStore store)
    {
        try
        {
            return store.ListResults(null, 500);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Console.WriteLine($"Could not list saved analysis results: {ex.Message}");
            return [];
        }
    }

    private static IReadOnlyList<SavedImportedGameSummary> TryListImportedGames(IAnalysisStore store)
    {
        try
        {
            return store.ListImportedGames(null, 500);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Console.WriteLine($"Could not list imported games: {ex.Message}");
            return [];
        }
    }
}

internal sealed class SnapshotFixtureStore :
    IAnalysisStore,
    IOpeningTheoryStore,
    IOpeningLineContextStore,
    IOpeningTrainingHistoryStore,
    IOpeningTrainingTelemetryStore,
    IOpeningTreeStore
{
    private static readonly DateTime FixtureNowUtc = new(2026, 06, 06, 12, 00, 00, DateTimeKind.Utc);
    private const string StartFen = "rn1qkbnr/ppp2ppp/4p3/3pP3/3P4/2N2N2/PPP2PPP/R1BQKB1R b KQkq - 1 5";
    private const string AfterC5Fen = "rn1qkbnr/pp3ppp/4p3/2ppP3/3P4/2N2N2/PPP2PPP/R1BQKB1R w KQkq c6 0 6";

    private readonly ImportedGame game;
    private readonly string fingerprint;
    private readonly GameAnalysisResult result;
    private readonly IReadOnlyList<StoredMoveAnalysis> moveAnalyses;
    private readonly IReadOnlyList<OpeningLineCatalogItem> lines;
    private readonly Dictionary<OpeningLineKey, OpeningTrainerOverview> overviews;
    private readonly IReadOnlyList<OpeningTrainingSessionResult> sessions;
    private readonly IReadOnlyList<OpeningReviewItem> reviewItems;

    private SnapshotFixtureStore()
    {
        game = CreateGame();
        fingerprint = GameFingerprint.Compute(game.PgnText);
        result = CreateAnalysisResult(game);
        moveAnalyses = CreateStoredMoveAnalyses();
        lines = CreateLines();
        overviews = lines.ToDictionary(line => line.LineKey, CreateOverview);
        reviewItems = CreateReviewItems();
        sessions = CreateSessions();
    }

    public static SnapshotFixtureStore Create() => new();

    public void SaveImportedGame(ImportedGame importedGame)
    {
    }

    public void SaveImportedGames(IReadOnlyList<ImportedGame> games)
    {
    }

    public bool TryLoadImportedGame(string gameFingerprint, out ImportedGame? importedGame)
    {
        importedGame = gameFingerprint == fingerprint ? this.game : null;
        return importedGame is not null;
    }

    public bool DeleteImportedGame(string gameFingerprint) => false;

    public IReadOnlyList<SavedImportedGameSummary> ListImportedGames(string? filterText = null, int limit = 200)
        => MatchesFilter(filterText, "Snapshot Player")
            ? [new SavedImportedGameSummary(
                fingerprint,
                "Snapshot Player vs Training Partner",
                game.WhitePlayer,
                game.BlackPlayer,
                game.DateText,
                game.Result,
                game.Eco,
                game.Site,
                game.WhiteElo,
                game.BlackElo,
                game.Metadata?.TimeControl,
                game.Metadata?.TimeControlCategory ?? GameTimeControlCategory.Unknown,
                FixtureNowUtc)]
            : [];

    public IReadOnlyList<GameAnalysisResult> ListResults(string? filterText = null, int limit = 500)
        => MatchesFilter(filterText, "Snapshot Player") ? [result] : [];

    public bool TryLoadResult(GameAnalysisCacheKey key, out GameAnalysisResult? analysisResult)
    {
        analysisResult = key.GameFingerprint == fingerprint ? this.result : null;
        return analysisResult is not null;
    }

    public void SaveResult(GameAnalysisCacheKey key, GameAnalysisResult analysisResult)
    {
    }

    public IReadOnlyList<StoredMoveAnalysis> ListMoveAnalyses(string? filterText = null, int limit = 5000)
        => moveAnalyses
            .Where(move => MatchesFilter(filterText, move.Game.WhitePlayer)
                || MatchesFilter(filterText, move.Game.BlackPlayer)
                || MatchesFilter(filterText, move.Game.Eco)
                || MatchesFilter(filterText, move.Advice.MistakeLabel))
            .Take(limit)
            .ToList();

    public bool TryLoadWindowState(string gameFingerprint, out AnalysisWindowState? state)
    {
        state = gameFingerprint == fingerprint ? new AnalysisWindowState(PlayerSide.White, 0, 1) : null;
        return state is not null;
    }

    public void SaveWindowState(string gameFingerprint, AnalysisWindowState state)
    {
    }

    public bool TryGetOpeningPositionByKey(string positionKey, out OpeningTheoryPosition? position)
    {
        position = new OpeningTheoryPosition(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            positionKey,
            positionKey,
            8,
            5,
            "black",
            42,
            18,
            new OpeningGameMetadata("C02", "French Defense", "Advance Variation"));
        return true;
    }

    public IReadOnlyList<OpeningTheoryMove> GetOpeningMovesByPositionKey(
        string positionKey,
        int limit = 10,
        bool playableOnly = false)
        => [
            new OpeningTheoryMove(
                Guid.Parse("22222222-2222-2222-2222-222222222222"),
                Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Guid.Parse("33333333-3333-3333-3333-333333333333"),
                "c7c5",
                "c5",
                28,
                14,
                true,
                true,
                1,
                "after-c5",
                AfterC5Fen,
                new OpeningGameMetadata("C02", "French Defense", "Advance Variation")),
            new OpeningTheoryMove(
                Guid.Parse("44444444-4444-4444-4444-444444444444"),
                Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Guid.Parse("55555555-5555-5555-5555-555555555555"),
                "b8c6",
                "Nc6",
                14,
                8,
                false,
                true,
                2,
                "after-nc6",
                StartFen,
                new OpeningGameMetadata("C02", "French Defense", "Advance Variation"))
        ];

    public IReadOnlyList<OpeningLineCatalogItem> ListOpeningLines(
        string? filterText = null,
        RepertoireSide? repertoireSide = null,
        int limit = 100)
        => lines
            .Where(line => repertoireSide is null
                || repertoireSide == RepertoireSide.Both
                || line.RepertoireSide == repertoireSide
                || line.RepertoireSide == RepertoireSide.Both)
            .Where(line => MatchesFilter(filterText, line.DisplayName)
                || MatchesFilter(filterText, line.Eco)
                || MatchesFilter(filterText, line.OpeningName))
            .Take(limit)
            .ToList();

    public bool TryGetOpeningOverview(
        OpeningLineKey lineKey,
        RepertoireSide repertoireSide,
        int maxDepth,
        out OpeningTrainerOverview? overview)
        => overviews.TryGetValue(lineKey, out overview);

    public IReadOnlyList<string> GetOpeningValidationMoves(OpeningPositionKey rootPositionKey)
        => rootPositionKey.Value.StartsWith("caro", StringComparison.OrdinalIgnoreCase)
            ? ["e4", "c6", "d4", "d5", "e5"]
            : ["e4", "e6", "d4", "d5", "e5"];

    public IReadOnlyList<OpeningLineMove> GetOpeningPathLineMoves(OpeningPositionKey rootPositionKey)
        => rootPositionKey.Value.StartsWith("caro", StringComparison.OrdinalIgnoreCase)
            ? [
                new OpeningLineMove(1, 1, PlayerSide.White, "e4", "e2e4", new OpeningPositionKey("start"), new OpeningPositionKey("after-e4"), true),
                new OpeningLineMove(2, 1, PlayerSide.Black, "c6", "c7c6", new OpeningPositionKey("after-e4"), new OpeningPositionKey("after-c6"), true),
                new OpeningLineMove(3, 2, PlayerSide.White, "d4", "d2d4", new OpeningPositionKey("after-c6"), new OpeningPositionKey("after-d4"), true),
                new OpeningLineMove(4, 2, PlayerSide.Black, "d5", "d7d5", new OpeningPositionKey("after-d4"), new OpeningPositionKey("after-d5"), true),
                new OpeningLineMove(5, 3, PlayerSide.White, "e5", "e4e5", new OpeningPositionKey("after-d5"), rootPositionKey, true)
            ]
            : [
                new OpeningLineMove(1, 1, PlayerSide.White, "e4", "e2e4", new OpeningPositionKey("start"), new OpeningPositionKey("after-e4"), true),
                new OpeningLineMove(2, 1, PlayerSide.Black, "e6", "e7e6", new OpeningPositionKey("after-e4"), new OpeningPositionKey("after-e6"), true),
                new OpeningLineMove(3, 2, PlayerSide.White, "d4", "d2d4", new OpeningPositionKey("after-e6"), new OpeningPositionKey("after-d4"), true),
                new OpeningLineMove(4, 2, PlayerSide.Black, "d5", "d7d5", new OpeningPositionKey("after-d4"), new OpeningPositionKey("after-d5"), true),
                new OpeningLineMove(5, 3, PlayerSide.White, "e5", "e4e5", new OpeningPositionKey("after-d5"), rootPositionKey, true)
            ];

    public void SaveOpeningTrainingSessionResult(OpeningTrainingSessionResult sessionResult)
    {
    }

    public IReadOnlyList<OpeningTrainingSessionResult> ListOpeningTrainingSessionResults(string? playerKey = null, int limit = 200)
        => sessions.Take(limit).ToList();

    public void SaveOpeningReviewItems(string playerKey, IReadOnlyList<OpeningReviewItem> items)
    {
    }

    public IReadOnlyList<OpeningReviewItem> ListOpeningReviewItems(string? playerKey = null, int limit = 1000)
        => reviewItems.Take(limit).ToList();

    public void SaveOpeningTrainingTelemetryEvent(OpeningTrainingTelemetryEvent telemetryEvent)
    {
    }

    public IReadOnlyList<OpeningTrainingTelemetryEvent> ListOpeningTrainingTelemetryEvents(
        string? playerKey = null,
        DateTime? fromUtc = null,
        DateTime? toUtc = null,
        int limit = 500)
        => [
            new OpeningTrainingTelemetryEvent(
                OpeningTrainingTelemetryEvents.OpeningTrainerOpened,
                FixtureNowUtc.AddDays(-1),
                "snapshot-player",
                lines[0].LineKey,
                lines[0].OpeningKey,
                null,
                null,
                null,
                new Dictionary<string, string> { ["source"] = "snapshot" })
        ];

    public void SaveOpeningTree(OpeningTreeBuildResult tree)
    {
    }

    public OpeningTreeStoreSummary GetOpeningTreeSummary() => new(16, 22, 4);

    private static ImportedGame CreateGame()
    {
        const string pgn = """
[Event "Snapshot Review"]
[Site "MoveMentor"]
[Date "2026.06.06"]
[White "Snapshot Player"]
[Black "Training Partner"]
[Result "1-0"]
[WhiteElo "1820"]
[BlackElo "1760"]
[ECO "C02"]

1. e4 e6 2. d4 d5 3. e5 c5 4. c3 Nc6 5. Nf3 Qb6 6. Bd3 cxd4 7. cxd4 Bd7 8. O-O Nxd4 9. Nxd4 Qxd4 10. Nc3 Qxe5 1-0
""";

        return new ImportedGame(
            pgn,
            ["e4", "e6", "d4", "d5", "e5", "c5", "c3", "Nc6", "Nf3", "Qb6", "Bd3", "cxd4", "cxd4", "Bd7", "O-O", "Nxd4", "Nxd4", "Qxd4", "Nc3", "Qxe5"],
            "Snapshot Player",
            "Training Partner",
            1820,
            1760,
            "2026.06.06",
            "1-0",
            "C02",
            "MoveMentor",
            new PgnGameMetadata(
                null,
                null,
                "UTC",
                null,
                "2026.06.06",
                "10:00:00",
                "600+5",
                "Normal",
                null,
                "2026.06.06",
                "10:18:00",
                null,
                GameTimeControlCategory.Rapid));
    }

    private static GameAnalysisResult CreateAnalysisResult(ImportedGame game)
    {
        List<ReplayPly> replay = [
            CreateReplay(1, 1, PlayerSide.White, "e4", "e2e4", GamePhase.Opening),
            CreateReplay(2, 1, PlayerSide.Black, "e6", "e7e6", GamePhase.Opening),
            CreateReplay(3, 2, PlayerSide.White, "d4", "d2d4", GamePhase.Opening),
            CreateReplay(4, 2, PlayerSide.Black, "d5", "d7d5", GamePhase.Opening),
            CreateReplay(5, 3, PlayerSide.White, "e5", "e4e5", GamePhase.Opening),
            CreateReplay(6, 3, PlayerSide.Black, "c5", "c7c5", GamePhase.Opening),
            CreateReplay(7, 4, PlayerSide.White, "c3", "c2c3", GamePhase.Opening),
            CreateReplay(8, 4, PlayerSide.Black, "Nc6", "b8c6", GamePhase.Opening),
            CreateReplay(9, 5, PlayerSide.White, "Nf3", "g1f3", GamePhase.Opening),
            CreateReplay(10, 5, PlayerSide.Black, "Qb6", "d8b6", GamePhase.Opening),
            CreateReplay(11, 6, PlayerSide.White, "Bd3", "f1d3", GamePhase.Middlegame),
            CreateReplay(12, 6, PlayerSide.Black, "cxd4", "c5d4", GamePhase.Middlegame),
            CreateReplay(13, 7, PlayerSide.White, "cxd4", "c3d4", GamePhase.Middlegame),
            CreateReplay(14, 7, PlayerSide.Black, "Bd7", "c8d7", GamePhase.Middlegame),
            CreateReplay(15, 8, PlayerSide.White, "O-O", "e1g1", GamePhase.Middlegame),
            CreateReplay(16, 8, PlayerSide.Black, "Nxd4", "c6d4", GamePhase.Middlegame),
            CreateReplay(17, 9, PlayerSide.White, "Nxd4", "f3d4", GamePhase.Middlegame),
            CreateReplay(18, 9, PlayerSide.Black, "Qxd4", "b6d4", GamePhase.Middlegame),
            CreateReplay(19, 10, PlayerSide.White, "Nc3", "b1c3", GamePhase.Middlegame),
            CreateReplay(20, 10, PlayerSide.Black, "Qxe5", "d4e5", GamePhase.Middlegame)
        ];

        List<MoveAnalysisResult> moves = replay
            .Select((ply, index) => CreateMoveAnalysis(ply, index))
            .ToList();

        return new GameAnalysisResult(
            game,
            PlayerSide.White,
            replay,
            moves,
            [
                new SelectedMistake(
                    [moves[10]],
                    MoveQualityBucket.Mistake,
                    new MistakeTag("Loose center tension", 0.78, ["Allowed the queen to pressure d4."]),
                    new MoveExplanation("Developing was natural, but the center needed attention.", "Review the c3-d4 structure before developing the bishop.")),
                new SelectedMistake(
                    [moves[14]],
                    MoveQualityBucket.Blunder,
                    new MistakeTag("Tactical oversight", 0.86, ["Nxd4 tactic became possible."]),
                    new MoveExplanation("Castling walked into a known capture pattern.", "Pause when the opponent queen and knight attack d4 together."))
            ]);
    }

    private List<StoredMoveAnalysis> CreateStoredMoveAnalyses()
    {
        StoredGameContext gameContext = new(
            fingerprint,
            game.WhitePlayer,
            game.BlackPlayer,
            game.DateText,
            game.Result,
            game.Eco,
            game.Site,
            game.WhiteElo,
            game.BlackElo,
            game.Metadata?.TimeControl,
            game.Metadata?.TimeControlCategory ?? GameTimeControlCategory.Unknown,
            game.Metadata?.UtcDate,
            game.Metadata?.UtcTime,
            game.Metadata?.EndDate,
            game.Metadata?.EndTime,
            game.Metadata?.Termination,
            game.Metadata?.Link);
        StoredAnalysisRunContext run = new(PlayerSide.White, 16, 3, 800, FixtureNowUtc.AddHours(-2));

        return result.MoveAnalyses
            .Select((move, index) => new StoredMoveAnalysis(
                gameContext,
                run,
                new StoredMoveContext(
                    move.Replay.Ply,
                    move.Replay.MoveNumber,
                    move.Replay.San,
                    move.Replay.Uci,
                    move.Replay.FenBefore,
                    move.Replay.FenAfter,
                    move.Replay.Phase,
                    move.EvalBeforeCp,
                    move.EvalAfterCp,
                    move.BestMateIn,
                    move.PlayedMateIn,
                    move.CentipawnLoss,
                    move.Quality,
                    move.MaterialDeltaCp,
                    move.BeforeAnalysis.BestMoveUci),
                new StoredMoveAdviceContext(
                    move.MistakeTag?.Label,
                    move.MistakeTag?.Confidence,
                    move.MistakeTag?.Evidence ?? [],
                    move.Explanation?.ShortText,
                    move.Explanation?.DetailedText,
                    move.Explanation?.TrainingHint,
                    move.Quality is MoveQualityBucket.Mistake or MoveQualityBucket.Blunder,
                    move.MistakeTag?.Label)))
            .ToList();
    }

    private static IReadOnlyList<OpeningLineCatalogItem> CreateLines()
    {
        OpeningKey french = new("C02:French Defense");
        OpeningKey caro = new("B12:Caro-Kann Defense");
        return [
            new OpeningLineCatalogItem(
                french,
                new OpeningLineKey("french-advance-main"),
                RepertoireSide.White,
                "C02",
                "French Defense",
                "Advance Variation",
                "French Defense - Advance Variation",
                new OpeningPositionKey("french-start"),
                StartFen,
                42,
                7),
            new OpeningLineCatalogItem(
                caro,
                new OpeningLineKey("caro-advance-main"),
                RepertoireSide.White,
                "B12",
                "Caro-Kann Defense",
                "Advance Variation",
                "Caro-Kann Defense - Advance Variation",
                new OpeningPositionKey("caro-start"),
                "rn1qkbnr/pp2pppp/2p5/3pP3/3P4/2N5/PPP2PPP/R1BQKBNR b KQkq - 1 4",
                25,
                5)
        ];
    }

    private static OpeningTrainerOverview CreateOverview(OpeningLineCatalogItem line)
    {
        OpeningBranchKey branchKey = new($"{line.LineKey.Value}:c5");
        OpeningPositionKey branchPosition = new($"{line.LineKey.Value}:after-c5");
        OpeningCoverageSummary coverage = new(7, 4, 2, 3, 57.1, 12, 3, 8);
        OpeningTrainingMoveOption recommended = new(
            "c3",
            "c2c3",
            OpeningTrainingMoveRole.Expected,
            true,
            "Keeps the pawn chain stable before developing.");

        return new OpeningTrainerOverview(
            line.OpeningKey,
            line.LineKey,
            line.RepertoireSide,
            line.Eco,
            line.OpeningName,
            line.VariationName,
            [
                new OpeningLineMove(1, 1, PlayerSide.White, "e4", "e2e4", new OpeningPositionKey("start"), new OpeningPositionKey("after-e4"), true),
                new OpeningLineMove(2, 1, PlayerSide.Black, "e6", "e7e6", new OpeningPositionKey("after-e4"), new OpeningPositionKey("after-e6"), true),
                new OpeningLineMove(3, 2, PlayerSide.White, "d4", "d2d4", new OpeningPositionKey("after-e6"), new OpeningPositionKey("after-d4"), true),
                new OpeningLineMove(4, 2, PlayerSide.Black, "d5", "d7d5", new OpeningPositionKey("after-d4"), new OpeningPositionKey("after-d5"), true),
                new OpeningLineMove(5, 3, PlayerSide.White, "e5", "e4e5", new OpeningPositionKey("after-d5"), line.RootPositionKey, true)
            ],
            [
                new OpeningTrainingBranch(
                    branchKey,
                    "c5",
                    "c7c5",
                    28,
                    "Most common pressure on the pawn chain.",
                    recommended,
                    [new OpeningTrainingMove(7, 4, PlayerSide.White, "c3", "c2c3", OpeningTrainingMoveRole.Expected, true, "Support d4.")],
                    [new OpeningTrainingBranchSourceStat(OpeningTrainingBranchSourceKind.ExampleGame, 28)],
                    branchPosition)
            ],
            new OpponentReplyProfile(
                line.LineKey,
                line.RepertoireSide,
                [
                    new OpponentMoveFrequency("c5", "c7c5", 28, 28, 3, 2, false, OpponentMoveFrequencySourceKind.BookFrequency, "Common and still under-trained."),
                    new OpponentMoveFrequency("Nc6", "b8c6", 14, 14, 1, 0, false, OpponentMoveFrequencySourceKind.BookFrequency, "Usually transposes.")
                ],
                "Opponents challenge d4 early; train the c3 response until it feels automatic."),
            coverage,
            [
                new TrainingPriorityItem(
                    $"{line.LineKey.Value}:priority-c5",
                    line.LineKey,
                    TrainingPriorityAction.TrainThisBranch,
                    TrainingPriorityReasonCode.DangerousOpponentReply,
                    "Practice the c5 branch",
                    "High frequency reply with recent mistakes.",
                    "2 mistakes in recent games.",
                    1500,
                    8,
                    branchKey,
                    branchPosition,
                    "c3",
                    "c2c3")
            ],
            [
                new OpeningTrainingPosition(
                    $"{line.LineKey.Value}:weak-c5",
                    OpeningTrainingMode.LineRecall,
                    OpeningTrainingSourceKind.OpeningWeakness,
                    line.Eco,
                    line.OpeningName,
                    StartFen,
                    6,
                    4,
                    PlayerSide.White,
                    "Find the stabilizing move after ...c5.",
                    "Keep your center connected before chasing activity.",
                    90,
                    "Pawn chain",
                    "Bd3",
                    "c3",
                    "c3 protects d4 and removes the tactic.",
                    ["center", "common reply"],
                    [recommended],
                    [new OpeningTrainingMove(7, 4, PlayerSide.White, "c3", "c2c3", OpeningTrainingMoveRole.Expected, true)],
                    new OpeningTrainingReference("snapshot-fixture", PlayerSide.White, "Training Partner", "2026.06.06", "1-0", "Snapshot game", 15, "Tactical oversight"),
                    line.LineKey.Value,
                    null,
                    "Common reply from the book and your games.")
            ],
            [
                new OpeningMoveIdea("c3", [OpeningMoveIdeaTag.ControlCenter, OpeningMoveIdeaTag.PreventThreat], "Support d4 before developing the bishop.")
            ]);
    }

    private IReadOnlyList<OpeningReviewItem> CreateReviewItems()
        => [
            new OpeningReviewItem(
                new OpeningBranchKey($"{lines[0].LineKey.Value}:c5"),
                new OpeningPositionKey($"{lines[0].LineKey.Value}:after-c5"),
                FixtureNowUtc.AddDays(-4),
                FixtureNowUtc.AddDays(1),
                2.2,
                1,
                1,
                4,
                lines[0].OpeningKey,
                lines[0].LineKey)
        ];

    private IReadOnlyList<OpeningTrainingSessionResult> CreateSessions()
        => [
            new OpeningTrainingSessionResult(
                "snapshot-session-1",
                "snapshot-player",
                "Snapshot Player",
                FixtureNowUtc.AddDays(-2),
                FixtureNowUtc.AddDays(-2).AddMinutes(9),
                OpeningTrainingSessionOutcome.Completed,
                OpeningTrainingStyle.Mixed,
                OpeningTrainingStrictness.BookFlexible,
                6,
                8,
                5,
                1,
                2,
                ["French Defense - Advance Variation"],
                ["Pawn chain", "Common reply"],
                [
                    new OpeningTrainingRecordedAttempt(
                        $"{lines[0].LineKey.Value}:weak-c5",
                        OpeningTrainingMode.LineRecall,
                        OpeningTrainingSourceKind.OpeningWeakness,
                        OpeningTrainingAttemptStatus.Normal,
                        "C02",
                        "French Defense",
                        "Pawn chain",
                        "Bd3",
                        "Bd3",
                        "f1d3",
                        OpeningTrainingScore.Wrong,
                        FixtureNowUtc.AddDays(-2),
                        new OpeningBranchKey($"{lines[0].LineKey.Value}:c5"),
                        new OpeningPositionKey($"{lines[0].LineKey.Value}:after-c5"),
                        lines[0].OpeningKey,
                        lines[0].LineKey)
                ])
        ];

    private static ReplayPly CreateReplay(int ply, int moveNumber, PlayerSide side, string san, string uci, GamePhase phase)
        => new(
            ply,
            moveNumber,
            side,
            san,
            san,
            uci,
            StartFen,
            AfterC5Fen,
            StartFen,
            AfterC5Fen,
            phase,
            san.Length > 0 ? san[0].ToString() : string.Empty,
            null,
            uci.Length >= 4 ? uci[..2] : string.Empty,
            uci.Length >= 4 ? uci[2..4] : string.Empty,
            san.Contains('x', StringComparison.Ordinal),
            false,
            san.Contains("O-O", StringComparison.Ordinal));

    private static MoveAnalysisResult CreateMoveAnalysis(ReplayPly replay, int index)
    {
        MoveQualityBucket quality = index switch
        {
            10 => MoveQualityBucket.Mistake,
            14 => MoveQualityBucket.Blunder,
            18 => MoveQualityBucket.Inaccuracy,
            _ when index < 6 => MoveQualityBucket.Book,
            _ => MoveQualityBucket.Good
        };
        int? loss = quality switch
        {
            MoveQualityBucket.Blunder => 230,
            MoveQualityBucket.Mistake => 125,
            MoveQualityBucket.Inaccuracy => 55,
            MoveQualityBucket.Book => 0,
            _ => 18
        };
        MistakeTag? tag = quality is MoveQualityBucket.Mistake or MoveQualityBucket.Blunder or MoveQualityBucket.Inaccuracy
            ? new MistakeTag(
                quality == MoveQualityBucket.Blunder ? "Tactical oversight" : "Loose center tension",
                quality == MoveQualityBucket.Blunder ? 0.86 : 0.72,
                ["The d4 pawn became vulnerable.", "The opponent queen gained tempo."])
            : null;
        MoveExplanation explanation = tag is null
            ? new MoveExplanation("Solid practical move.", "Keep following the line until the opponent changes plans.")
            : new MoveExplanation("The move allowed pressure on the center.", "Ask what attacks d4 before choosing a developing move.", "The snapshot fixture uses this move to show coach language with real mistakes.");

        return new MoveAnalysisResult(
            replay,
            new EngineAnalysis(replay.FenBefore, [new EngineLine("c2c3", 35, null, ["c2c3", "b8c6"])], "c2c3"),
            new EngineAnalysis(replay.FenAfter, [new EngineLine("b8c6", 15, null, ["b8c6", "g1f3"])], "b8c6"),
            30 - index,
            quality == MoveQualityBucket.Blunder ? -200 : 15 - index,
            null,
            null,
            loss,
            quality,
            index == 14 ? -100 : 0,
            tag,
            explanation);
    }

    private static bool MatchesFilter(string? filterText, string? value)
        => string.IsNullOrWhiteSpace(filterText)
            || (!string.IsNullOrWhiteSpace(value)
                && value.Contains(filterText, StringComparison.OrdinalIgnoreCase));
}

internal static class SnapshotWindowFactory
{
    public static readonly IReadOnlyList<string> KnownViews =
    [
        "main",
        "analysis",
        "opening-trainer",
        "profiles",
        "opening-coverage",
        "settings"
    ];

    public static Window? Create(string view, SnapshotData data, IAnalysisStore? store)
    {
        return view switch
        {
            "main" => CreateMainWindow(data, store),
            "analysis" => CreateAnalysisWindow(data, store),
            "analysis-empty" => new AnalysisWindow(),
            "opening-trainer" => store is null ? null : CreateOpeningTrainerWindow(store),
            "profiles" => store is null ? null : CreateProfilesWindow(store),
            "opening-coverage" => store is null ? null : new OpeningCoverageWindow(new OpeningCoverageWindowViewModel(CreateSnapshotWorkspace(store))),
            "settings" => new SettingsWindow(),
            _ => null
        };
    }

    private static OpeningTrainerWorkspaceService CreateSnapshotWorkspace(IAnalysisStore store)
    {
        return new(
            store,
            store,
            store,
            store as IOpeningTheoryStore,
            store as IOpeningTrainingHistoryStore,
            new SnapshotTelemetryStore(store as IOpeningTrainingTelemetryStore));
    }

    private static OpeningTrainerWindow CreateOpeningTrainerWindow(IAnalysisStore store)
    {
        OpeningTrainerWindowViewModel viewModel = new(CreateSnapshotWorkspace(store));
        IReadOnlyList<OpeningLineCatalogItem> lines = (store as IOpeningTheoryStore)
            ?.ListOpeningLines(null, RepertoireSide.Both, 1)
            ?? [];
        if (lines.Count > 0)
        {
            viewModel.OpenLineFromCoverage(lines[0]);
        }

        return new OpeningTrainerWindow(viewModel);
    }

    private static MainWindow CreateMainWindow(SnapshotData data, IAnalysisStore? store)
    {
        MainWindow window = new(new NoopAnalysisWindowFactory(), new NoopProfilesWindowFactory(), () => store);
        MainWindowViewModel viewModel = new(new MissingStockfishPathResolver(), () => store);
        if (data.RichestAnalysisResult is not null)
        {
            viewModel.LoadAnalysisResult(data.RichestAnalysisResult);
        }
        else if (data.RichestImportedGame is not null)
        {
            viewModel.LoadImportedGame(data.RichestImportedGame);
        }

        window.DataContext = viewModel;
        return window;
    }

    private static AnalysisWindow CreateAnalysisWindow(SnapshotData data, IAnalysisStore? store)
    {
        if (data.RichestAnalysisResult is null)
        {
            return new AnalysisWindow();
        }

        GameAnalysisResult result = data.RichestAnalysisResult;
        return new AnalysisWindow(
            result.Game,
            null,
            _ => Task.CompletedTask,
            null,
            result.AnalyzedSide,
            new Dictionary<PlayerSide, GameAnalysisResult>
            {
                [result.AnalyzedSide] = result
            });
    }

    private static ProfilesWindow CreateProfilesWindow(IAnalysisStore store)
    {
        ProfilesWindow window = new(new PlayerProfileService(store));
        ListBox? profilesList = window.FindControl<ListBox>("ProfilesListBox");
        if (profilesList?.Items.Count > 0)
        {
            profilesList.SelectedIndex = 0;
        }

        return window;
    }

    private sealed class MissingStockfishPathResolver : IStockfishPathResolver
    {
        public string? Resolve() => null;
    }

    private sealed class NoopAnalysisWindowFactory : IAnalysisWindowFactory
    {
        public AnalysisWindow Create(AnalysisWindowRequest request) => new();
    }

    private sealed class NoopProfilesWindowFactory : IProfilesWindowFactory
    {
        public ProfilesWindow Create(ProfilesWindowRequest request) => new();
    }

    private sealed class SnapshotTelemetryStore(IOpeningTrainingTelemetryStore? inner) : IOpeningTrainingTelemetryStore
    {
        public void SaveOpeningTrainingTelemetryEvent(OpeningTrainingTelemetryEvent telemetryEvent)
        {
        }

        public IReadOnlyList<OpeningTrainingTelemetryEvent> ListOpeningTrainingTelemetryEvents(
            string? playerKey = null,
            DateTime? fromUtc = null,
            DateTime? toUtc = null,
            int limit = 500)
            => inner?.ListOpeningTrainingTelemetryEvents(playerKey, fromUtc, toUtc, limit) ?? [];
    }
}
