namespace MoveMentorChess.Presentation.Models;

public enum ProfileTrendChartKind
{
    Line,
    Bars
}

public sealed record ProfileTrendChartPoint(string Label, double? Value);

public sealed record ProfileTrendChartSeries(
    string Name,
    string StrokeHex,
    IReadOnlyList<ProfileTrendChartPoint> Points,
    ProfileTrendChartKind Kind = ProfileTrendChartKind.Line);
