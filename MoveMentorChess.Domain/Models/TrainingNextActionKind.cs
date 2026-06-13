namespace MoveMentorChess.Domain;

public enum TrainingNextActionKind
{
    RepeatNow,
    RepeatAfterBreak,
    ReturnTomorrow,
    RepairWeakBranches,
    BrowseAnotherOpening,
    PracticeMainLineOnly,
    ReviewWithHintsAllowed,
    StopForNow
}
