namespace MoveMentorChess.Domain;

public sealed record OpeningTrainingTelemetryEvent(
    string EventName,
    DateTime CreatedUtc,
    string? PlayerKey = null,
    OpeningLineKey? LineKey = null,
    OpeningKey? OpeningKey = null,
    string? SessionId = null,
    string? RecommendationId = null,
    SpecialTrainingModeKind? SpecialMode = null,
    IReadOnlyDictionary<string, string>? Properties = null);
