namespace MoveMentorChess.App.ViewModels;

public sealed record OpeningTrainingProfileChoice(
    string Id,
    string Title,
    string Description,
    RepertoireSide Side,
    string PlayerKey);
