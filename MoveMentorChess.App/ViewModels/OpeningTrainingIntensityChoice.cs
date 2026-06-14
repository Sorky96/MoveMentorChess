namespace MoveMentorChess.App.ViewModels;

public sealed record OpeningTrainingIntensityChoice(
    string Id,
    string Title,
    string Description,
    OpeningTrainingStrictness Strictness);
