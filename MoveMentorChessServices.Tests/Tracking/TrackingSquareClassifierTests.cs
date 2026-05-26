using System.Drawing;
using MoveMentorChess.Tracking;
using Xunit;

namespace MoveMentorChessServices.Tests;

public sealed class TrackingSquareClassifierTests
{
    [Fact]
    public void TryClassifyLearnedSquare_ReturnsNearestMatchingTemplate()
    {
        TrackingTemplateBank learnedTemplates = new();
        learnedTemplates.Add("P|L", [0.1f, 0.2f], maxVariants: 2);
        TrackingSquareClassifier classifier = CreateClassifier(learnedTemplates);

        bool classified = classifier.TryClassifyLearnedSquare([0.1f, 0.2f], isLightSquare: true, out string? piece, out double confidence);

        Assert.True(classified);
        Assert.Equal("P", piece);
        Assert.Equal(1.0, confidence);
    }

    [Fact]
    public void TryClassifyLearnedSquare_IgnoresTemplatesForOtherSquareColor()
    {
        TrackingTemplateBank learnedTemplates = new();
        learnedTemplates.Add("P|D", [0.1f, 0.2f], maxVariants: 2);
        TrackingSquareClassifier classifier = CreateClassifier(learnedTemplates);

        bool classified = classifier.TryClassifyLearnedSquare([0.1f, 0.2f], isLightSquare: true, out string? piece, out double confidence);

        Assert.False(classified);
        Assert.Null(piece);
        Assert.Equal(0, confidence);
    }

    [Fact]
    public void TryClassifyColdStartSquare_ClassifiesStandardEmptySquare()
    {
        TrackingSquareClassifier classifier = CreateClassifier();
        using Bitmap square = new(64, 64);
        using (Graphics graphics = Graphics.FromImage(square))
        using (Brush brush = new SolidBrush(TrackingBoardPalette.LightSquare))
        {
            graphics.FillRectangle(brush, 0, 0, square.Width, square.Height);
        }

        bool classified = classifier.TryClassifyColdStartSquare(square, isLightSquare: true, out string? piece, out double confidence);

        Assert.True(classified);
        Assert.Null(piece);
        Assert.True(confidence > 0.9);
    }

    [Fact]
    public void Constructor_RejectsInvalidOptions()
    {
        BoardRecognitionOptions invalidOptions = BoardRecognitionOptions.Default with
        {
            LearnedSquareMinConfidence = 1.5
        };

        Assert.Throws<ArgumentOutOfRangeException>(
            () => CreateClassifier(options: invalidOptions));
    }

    private static TrackingSquareClassifier CreateClassifier(
        TrackingTemplateBank? learnedTemplates = null,
        BoardRecognitionOptions? options = null)
    {
        return new TrackingSquareClassifier(
            new DefaultTrackingTemplateVectorizer(),
            options ?? BoardRecognitionOptions.Default,
            learnedTemplates ?? new TrackingTemplateBank(),
            new TrackingTemplateBank(),
            new TrackingTemplateBank(),
            new TrackingTemplateBank());
    }
}
