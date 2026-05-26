using System.Diagnostics;
using MoveMentorChess.Analysis;
using Xunit;

namespace MoveMentorChessServices.Tests;

public sealed class DiagnosticsLoggerTests
{
    [Fact]
    public void JsonlDiagnosticsLogger_ReportsWriteFailureWithoutThrowing()
    {
        string directoryPath = Path.Join(Path.GetTempPath(), $"MoveMentorChessServices-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directoryPath);
        using CapturingTraceListener listener = new();
        Trace.Listeners.Add(listener);

        try
        {
            JsonlDiagnosticsLogger<TestEntry> logger = new(directoryPath);

            logger.Record(new TestEntry("sample"));
            Trace.Flush();

            Assert.Contains(nameof(JsonlDiagnosticsLogger<TestEntry>), listener.Output);
            Assert.Contains("failed to write diagnostic log", listener.Output);
        }
        finally
        {
            Trace.Listeners.Remove(listener);
            Directory.Delete(directoryPath);
        }
    }

    [Fact]
    public void JsonlDiagnosticsLogger_DoesNotThrowWhenTraceListenerFails()
    {
        string directoryPath = Path.Join(Path.GetTempPath(), $"MoveMentorChessServices-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directoryPath);
        using ThrowingTraceListener listener = new();
        Trace.Listeners.Add(listener);

        try
        {
            JsonlDiagnosticsLogger<TestEntry> logger = new(directoryPath);

            logger.Record(new TestEntry("sample"));
        }
        finally
        {
            Trace.Listeners.Remove(listener);
            Directory.Delete(directoryPath);
        }
    }

    [Fact]
    public void LoggedAdviceGenerator_UsesInjectedClockForTraceTimestamp()
    {
        DateTime nowUtc = new(2026, 5, 26, 18, 30, 0, DateTimeKind.Utc);
        CapturingAdviceGenerationLogger logger = new();
        LoggedAdviceGenerator generator = new(
            new StaticAdviceGenerator(),
            AdviceGeneratorMode.Template,
            "test-generator",
            logger,
            new FixedClock(nowUtc));
        ReplayPly replay = new(
            1,
            1,
            PlayerSide.White,
            "e4",
            "e4",
            "e2e4",
            "start",
            "after",
            string.Empty,
            string.Empty,
            GamePhase.Opening,
            "P",
            null,
            "e2",
            "e4",
            false,
            false,
            false);

        _ = generator.Generate(
            replay,
            MoveQualityBucket.Inaccuracy,
            null,
            "e7e5",
            25);

        Assert.NotNull(logger.Trace);
        Assert.Equal(nowUtc, logger.Trace!.TimestampUtc);
    }

    private sealed record TestEntry(string Value);

    private sealed class StaticAdviceGenerator : IAdviceGenerator
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
            return new MoveExplanation("Short", "Hint", "Detailed");
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

    private sealed class CapturingTraceListener : TraceListener
    {
        private readonly StringWriter writer = new();

        public string Output => writer.ToString();

        public override void Write(string? message)
        {
            writer.Write(message);
        }

        public override void WriteLine(string? message)
        {
            writer.WriteLine(message);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                writer.Dispose();
            }

            base.Dispose(disposing);
        }
    }

    private sealed class ThrowingTraceListener : TraceListener
    {
        public override void Write(string? message)
        {
            throw new TraceListenerFailureException();
        }

        public override void WriteLine(string? message)
        {
            throw new TraceListenerFailureException();
        }
    }

    private sealed class TraceListenerFailureException : ApplicationException
    {
    }
}
