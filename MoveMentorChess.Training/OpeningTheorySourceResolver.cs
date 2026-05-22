namespace MoveMentorChess.Training;

public static class OpeningTheorySourceResolver
{
    public static OpeningTheoryQueryService? Create(IAnalysisStore analysisStore)
    {
        ArgumentNullException.ThrowIfNull(analysisStore);

        return analysisStore is IOpeningTheoryStore theoryStore
            ? Create(theoryStore)
            : null;
    }

    public static OpeningTheoryQueryService Create(IOpeningTheoryStore theoryStore)
    {
        ArgumentNullException.ThrowIfNull(theoryStore);

        IOpeningTheoryStore resolvedTheoryStore = theoryStore is SqliteAnalysisStore
            ? TryCreateBundledSeedStore() ?? theoryStore
            : theoryStore;

        return new OpeningTheoryQueryService(resolvedTheoryStore);
    }

    private static SqliteAnalysisStore? TryCreateBundledSeedStore()
    {
        string seedPath = OpeningSeedBootstrapper.GetDefaultBundledSeedPath();
        if (!File.Exists(seedPath))
        {
            return null;
        }

        return new SqliteAnalysisStore(
            seedPath,
            applyDerivedAnalysisDataVersionPolicy: false);
    }
}
