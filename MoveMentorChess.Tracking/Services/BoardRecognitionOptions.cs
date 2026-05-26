using System;

namespace MoveMentorChess.Tracking;

public sealed record BoardRecognitionOptions
{
    public static BoardRecognitionOptions Default { get; } = new();

    public double MissingPawnCentralOccupancyMax { get; init; } = 0.08;

    public double MissingPawnOccupancyMax { get; init; } = 0.17;

    public double MissingPawnOccupancyPenaltyWeight { get; init; } = 3.5;

    public double MissingPawnCentralOccupancyPenaltyWeight { get; init; } = 6.0;

    public double LearnedRecognitionMinConfidence { get; init; } = 0.30;

    public double ColdStartRecognitionMinConfidence { get; init; } = 0.34;

    public int MaxEmptyTemplateVariants { get; init; } = 40;

    public int MaxLearnedPieceTemplateVariants { get; init; } = 16;

    public int MaxGenericShapeTemplateVariants { get; init; } = 12;

    public int MaxColdStartBoardTemplateVariants { get; init; } = 16;

    public int MaxGenericPieceTemplateVariants { get; init; } = 12;

    public double LearnedSquareMinConfidence { get; init; } = 0.40;

    public double StandardBackgroundColorMaxDistance { get; init; } = 55.0;

    public double EmptyOccupancyMax { get; init; } = 0.045;

    public double EmptyOccupancyPenaltyWeight { get; init; } = 10.0;

    public double SimilarityConfidenceWeight { get; init; } = 0.70;

    public double SeparationConfidenceWeight { get; init; } = 0.30;

    public double GenericSeparationScale { get; init; } = 6.0;

    public double ExactGrayConfidenceWeight { get; init; } = 0.55;

    public double ExactShapeConfidenceWeight { get; init; } = 0.45;

    public double ExactBoardTemplateConfidenceWeight { get; init; } = 0.25;

    public double BoardTemplateAdvantageMargin { get; init; } = 0.08;

    public double BoardTemplateMinConfidence { get; init; } = 0.30;

    public double ShapeMinConfidence { get; init; } = 0.24;

    public double GrayMinConfidence { get; init; } = 0.25;

    public double BoardTemplateReinforcementWeight { get; init; } = 0.30;

    public double BoardTemplateSeparationScale { get; init; } = 8.0;

    public double BoardTemplateSimilarityWeight { get; init; } = 0.65;

    public double BoardTemplateSeparationWeight { get; init; } = 0.35;

    public double LightBackgroundWhitePieceLuminanceThreshold { get; init; } = 0.55;

    public double DarkBackgroundWhitePieceLuminanceThreshold { get; init; } = 0.48;

    public double LightBackgroundLuminanceCutoff { get; init; } = 0.65;

    public double ReferenceSnapshotMinConfidence { get; init; } = 0.96;

    public double KnownRenderedSnapshotMinConfidence { get; init; } = 0.985;

    public void Validate()
    {
        ValidateNonNegative(MissingPawnCentralOccupancyMax, nameof(MissingPawnCentralOccupancyMax));
        ValidateNonNegative(MissingPawnOccupancyMax, nameof(MissingPawnOccupancyMax));
        ValidateNonNegative(MissingPawnOccupancyPenaltyWeight, nameof(MissingPawnOccupancyPenaltyWeight));
        ValidateNonNegative(MissingPawnCentralOccupancyPenaltyWeight, nameof(MissingPawnCentralOccupancyPenaltyWeight));
        ValidateConfidence(LearnedRecognitionMinConfidence, nameof(LearnedRecognitionMinConfidence));
        ValidateConfidence(ColdStartRecognitionMinConfidence, nameof(ColdStartRecognitionMinConfidence));
        ValidatePositive(MaxEmptyTemplateVariants, nameof(MaxEmptyTemplateVariants));
        ValidatePositive(MaxLearnedPieceTemplateVariants, nameof(MaxLearnedPieceTemplateVariants));
        ValidatePositive(MaxGenericShapeTemplateVariants, nameof(MaxGenericShapeTemplateVariants));
        ValidatePositive(MaxColdStartBoardTemplateVariants, nameof(MaxColdStartBoardTemplateVariants));
        ValidatePositive(MaxGenericPieceTemplateVariants, nameof(MaxGenericPieceTemplateVariants));
        ValidateConfidence(LearnedSquareMinConfidence, nameof(LearnedSquareMinConfidence));
        ValidateNonNegative(StandardBackgroundColorMaxDistance, nameof(StandardBackgroundColorMaxDistance));
        ValidateNonNegative(EmptyOccupancyMax, nameof(EmptyOccupancyMax));
        ValidateNonNegative(EmptyOccupancyPenaltyWeight, nameof(EmptyOccupancyPenaltyWeight));
        ValidateNonNegative(GenericSeparationScale, nameof(GenericSeparationScale));
        ValidateConfidence(ExactGrayConfidenceWeight, nameof(ExactGrayConfidenceWeight));
        ValidateConfidence(ExactShapeConfidenceWeight, nameof(ExactShapeConfidenceWeight));
        ValidateConfidence(ExactBoardTemplateConfidenceWeight, nameof(ExactBoardTemplateConfidenceWeight));
        ValidateNonNegative(BoardTemplateAdvantageMargin, nameof(BoardTemplateAdvantageMargin));
        ValidateConfidence(BoardTemplateMinConfidence, nameof(BoardTemplateMinConfidence));
        ValidateConfidence(ShapeMinConfidence, nameof(ShapeMinConfidence));
        ValidateConfidence(GrayMinConfidence, nameof(GrayMinConfidence));
        ValidateConfidence(BoardTemplateReinforcementWeight, nameof(BoardTemplateReinforcementWeight));
        ValidateNonNegative(BoardTemplateSeparationScale, nameof(BoardTemplateSeparationScale));
        ValidateConfidence(BoardTemplateSimilarityWeight, nameof(BoardTemplateSimilarityWeight));
        ValidateConfidence(BoardTemplateSeparationWeight, nameof(BoardTemplateSeparationWeight));
        ValidateConfidence(LightBackgroundWhitePieceLuminanceThreshold, nameof(LightBackgroundWhitePieceLuminanceThreshold));
        ValidateConfidence(DarkBackgroundWhitePieceLuminanceThreshold, nameof(DarkBackgroundWhitePieceLuminanceThreshold));
        ValidateConfidence(LightBackgroundLuminanceCutoff, nameof(LightBackgroundLuminanceCutoff));
        ValidateConfidence(ReferenceSnapshotMinConfidence, nameof(ReferenceSnapshotMinConfidence));
        ValidateConfidence(KnownRenderedSnapshotMinConfidence, nameof(KnownRenderedSnapshotMinConfidence));
        ValidateWeights(SimilarityConfidenceWeight, SeparationConfidenceWeight, nameof(SimilarityConfidenceWeight), nameof(SeparationConfidenceWeight));
        ValidateWeights(BoardTemplateSimilarityWeight, BoardTemplateSeparationWeight, nameof(BoardTemplateSimilarityWeight), nameof(BoardTemplateSeparationWeight));
        ValidateWeights(ExactGrayConfidenceWeight, ExactShapeConfidenceWeight, nameof(ExactGrayConfidenceWeight), nameof(ExactShapeConfidenceWeight));
    }

    private static void ValidateConfidence(double value, string name)
    {
        if (double.IsNaN(value) || value < 0 || value > 1)
        {
            throw new ArgumentOutOfRangeException(name, value, "Confidence values must be between 0 and 1.");
        }
    }

    private static void ValidateNonNegative(double value, string name)
    {
        if (double.IsNaN(value) || value < 0)
        {
            throw new ArgumentOutOfRangeException(name, value, "Value must be non-negative.");
        }
    }

    private static void ValidatePositive(int value, string name)
    {
        if (value <= 0)
        {
            throw new ArgumentOutOfRangeException(name, value, "Value must be positive.");
        }
    }

    private static void ValidateWeights(double first, double second, string firstName, string secondName)
    {
        ValidateConfidence(first, firstName);
        ValidateConfidence(second, secondName);

        if (Math.Abs((first + second) - 1.0) > 0.0001)
        {
            throw new ArgumentException($"Weights {firstName} and {secondName} must add up to 1.");
        }
    }
}
