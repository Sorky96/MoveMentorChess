using MoveMentorChess.Opening;
using MoveMentorChess.Persistence;

namespace MoveMentorChess.App.ViewModels;

internal static class PersistenceOpeningTheorySourceResolver
{
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

        return new SqliteAnalysisStore(
            seedPath,
            applyDerivedAnalysisDataVersionPolicy: false);
    }
}
