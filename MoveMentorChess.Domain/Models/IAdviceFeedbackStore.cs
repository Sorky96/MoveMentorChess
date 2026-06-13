namespace MoveMentorChess.Domain;

public interface IAdviceFeedbackStore
{
    IReadOnlyList<MoveAdviceFeedback> ListMoveAdviceFeedback(string? filterText = null, int limit = 5000);
    void SaveMoveAdviceFeedback(MoveAdviceFeedback feedback);
}
