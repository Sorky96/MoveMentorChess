using MoveMentorChess.Analysis;
using Xunit;

namespace MoveMentorChessServices.Tests;

public sealed class LoggedAdviceGeneratorTests
{
    [Fact]
    public void Generate_UsesClockForTraceTimestamp()
    {
        DateTime timestamp = new(2026, 5, 25, 12, 30, 0, DateTimeKind.Utc);
        CapturingAdviceGenerationLogger logger = new();
        LoggedAdviceGenerator generator = new(
            new StubAdviceGenerator(),
            AdviceGeneratorMode.Template,
            "stub-generator",
            logger,
            new FixedClock(timestamp));

        generator.Generate(
            CreateReplay(),
            MoveQualityBucket.Good,
            new MistakeTag("general", 0.8, ["test"]),
            "a2a4",
            12,
            ExplanationLevel.Intermediate,
            new AdviceGenerationContext("unit-test", "game-1", PlayerSide.White));

        Assert.NotNull(logger.Trace);
        Assert.Equal(timestamp, logger.Trace.TimestampUtc);
        Assert.Equal("stub-generator", logger.Trace.GeneratorName);
        Assert.Equal("unit-test", logger.Trace.Source);
    }

    private static ReplayPly CreateReplay()
    {
        return new ReplayPly(
            1,
            1,
            PlayerSide.White,
            "a3",
            "a3",
            "a2a3",
            "4k3/8/8/8/8/8/P7/4K3 w - - 0 1",
            "4k3/8/8/8/8/P7/8/4K3 b - - 0 1",
            string.Empty,
            string.Empty,
            GamePhase.Opening,
            "P",
            null,
            "a2",
            "a3",
            false,
            false,
            false);
    }

    private sealed class StubAdviceGenerator : IAdviceGenerator
    {
        public MoveExplanation Generate(
            ReplayPly replay,
            MoveQualityBucket quality,
            MistakeTag? tag,
            string? bestMoveUci,
            int? centipawnLoss,
            ExplanationLevel level = ExplanationLevel.Intermediate,
            AdviceGenerationContext? context = null)
        {
            return new MoveExplanation("short", "detailed", "hint");
        }
    }

    private sealed class CapturingAdviceGenerationLogger : IAdviceGenerationLogger
    {
        public AdviceGenerationTrace? Trace { get; private set; }

        public void Record(AdviceGenerationTrace trace)
        {
            Trace = trace;
        }
    }

    private sealed class FixedClock(DateTime utcNow) : IClock
    {
        public DateTime UtcNow { get; } = utcNow;
    }
}
