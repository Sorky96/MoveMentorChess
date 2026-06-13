namespace MoveMentorChess.Domain;

public interface IOpeningTrainingTelemetryStore
{
    void SaveOpeningTrainingTelemetryEvent(OpeningTrainingTelemetryEvent telemetryEvent);
    IReadOnlyList<OpeningTrainingTelemetryEvent> ListOpeningTrainingTelemetryEvents(
        string? playerKey = null,
        DateTime? fromUtc = null,
        DateTime? toUtc = null,
        int limit = 500);
}
