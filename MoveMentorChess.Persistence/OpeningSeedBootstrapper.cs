namespace MoveMentorChess.Persistence;

public sealed class OpeningSeedBootstrapper
{
    public const string BundledSeedRelativePath = @"OpeningSeed\opening-seed.db";

    private readonly string localDatabasePath;
    private readonly string bundledSeedPath;
    private readonly IOpeningSeedRuntimeEnvironment runtimeEnvironment;

    public OpeningSeedBootstrapper(
        string localDatabasePath,
        string bundledSeedPath,
        IOpeningSeedRuntimeEnvironment? runtimeEnvironment = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(localDatabasePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(bundledSeedPath);

        this.localDatabasePath = localDatabasePath;
        this.bundledSeedPath = bundledSeedPath;
        this.runtimeEnvironment = runtimeEnvironment ?? SystemOpeningSeedRuntimeEnvironment.Instance;
    }

    public static string GetDefaultBundledSeedPath(IOpeningSeedRuntimeEnvironment? runtimeEnvironment = null)
    {
        IOpeningSeedRuntimeEnvironment effectiveEnvironment =
            runtimeEnvironment ?? SystemOpeningSeedRuntimeEnvironment.Instance;
        return Path.Combine(effectiveEnvironment.BaseDirectory, BundledSeedRelativePath);
    }

    public OpeningSeedBootstrapResult EnsureSeedImported()
    {
        if (!runtimeEnvironment.FileExists(bundledSeedPath))
        {
            return new OpeningSeedBootstrapResult(false, false, null, new OpeningTreeStoreSummary(0, 0, 0));
        }

        SqliteAnalysisStore bundledSeedStore = new(
            bundledSeedPath,
            applyDerivedAnalysisDataVersionPolicy: false);
        SqliteAnalysisStore localStore = new(localDatabasePath);
        string seedVersion = bundledSeedStore.GetOpeningSeedVersion() ?? BuildFallbackSeedVersion();
        OpeningTreeStoreSummary localSummary = localStore.GetOpeningTreeSummary();

        if (string.Equals(localStore.GetOpeningSeedVersion(), seedVersion, StringComparison.Ordinal)
            && localSummary.NodeCount > 0
            && localSummary.EdgeCount > 0)
        {
            return new OpeningSeedBootstrapResult(true, false, seedVersion, localSummary);
        }

        OpeningTreeBuildResult tree = bundledSeedStore.LoadOpeningTree();
        if (tree.Nodes.Count == 0 || tree.Edges.Count == 0)
        {
            return new OpeningSeedBootstrapResult(true, false, seedVersion, localSummary);
        }

        localStore.ReplaceOpeningTree(tree);
        localStore.SetOpeningSeedVersion(seedVersion);
        OpeningTreeStoreSummary importedSummary = localStore.GetOpeningTreeSummary();
        return new OpeningSeedBootstrapResult(true, true, seedVersion, importedSummary);
    }

    private string BuildFallbackSeedVersion()
    {
        DateTime utc = runtimeEnvironment.GetLastWriteTimeUtc(bundledSeedPath);
        return $"file-{utc:yyyyMMddHHmmss}";
    }
}

public interface IOpeningSeedRuntimeEnvironment
{
    string BaseDirectory { get; }

    bool FileExists(string path);

    DateTime GetLastWriteTimeUtc(string path);
}

public sealed class SystemOpeningSeedRuntimeEnvironment : IOpeningSeedRuntimeEnvironment
{
    public static SystemOpeningSeedRuntimeEnvironment Instance { get; } = new();

    private SystemOpeningSeedRuntimeEnvironment()
    {
    }

    public string BaseDirectory => AppContext.BaseDirectory;

    public bool FileExists(string path) => File.Exists(path);

    public DateTime GetLastWriteTimeUtc(string path) => File.GetLastWriteTimeUtc(path);
}

public sealed record OpeningSeedBootstrapResult(
    bool SeedFileFound,
    bool Imported,
    string? SeedVersion,
    OpeningTreeStoreSummary Summary);
