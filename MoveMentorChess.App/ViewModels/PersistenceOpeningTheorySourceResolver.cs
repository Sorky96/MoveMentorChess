using MoveMentorChess.Opening;
using MoveMentorChess.Persistence;

namespace MoveMentorChess.App.ViewModels;

internal static class PersistenceOpeningTheorySourceResolver
{
    private static readonly object Sync = new();
    private static string? cachedBundledSeedPath;
    private static SqliteAnalysisStore? cachedBundledSeedStore;

    public static OpeningTheoryQueryService? Create(
        IAnalysisStore analysisStore,
        IOpeningSeedRuntimeEnvironment? runtimeEnvironment = null)
    {
        ArgumentNullException.ThrowIfNull(analysisStore);

        return analysisStore is IOpeningTheoryStore theoryStore
            ? Create(theoryStore, runtimeEnvironment)
            : null;
    }

    public static OpeningTheoryQueryService Create(
        IOpeningTheoryStore theoryStore,
        IOpeningSeedRuntimeEnvironment? runtimeEnvironment = null)
    {
        ArgumentNullException.ThrowIfNull(theoryStore);

        return new OpeningTheoryQueryService(ResolveTheoryStore(theoryStore, runtimeEnvironment));
    }

    public static IOpeningTheoryStore ResolveTheoryStore(
        IOpeningTheoryStore theoryStore,
        IOpeningSeedRuntimeEnvironment? runtimeEnvironment = null)
    {
        ArgumentNullException.ThrowIfNull(theoryStore);

        return theoryStore is SqliteAnalysisStore
            ? TryCreateBundledSeedStore(runtimeEnvironment ?? SystemOpeningSeedRuntimeEnvironment.Instance) ?? theoryStore
            : theoryStore;
    }

    private static SqliteAnalysisStore? TryCreateBundledSeedStore(IOpeningSeedRuntimeEnvironment runtimeEnvironment)
    {
        string seedPath = OpeningSeedBootstrapper.GetDefaultBundledSeedPath(runtimeEnvironment);
        if (!runtimeEnvironment.FileExists(seedPath))
        {
            return null;
        }

        lock (Sync)
        {
            if (string.Equals(cachedBundledSeedPath, seedPath, StringComparison.OrdinalIgnoreCase)
                && cachedBundledSeedStore is not null)
            {
                return cachedBundledSeedStore;
            }

            cachedBundledSeedPath = seedPath;
            cachedBundledSeedStore = new SqliteAnalysisStore(
                seedPath,
                applyDerivedAnalysisDataVersionPolicy: false);
            return cachedBundledSeedStore;
        }
    }
}
