namespace MoveMentorChess.App.ViewModels;

public sealed record ProfilesWindowRequest(
    Func<ProfileMistakeExample, Task>? NavigateToProfileExampleAsync,
    Func<OpeningExampleGame, Task>? NavigateToOpeningExampleAsync,
    Func<OpeningMoveRecommendation, Task>? NavigateToOpeningPositionAsync);
