using MoveMentorChess.App.ViewModels;
using MoveMentorChess.App.Views;

namespace MoveMentorChess.App.Composition;

public sealed class AnalysisWindowFactory : IAnalysisWindowFactory
{
    public AnalysisWindow Create(AnalysisWindowRequest request)
    {
        return new AnalysisWindow(
            request.ImportedGame,
            request.EngineAnalyzer,
            request.NavigateToMoveAsync,
            request.AnalysisProgress,
            request.InitialSide,
            request.InitialResultsBySide);
    }
}
