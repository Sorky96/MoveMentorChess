namespace MoveMentorChess.Domain;

public sealed record OpeningUnderstandingCard(
    OpeningUnderstandingCardKind Kind,
    string Title,
    string Body,
    int Priority);
