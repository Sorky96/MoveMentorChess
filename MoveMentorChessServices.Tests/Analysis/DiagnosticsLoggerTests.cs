using System.Diagnostics;
using System.Text.Json;
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

    [Fact]
    public void DiagnosticMistakeClassifier_UsesInjectedClockForDiagnosticEntry()
    {
        DateTime nowUtc = new(2026, 5, 26, 19, 10, 0, DateTimeKind.Utc);
        string tempDirectory = Path.Join(Path.GetTempPath(), $"MoveMentorChessServices-classifier-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDirectory);
        string logFilePath = Path.Join(tempDirectory, "classifier.jsonl");

        try
        {
            DiagnosticMistakeClassifier classifier = new(clock: new FixedClock(nowUtc), logFilePath: logFilePath);
            ReplayPly replay = new(
                1,
                1,
                PlayerSide.White,
                "Qh5",
                "Qh5",
                "d1h5",
                "before",
                "after",
                string.Empty,
                string.Empty,
                GamePhase.Middlegame,
                "Q",
                null,
                "d1",
                "h5",
                false,
                false,
                false);

            MistakeTag? tag = classifier.Classify(
                replay,
                "game-fingerprint",
                PlayerSide.White,
                MoveQualityBucket.Mistake,
                centipawnLoss: 130,
                materialDeltaCp: 0);

            Assert.NotNull(tag);
            string jsonLine = File.ReadAllText(logFilePath);
            ClassifierDiagnosticEntry? entry = JsonSerializer.Deserialize<ClassifierDiagnosticEntry>(jsonLine);

            Assert.NotNull(entry);
            Assert.Equal(nowUtc, entry!.TimestampUtc);
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Fact]
    public void LlamaCppAdviceModel_UsesInjectedClockForInvocationFailureLog()
    {
        string tempDirectory = Path.Join(Path.GetTempPath(), $"MoveMentorChessServices-llama-clock-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDirectory);
        string modelPath = Path.Join(tempDirectory, "model.gguf");
        File.WriteAllText(modelPath, "fake model");
        DateTime nowUtc = new(2026, 5, 26, 19, 20, 0, DateTimeKind.Utc);
        LlamaCppAdviceRuntime runtime = new(
            CliPath: Path.Join(
                Environment.GetFolderPath(Environment.SpecialFolder.System),
                "WindowsPowerShell",
                "v1.0",
                "powershell.exe"),
            ModelPath: modelPath,
            TimeoutMs: 5000);
        AdviceRuntimeInvocationLog? capturedLog = null;
        LlamaCppAdviceModel model = new(
            runtime,
            new FixedClock(nowUtc),
            log =>
            {
                capturedLog = log;
                return Path.Join(tempDirectory, "diagnostic.json");
            });
        LocalModelAdviceRequest request = new(
            new ReplayPly(
                1,
                1,
                PlayerSide.White,
                "e4",
                "e4",
                "e2e4",
                "before",
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
                false),
            MoveQualityBucket.Inaccuracy,
            null,
            "e7e5",
            25,
            ExplanationLevel.Intermediate,
            new AdviceGenerationContext("test", "game", PlayerSide.White),
            "Return JSON.");

        try
        {
            AdviceRuntimeInvocationException exception = Assert.Throws<AdviceRuntimeInvocationException>(() => model.Generate(request));

            Assert.Equal(nowUtc, exception.Log.TimestampUtc);
            Assert.Same(capturedLog, exception.Log);
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Fact]
    public void AdviceRuntimeSmokeTester_UsesInjectedClockForParseFailureLog()
    {
        DateTime nowUtc = new(2026, 5, 26, 19, 40, 0, DateTimeKind.Utc);
        AdviceRuntimeInvocationLog? capturedLog = null;
        AdviceRuntimeSmokeTestResult result = AdviceRuntimeSmokeTester.Run(
            new AdviceRuntimeStatus(true, "ready", RuntimeName: "fake"),
            new InvalidSmokeTestModel(),
            new FixedClock(nowUtc),
            log =>
            {
                capturedLog = log;
                return "diagnostic.json";
            });

        Assert.False(result.Success);
        Assert.NotNull(capturedLog);
        Assert.Equal(nowUtc, capturedLog!.TimestampUtc);
        Assert.Equal("diagnostic.json", result.DiagnosticPath);
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

    private sealed class InvalidSmokeTestModel : ILocalAdviceModel
    {
        public string Name => "invalid-smoke-test-model";

        public bool IsAvailable => true;

        public string? Generate(LocalModelAdviceRequest request)
        {
            return "not structured advice";
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
