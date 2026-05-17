using MoveMentorChess.App.ViewModels;
using MoveMentorChess.App.Views;

namespace MoveMentorChess.App.Composition;

public interface IAnalysisWindowFactory
{
    AnalysisWindow Create(AnalysisWindowRequest request);
}
