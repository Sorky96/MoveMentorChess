using MoveMentorChess.Opening;
using MoveMentorChess.App.ViewModels;
using MoveMentorChess.Persistence;
using Xunit;

namespace MoveMentorChessServices.Tests;

public sealed class PersistenceOpeningTheorySourceResolverTests
{
    private const string StartFen = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";
    private const string E4Fen = "rnbqkbnr/pppppppp/8/8/4P3/8/PPPP1PPP/RNBQKBNR b KQkq e3 0 1";

    [Fact]
    public void Create_UsesLocalSqliteStoreWhenBundledSeedIsMissing()
    {
        string localDatabasePath = CreateTempDatabasePath();
        FakeOpeningSeedRuntimeEnvironment environment = new(@"C:\app-root");

        try
        {
            SqliteAnalysisStore localStore = new(localDatabasePath);
            localStore.ReplaceOpeningTree(CreateSingleNodeTree(StartFen));

            OpeningTheoryQueryService service = PersistenceOpeningTheorySourceResolver.Create((IOpeningTheoryStore)localStore, environment);

            Assert.True(service.TryGetPositionByFen(StartFen, out OpeningTheoryPosition? position));
            Assert.NotNull(position);
            Assert.Contains(
                OpeningSeedBootstrapper.BundledSeedRelativePath,
                Assert.Single(environment.FileExistenceChecks),
                StringComparison.Ordinal);
        }
        finally
        {
            DeleteTempDatabase(localDatabasePath);
        }
    }

    [Fact]
    public void Create_UsesBundledSeedStoreWhenSeedExists()
    {
        string localDatabasePath = CreateTempDatabasePath();
        string seedRoot = Path.Combine(Path.GetTempPath(), $"MoveMentorChessServices-seed-{Guid.NewGuid():N}");
        string seedDirectory = Path.Combine(seedRoot, "OpeningSeed");
        string seedPath = Path.Combine(seedDirectory, "opening-seed.db");
        FakeOpeningSeedRuntimeEnvironment environment = new(seedRoot);

        try
        {
            Directory.CreateDirectory(seedDirectory);
            SqliteAnalysisStore localStore = new(localDatabasePath);
            localStore.ReplaceOpeningTree(CreateSingleNodeTree(StartFen));
            SqliteAnalysisStore seedStore = new(seedPath);
            seedStore.ReplaceOpeningTree(CreateSingleNodeTree(E4Fen));
            environment.AddFile(seedPath, new DateTime(2026, 6, 1, 16, 0, 0, DateTimeKind.Utc));

            OpeningTheoryQueryService service = PersistenceOpeningTheorySourceResolver.Create((IOpeningTheoryStore)localStore, environment);

            Assert.False(service.TryGetPositionByFen(StartFen, out _));
            Assert.True(service.TryGetPositionByFen(E4Fen, out OpeningTheoryPosition? seedPosition));
            Assert.NotNull(seedPosition);
        }
        finally
        {
            DeleteTempDatabase(localDatabasePath);
            DeleteTempDatabase(seedPath);
            if (Directory.Exists(seedRoot))
            {
                Directory.Delete(seedRoot, recursive: true);
            }
        }
    }

    private static OpeningTreeBuildResult CreateSingleNodeTree(string fen)
    {
        OpeningPositionKey positionKey = OpeningPositionKeyBuilder.BuildKey(fen);
        OpeningPositionNode node = new()
        {
            Id = Guid.NewGuid(),
            PositionKey = positionKey.Value,
            Fen = fen,
            Ply = 0,
            MoveNumber = 1,
            SideToMove = fen.Contains(" b ", StringComparison.Ordinal) ? "Black" : "White",
            OccurrenceCount = 1,
            DistinctGameCount = 1
        };

        return new OpeningTreeBuildResult([node], [], []);
    }

    private static string CreateTempDatabasePath()
    {
        return Path.Combine(Path.GetTempPath(), $"MoveMentorChessServices-theory-source-{Guid.NewGuid():N}.db");
    }

    private static void DeleteTempDatabase(string databasePath)
    {
        if (File.Exists(databasePath))
        {
            File.Delete(databasePath);
        }
    }

    private sealed class FakeOpeningSeedRuntimeEnvironment(string baseDirectory) : IOpeningSeedRuntimeEnvironment
    {
        private readonly HashSet<string> files = new(StringComparer.OrdinalIgnoreCase);

        public string BaseDirectory { get; } = baseDirectory;

        public List<string> FileExistenceChecks { get; } = [];

        public void AddFile(string path, DateTime lastWriteTimeUtc)
        {
            files.Add(path);
            LastWriteTimes[path] = lastWriteTimeUtc;
        }

        public Dictionary<string, DateTime> LastWriteTimes { get; } = new(StringComparer.OrdinalIgnoreCase);

        public bool FileExists(string path)
        {
            FileExistenceChecks.Add(path);
            return files.Contains(path);
        }

        public DateTime GetLastWriteTimeUtc(string path) => LastWriteTimes[path];
    }
}
