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

    private static SqliteAnalysisStore? TryCreateStore(string? databasePath)
    {
        string resolvedPath = string.IsNullOrWhiteSpace(databasePath)
            ? SqliteAnalysisStore.GetDefaultDatabasePath()
            : databasePath;

        if (!File.Exists(resolvedPath))
        {
            Console.WriteLine("No analysis database was found. Rendering fallback empty states.");
            return null;
        }

        try
        {
            return new SqliteAnalysisStore(
                resolvedPath,
                applyDerivedAnalysisDataVersionPolicy: false);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Console.WriteLine($"Could not open analysis database: {ex.Message}");
            return null;
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
            frame.Save(outputPath);
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
            "opening-trainer" => store is null ? null : new OpeningTrainerWindow(new OpeningTrainerWindowViewModel(CreateSnapshotWorkspace(store))),
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
