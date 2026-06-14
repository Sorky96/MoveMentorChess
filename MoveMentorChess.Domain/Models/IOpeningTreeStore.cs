namespace MoveMentorChess.Domain;

public interface IOpeningTreeStore
{
    void SaveOpeningTree(OpeningTreeBuildResult tree);
    OpeningTreeStoreSummary GetOpeningTreeSummary();
}
