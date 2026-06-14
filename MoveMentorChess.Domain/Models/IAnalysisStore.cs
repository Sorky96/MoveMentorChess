namespace MoveMentorChess.Domain;

public interface IAnalysisStore :
    IImportedGameStore,
    IAnalysisResultStore,
    IStoredMoveAnalysisStore,
    IAdviceFeedbackStore,
    IAnalysisWindowStateStore
{
}
