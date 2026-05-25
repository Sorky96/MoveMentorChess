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

    private sealed record TestEntry(string Value);

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

    private sealed class TraceListenerFailureException : Exception
    {
    }
}
