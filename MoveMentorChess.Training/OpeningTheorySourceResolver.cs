namespace MoveMentorChess.Training;

public static class OpeningTheorySourceResolver
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

        IOpeningTheoryStore resolvedTheoryStore = theoryStore is SqliteAnalysisStore
            ? TryCreateBundledSeedStore(runtimeEnvironment ?? SystemOpeningSeedRuntimeEnvironment.Instance) ?? theoryStore
            : theoryStore;

        return new OpeningTheoryQueryService(resolvedTheoryStore);
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
