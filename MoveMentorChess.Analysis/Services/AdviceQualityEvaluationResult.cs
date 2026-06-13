namespace MoveMentorChess.Analysis;

public sealed record AdviceQualityEvaluationResult(
    int Passed,
    int Failed,
    int Total,
    string ReportMarkdown);
