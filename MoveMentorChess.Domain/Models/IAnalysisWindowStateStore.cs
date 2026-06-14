namespace MoveMentorChess.Domain;

public interface IAnalysisWindowStateStore
{
    bool TryLoadWindowState(string gameFingerprint, out AnalysisWindowState? state);
    void SaveWindowState(string gameFingerprint, AnalysisWindowState state);
}
