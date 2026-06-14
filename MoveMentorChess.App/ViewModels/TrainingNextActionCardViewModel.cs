namespace MoveMentorChess.App.ViewModels;

public sealed record TrainingNextActionCardViewModel(
    TrainingNextAction Action,
    string Title,
    string Reason,
    string TimingText,
    string ButtonText,
    bool IsPrimary)
{
    public static TrainingNextActionCardViewModel Create(TrainingNextAction action, bool isPrimary)
    {
        string timingText = action.DelayMinutes switch
        {
            _ when !isPrimary && action.Kind == TrainingNextActionKind.RepairWeakBranches => "Optional",
            <= 0 => "Ready now",
            < 60 => $"{action.DelayMinutes} min",
            1440 => "Tomorrow",
            _ => $"{Math.Round(action.DelayMinutes / 60d, 1):0.#} hours"
        };

        string buttonText = action.Kind switch
        {
            TrainingNextActionKind.RepeatAfterBreak when action.DelayMinutes > 0 && isPrimary => "Train another opening",
            TrainingNextActionKind.RepeatAfterBreak when action.DelayMinutes > 0 => "Schedule repeat",
            TrainingNextActionKind.RepeatNow => "Repeat now",
            TrainingNextActionKind.ReturnTomorrow => "Back to selection",
            TrainingNextActionKind.RepairWeakBranches => "Open priorities",
            TrainingNextActionKind.PracticeMainLineOnly => "Practice main line only",
            TrainingNextActionKind.ReviewWithHintsAllowed => "Review with hints",
            TrainingNextActionKind.StopForNow => "Stop for now",
            TrainingNextActionKind.BrowseAnotherOpening when string.Equals(action.Id, "train-another-opening", StringComparison.OrdinalIgnoreCase) => "Train another opening",
            TrainingNextActionKind.BrowseAnotherOpening => "Browse openings",
            _ => action.CommandLabel
        };

        return new TrainingNextActionCardViewModel(
            action,
            action.Title,
            action.Description,
            timingText,
            buttonText,
            isPrimary);
    }
}
