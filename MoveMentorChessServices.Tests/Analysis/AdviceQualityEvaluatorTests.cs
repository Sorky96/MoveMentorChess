using MoveMentorChess.Analysis;
using MoveMentorChess.Domain;
using Xunit;

namespace MoveMentorChessServices.Tests;

public sealed class AdviceQualityEvaluatorTests
{
    [Fact]
    public void Evaluate_UsesInjectedClockAndModelPathInReport()
    {
        DateTime evaluatedAtUtc = new(2026, 6, 1, 18, 45, 0, DateTimeKind.Utc);
        FakeLocalAdviceModel model = new("""
            {
              "short_text": "Use the forcing move.",
              "detailed_text": "The candidate keeps material safe and improves coordination.",
              "training_hint": "Check forcing moves before quiet moves."
            }
            """);

        AdviceQualityEvaluationResult result = AdviceQualityEvaluator.Evaluate(
            model,
            @"C:\models\mentor.gguf",
            new FixedClock(evaluatedAtUtc));

        Assert.Equal(5, result.Passed);
        Assert.Equal(0, result.Failed);
        Assert.Equal(5, result.Total);
        Assert.Contains("Date: 2026-06-01 18:45", result.ReportMarkdown, StringComparison.Ordinal);
        Assert.Contains("Model: mentor.gguf", result.ReportMarkdown, StringComparison.Ordinal);
        Assert.Equal(5, model.Requests.Count);
        Assert.All(model.Requests, request => Assert.False(string.IsNullOrWhiteSpace(request.Prompt)));
    }

    [Fact]
    public void Evaluate_RecordsParseFailuresWithoutThrowing()
    {
        FakeLocalAdviceModel model = new("not structured advice");

        AdviceQualityEvaluationResult result = AdviceQualityEvaluator.Evaluate(
            model,
            "model.gguf",
            new FixedClock(new DateTime(2026, 6, 1, 18, 45, 0, DateTimeKind.Utc)));

        Assert.Equal(0, result.Passed);
        Assert.Equal(5, result.Failed);
        Assert.Contains("could not parse response", result.ReportMarkdown, StringComparison.Ordinal);
    }

    [Fact]
    public void GetDefaultReportPath_UsesInjectedRuntimeEnvironment()
    {
        FakeRuntimeEnvironment environment = new(@"C:\runtime-root");

        string reportPath = AdviceQualityEvaluator.GetDefaultReportPath(environment);

        Assert.Equal(Path.Combine(@"C:\runtime-root", "advice-quality-report.md"), reportPath);
    }

    private sealed class FakeLocalAdviceModel(string response) : ILocalAdviceModel
    {
        public List<LocalModelAdviceRequest> Requests { get; } = [];

        public string Name => "fake";

        public bool IsAvailable => true;

        public string? Generate(LocalModelAdviceRequest request)
        {
            Requests.Add(request);
            return response;
        }
    }

    private sealed class FixedClock(DateTime utcNow) : IClock
    {
        public DateTime UtcNow { get; } = utcNow;
    }

    private sealed class FakeRuntimeEnvironment(string baseDirectory) : ILlamaRuntimeEnvironment
    {
        public string BaseDirectory { get; } = baseDirectory;

        public string CurrentDirectory => BaseDirectory;

        public bool FileExists(string path) => false;

        public bool DirectoryExists(string path) => false;

        public IEnumerable<string> EnumerateFiles(string path, string searchPattern)
            => [];
    }
}
