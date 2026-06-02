using System;
using System.Collections.Generic;
using System.Drawing;

namespace MoveMentorChess.Tracking;

public sealed class TrackingSquareClassifier
{
    private const string EmptyKey = ".";

    private static readonly Color LightSquareColor = TrackingBoardPalette.LightSquare;
    private static readonly Color DarkSquareColor = TrackingBoardPalette.DarkSquare;

    private readonly ITrackingTemplateVectorizer templateVectorizer;
    private readonly BoardRecognitionOptions options;
    private readonly TrackingTemplateBank learnedTemplates;
    private readonly TrackingTemplateBank coldStartBoardTemplates;
    private readonly TrackingTemplateBank genericShapeTemplates;
    private readonly TrackingTemplateBank genericPieceTemplates;

    public TrackingSquareClassifier(
        ITrackingTemplateVectorizer templateVectorizer,
        BoardRecognitionOptions options,
        TrackingTemplateBank learnedTemplates,
        TrackingTemplateBank coldStartBoardTemplates,
        TrackingTemplateBank genericShapeTemplates,
        TrackingTemplateBank genericPieceTemplates)
    {
        this.templateVectorizer = templateVectorizer ?? throw new ArgumentNullException(nameof(templateVectorizer));
        this.options = options ?? throw new ArgumentNullException(nameof(options));
        this.options.Validate();
        this.learnedTemplates = learnedTemplates ?? throw new ArgumentNullException(nameof(learnedTemplates));
        this.coldStartBoardTemplates = coldStartBoardTemplates ?? throw new ArgumentNullException(nameof(coldStartBoardTemplates));
        this.genericShapeTemplates = genericShapeTemplates ?? throw new ArgumentNullException(nameof(genericShapeTemplates));
        this.genericPieceTemplates = genericPieceTemplates ?? throw new ArgumentNullException(nameof(genericPieceTemplates));
    }

    public bool TryClassifyLearnedSquare(float[] vector, bool isLightSquare, out string? piece, out double confidence)
    {
        ArgumentNullException.ThrowIfNull(vector);

        piece = null;
        confidence = 0;

        string squareSuffix = isLightSquare ? "|L" : "|D";
        string? bestPiece = null;
        double bestDistance = double.MaxValue;

        foreach ((string key, IReadOnlyList<float[]> variants) in learnedTemplates.Enumerate())
        {
            if (!key.EndsWith(squareSuffix, StringComparison.Ordinal))
            {
                continue;
            }

            foreach (float[] variant in variants)
            {
                double distance = ComputeDistance(vector, variant);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestPiece = key[..^2];
                }
            }
        }

        if (bestPiece is null)
        {
            return false;
        }

        piece = bestPiece == EmptyKey ? null : bestPiece;
        confidence = Math.Clamp(1.0 - bestDistance, 0.0, 1.0);
        return confidence >= options.LearnedSquareMinConfidence;
    }

    public bool TryClassifyColdStartSquare(Bitmap squareBitmap, bool isLightSquare, out string? piece, out double confidence)
    {
        ArgumentNullException.ThrowIfNull(squareBitmap);

        piece = null;
        confidence = 0;

        Color estimatedBackground = EstimateBackgroundColor(squareBitmap);
        Color expectedBackground = isLightSquare ? LightSquareColor : DarkSquareColor;
        bool backgroundLooksStandard = ColorDistance(estimatedBackground, expectedBackground) <= options.StandardBackgroundColorMaxDistance;

        string? boardTemplatePiece = null;
        double boardTemplateConfidence = 0;
        _ = TryClassifyWithBoardTemplates(squareBitmap, isLightSquare, out boardTemplatePiece, out boardTemplateConfidence);

        float[] maskVector = templateVectorizer.ToMaskVector(squareBitmap, out double occupancy, out _, out double pieceLuminance, out double backgroundLuminance);

        if (occupancy < options.EmptyOccupancyMax)
        {
            if (!backgroundLooksStandard && boardTemplatePiece is not null && boardTemplatePiece != EmptyKey)
            {
                piece = boardTemplatePiece;
                confidence = boardTemplateConfidence;
            }
            else
            {
                piece = null;
                confidence = Math.Clamp(1.0 - occupancy * options.EmptyOccupancyPenaltyWeight, 0.0, 1.0);
            }
            return true;
        }

        float[] pieceGrayVector = templateVectorizer.ToPieceGrayVector(squareBitmap);

        string? bestPieceByGray = null;
        double bestGrayDistance = double.MaxValue;
        double secondGrayDistance = double.MaxValue;
        foreach ((string key, IReadOnlyList<float[]> variants) in genericPieceTemplates.Enumerate())
        {
            foreach (float[] variant in variants)
            {
                double distance = ComputeDistance(pieceGrayVector, variant);
                if (distance < bestGrayDistance)
                {
                    secondGrayDistance = bestGrayDistance;
                    bestGrayDistance = distance;
                    bestPieceByGray = key;
                }
                else if (distance < secondGrayDistance)
                {
                    secondGrayDistance = distance;
                }
            }
        }

        string? bestTypeByShape = null;
        double bestShapeDistance = double.MaxValue;
        double secondShapeDistance = double.MaxValue;
        foreach ((string key, IReadOnlyList<float[]> variants) in genericShapeTemplates.Enumerate())
        {
            foreach (float[] variant in variants)
            {
                double distance = ComputeDistance(maskVector, variant);
                if (distance < bestShapeDistance)
                {
                    secondShapeDistance = bestShapeDistance;
                    bestShapeDistance = distance;
                    bestTypeByShape = key;
                }
                else if (distance < secondShapeDistance)
                {
                    secondShapeDistance = distance;
                }
            }
        }

        if (bestPieceByGray is null && bestTypeByShape is null)
        {
            return false;
        }

        bool isWhitePiece = EstimatePieceIsWhite(pieceLuminance, backgroundLuminance);
        string? shapePiece = bestTypeByShape is null
            ? null
            : (isWhitePiece ? bestTypeByShape : bestTypeByShape.ToLowerInvariant());

        double grayConfidence = bestPieceByGray is null
            ? 0
            : (Math.Clamp(1.0 - bestGrayDistance, 0.0, 1.0) * options.SimilarityConfidenceWeight)
                + ((secondGrayDistance == double.MaxValue
                    ? Math.Clamp(1.0 - bestGrayDistance, 0.0, 1.0)
                    : Math.Clamp((secondGrayDistance - bestGrayDistance) * options.GenericSeparationScale, 0.0, 1.0)) * options.SeparationConfidenceWeight);

        double shapeConfidence = bestTypeByShape is null
            ? 0
            : (Math.Clamp(1.0 - bestShapeDistance, 0.0, 1.0) * options.SimilarityConfidenceWeight)
                + ((secondShapeDistance == double.MaxValue
                    ? Math.Clamp(1.0 - bestShapeDistance, 0.0, 1.0)
                    : Math.Clamp((secondShapeDistance - bestShapeDistance) * options.GenericSeparationScale, 0.0, 1.0)) * options.SeparationConfidenceWeight);

        if (bestPieceByGray is not null
            && shapePiece is not null
            && string.Equals(bestPieceByGray, shapePiece, StringComparison.Ordinal))
        {
            piece = bestPieceByGray;
            confidence = (grayConfidence * options.ExactGrayConfidenceWeight) + (shapeConfidence * options.ExactShapeConfidenceWeight);
            if (backgroundLooksStandard
                && boardTemplatePiece is not null
                && string.Equals(boardTemplatePiece, piece, StringComparison.Ordinal))
            {
                confidence = ReinforceWithBoardTemplate(confidence, boardTemplateConfidence, options.ExactBoardTemplateConfidenceWeight);
            }
            return confidence >= options.ShapeMinConfidence;
        }

        if (backgroundLooksStandard
            && boardTemplatePiece is not null
            && boardTemplateConfidence >= Math.Max(grayConfidence, shapeConfidence) + options.BoardTemplateAdvantageMargin)
        {
            piece = boardTemplatePiece;
            confidence = boardTemplateConfidence;
            return confidence >= options.BoardTemplateMinConfidence;
        }

        if (shapePiece is not null && shapeConfidence + options.BoardTemplateAdvantageMargin >= grayConfidence)
        {
            piece = shapePiece;
            confidence = shapeConfidence;
            if (backgroundLooksStandard
                && boardTemplatePiece is not null
                && string.Equals(boardTemplatePiece, piece, StringComparison.Ordinal))
            {
                confidence = ReinforceWithBoardTemplate(confidence, boardTemplateConfidence, options.BoardTemplateReinforcementWeight);
            }
            return confidence >= options.ShapeMinConfidence;
        }

        if (bestPieceByGray is not null)
        {
            piece = bestPieceByGray;
            confidence = grayConfidence;
            if (backgroundLooksStandard
                && boardTemplatePiece is not null
                && string.Equals(boardTemplatePiece, piece, StringComparison.Ordinal))
            {
                confidence = ReinforceWithBoardTemplate(confidence, boardTemplateConfidence, options.BoardTemplateReinforcementWeight);
            }
            return confidence >= options.GrayMinConfidence;
        }

        return false;
    }

    private bool TryClassifyWithBoardTemplates(Bitmap squareBitmap, bool isLightSquare, out string? piece, out double confidence)
    {
        piece = null;
        confidence = 0;

        float[] vector = templateVectorizer.ToPieceGrayVector(squareBitmap);
        string squareSuffix = isLightSquare ? "|L" : "|D";

        string? bestKey = null;
        double bestDistance = double.MaxValue;
        double secondBestDistance = double.MaxValue;

        foreach ((string key, IReadOnlyList<float[]> variants) in coldStartBoardTemplates.Enumerate())
        {
            if (!key.EndsWith(squareSuffix, StringComparison.Ordinal))
            {
                continue;
            }

            foreach (float[] variant in variants)
            {
                double distance = ComputeDistance(vector, variant);
                if (distance < bestDistance)
                {
                    secondBestDistance = bestDistance;
                    bestDistance = distance;
                    bestKey = key;
                }
                else if (distance < secondBestDistance)
                {
                    secondBestDistance = distance;
                }
            }
        }

        if (bestKey is null)
        {
            return false;
        }

        string bestPiece = bestKey[..^2];
        piece = bestPiece == EmptyKey ? null : bestPiece;

        double likeness = Math.Clamp(1.0 - bestDistance, 0.0, 1.0);
        double separation = secondBestDistance == double.MaxValue
            ? likeness
            : Math.Clamp((secondBestDistance - bestDistance) * options.BoardTemplateSeparationScale, 0.0, 1.0);
        confidence = (likeness * options.BoardTemplateSimilarityWeight) + (separation * options.BoardTemplateSeparationWeight);
        return true;
    }

    private static double ComputeDistance(float[] left, float[] right)
    {
        double sum = 0;
        for (int i = 0; i < left.Length; i++)
        {
            sum += Math.Abs(left[i] - right[i]);
        }

        return sum / left.Length;
    }

    private static Color EstimateBackgroundColor(Bitmap bitmap)
    {
        List<Color> samples = new();
        int maxX = bitmap.Width - 1;
        int maxY = bitmap.Height - 1;

        foreach (Point point in new[]
        {
            new Point(1, 1),
            new Point(maxX - 1, 1),
            new Point(1, maxY - 1),
            new Point(maxX - 1, maxY - 1),
            new Point(bitmap.Width / 2, 1),
            new Point(bitmap.Width / 2, maxY - 1),
            new Point(1, bitmap.Height / 2),
            new Point(maxX - 1, bitmap.Height / 2)
        })
        {
            samples.Add(bitmap.GetPixel(
                Math.Clamp(point.X, 0, maxX),
                Math.Clamp(point.Y, 0, maxY)));
        }

        int r = 0;
        int g = 0;
        int b = 0;
        foreach (Color sample in samples)
        {
            r += sample.R;
            g += sample.G;
            b += sample.B;
        }

        return Color.FromArgb(r / samples.Count, g / samples.Count, b / samples.Count);
    }

    private static double ColorDistance(Color left, Color right)
    {
        int dr = left.R - right.R;
        int dg = left.G - right.G;
        int db = left.B - right.B;
        return Math.Sqrt((double)dr * dr + (double)dg * dg + (double)db * db);
    }

    private bool EstimatePieceIsWhite(double pieceLuminance, double backgroundLuminance)
    {
        double threshold = backgroundLuminance > options.LightBackgroundLuminanceCutoff
            ? options.LightBackgroundWhitePieceLuminanceThreshold
            : options.DarkBackgroundWhitePieceLuminanceThreshold;
        return pieceLuminance >= threshold;
    }

    private static double ReinforceWithBoardTemplate(double confidence, double boardTemplateConfidence, double boardTemplateWeight)
    {
        return Math.Max(confidence, (confidence * (1.0 - boardTemplateWeight)) + (boardTemplateConfidence * boardTemplateWeight));
    }
}
