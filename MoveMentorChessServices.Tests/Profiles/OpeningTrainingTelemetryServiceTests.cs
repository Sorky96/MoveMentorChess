using Xunit;

namespace MoveMentorChessServices.Tests.Profiles;

public sealed class OpeningTrainingTelemetryServiceTests
{
    [Fact]
    public void Track_StoresTelemetrySnapshot()
    {
        OpeningTrainingTelemetryService service = new();
        OpeningLineCatalogItem opening = new(
            new OpeningKey("C20"),
            new OpeningLineKey("C20:main"),
            RepertoireSide.White,
            "C20",
            "King's Pawn",
            "Main line",
            "King's Pawn",
            new OpeningPositionKey("root"),
            new ChessGame().GetFen(),
            10,
            3);

        service.Track(
            OpeningTrainingTelemetryEvents.OpeningTrainingStarted,
            "Alpha",
            opening,
            recommendationId: "C20:main",
            properties: new Dictionary<string, string>
            {
                ["start_source"] = "today_recommendation"
            });

        OpeningTrainingTelemetryEvent telemetryEvent = Assert.Single(service.Snapshot());
        Assert.Equal(OpeningTrainingTelemetryEvents.OpeningTrainingStarted, telemetryEvent.EventName);
        Assert.Equal("alpha", telemetryEvent.PlayerKey);
        Assert.Equal(opening.LineKey, telemetryEvent.LineKey);
        Assert.Equal("today_recommendation", telemetryEvent.Properties!["start_source"]);
    }

    [Fact]
    public void Track_PersistsTelemetryEvent()
    {
        InMemoryTelemetryStore store = new();
        OpeningTrainingTelemetryService service = new(store);

        service.Track(
            OpeningTrainingTelemetryEvents.GuidedHintUsed,
            "Alpha",
            recommendationId: "recommendation-1",
            properties: new Dictionary<string, string>
            {
                ["hint_level"] = "Plan"
            });

        OpeningTrainingTelemetryEvent persisted = Assert.Single(store.Events);
        OpeningTrainingTelemetryEvent snapshot = Assert.Single(service.Snapshot());
        Assert.Equal(OpeningTrainingTelemetryEvents.GuidedHintUsed, persisted.EventName);
        Assert.Equal("alpha", persisted.PlayerKey);
        Assert.Equal("Plan", persisted.Properties!["hint_level"]);
        Assert.Equal(persisted, snapshot);
    }

    [Fact]
    public void Track_KeepsInMemorySnapshot()
    {
        OpeningTrainingTelemetryService service = new(new InMemoryTelemetryStore());

        service.Track(OpeningTrainingTelemetryEvents.OpeningTrainerOpened, "Beta");

        OpeningTrainingTelemetryEvent telemetryEvent = Assert.Single(service.Snapshot());
        Assert.Equal("beta", telemetryEvent.PlayerKey);
    }

    [Fact]
    public void OpeningCoachTelemetryEvents_UseProductEventNames()
    {
        Assert.Equal("opening_daily_lesson_shown", OpeningTrainingTelemetryEvents.OpeningDailyLessonShown);
        Assert.Equal("opening_daily_lesson_started", OpeningTrainingTelemetryEvents.OpeningDailyLessonStarted);
        Assert.Equal("opening_advanced_opened", OpeningTrainingTelemetryEvents.OpeningAdvancedOpened);
        Assert.Equal("opening_reference_revealed", OpeningTrainingTelemetryEvents.GuidedReferenceRevealed);
        Assert.Equal("opening_dont_know_used", OpeningTrainingTelemetryEvents.GuidedDontKnowUsed);
        Assert.Equal("opening_learning_plan_shown", OpeningTrainingTelemetryEvents.OpeningLearningPlanShown);
    }

    private sealed class InMemoryTelemetryStore : IOpeningTrainingTelemetryStore
    {
        public List<OpeningTrainingTelemetryEvent> Events { get; } = [];

        public void SaveOpeningTrainingTelemetryEvent(OpeningTrainingTelemetryEvent telemetryEvent)
            => Events.Add(telemetryEvent);

        public IReadOnlyList<OpeningTrainingTelemetryEvent> ListOpeningTrainingTelemetryEvents(
            string? playerKey = null,
            DateTime? fromUtc = null,
            DateTime? toUtc = null,
            int limit = 500)
            => Events;
    }
}
