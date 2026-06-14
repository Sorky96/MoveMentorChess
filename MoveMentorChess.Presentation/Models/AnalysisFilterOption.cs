namespace MoveMentorChess.Presentation.Models;

public sealed record AnalysisFilterOption(
    string Label,
    MoveQualityBucket? QualityFilter,
    AnalysisReviewFilter ReviewFilter = AnalysisReviewFilter.All)
{
    public override string ToString() => Label;
}
