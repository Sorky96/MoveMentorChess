using System.Drawing;
using MoveMentorChess.Tracking;
using Xunit;

namespace MoveMentorChessServices.Tests;

public sealed class TrackingColdStartBoardRecognizerTests
{
    [Fact]
    public void TryRecognize_ReturnsFalseWithoutGenericTemplates()
    {
        TrackingColdStartBoardRecognizer recognizer = CreateRecognizer();
        using Bitmap board = new(384, 384);

        bool recognized = recognizer.TryRecognize(board, whiteAtBottom: true, out string placementFen, out double confidence);

        Assert.False(recognized);
        Assert.Equal(string.Empty, placementFen);
        Assert.Equal(0, confidence);
    }

    [Fact]
    public void TryRecognizeNormalized_RejectsNullImage()
    {
        TrackingColdStartBoardRecognizer recognizer = CreateRecognizer();

        Assert.Throws<ArgumentNullException>(
            () => recognizer.TryRecognizeNormalized(null!, whiteAtBottom: true, out _, out _));
    }

    [Fact]
    public void Constructor_RejectsInvalidOptions()
    {
        BoardRecognitionOptions invalidOptions = BoardRecognitionOptions.Default with
        {
            ColdStartRecognitionMinConfidence = -0.1
        };

        Assert.Throws<ArgumentOutOfRangeException>(() => CreateRecognizer(options: invalidOptions));
    }

    private static TrackingColdStartBoardRecognizer CreateRecognizer(
        IBoardImageNormalizer? normalizer = null,
        ITrackingTemplateVectorizer? vectorizer = null,
        BoardRecognitionOptions? options = null,
        TrackingTemplateBank? coldStartBoardTemplates = null,
        TrackingTemplateBank? genericShapeTemplates = null,
        TrackingTemplateBank? genericPieceTemplates = null)
    {
        BoardRecognitionOptions recognitionOptions = options ?? BoardRecognitionOptions.Default;
        ITrackingTemplateVectorizer templateVectorizer = vectorizer ?? new DefaultTrackingTemplateVectorizer();
        TrackingTemplateBank learnedTemplates = new();
        TrackingTemplateBank coldStartTemplates = coldStartBoardTemplates ?? new TrackingTemplateBank();
        TrackingTemplateBank shapeTemplates = genericShapeTemplates ?? new TrackingTemplateBank();
        TrackingTemplateBank pieceTemplates = genericPieceTemplates ?? new TrackingTemplateBank();
        TrackingSquareClassifier classifier = new(
            templateVectorizer,
            recognitionOptions,
            learnedTemplates,
            coldStartTemplates,
            shapeTemplates,
            pieceTemplates);

        return new TrackingColdStartBoardRecognizer(
            normalizer ?? new DefaultBoardImageNormalizer(),
            classifier,
            recognitionOptions,
            shapeTemplates,
            pieceTemplates);
    }
}
