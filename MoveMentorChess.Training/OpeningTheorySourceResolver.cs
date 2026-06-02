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

        return new OpeningTheoryQueryService(theoryStore);
    }
}
