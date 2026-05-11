namespace MoveMentorChess.Domain;

public sealed record TrainingCoachHint(
    TrainingCoachHintLevel Level,
    string Title,
    string Text);

public enum TrainingCoachHintLevel
{
    Light,
    Plan,
    Structure,
    OpponentIdea,
    Full
}

public enum TrainingMistakeCategory
{
    Unknown,
    IllegalMove,
    MissedBookMove,
    WrongBranch,
    NeedsRepair,
    Transposition
}
