using MoveMentorChess.Tracking;
using Xunit;

namespace MoveMentorChessServices.Tests;

public sealed class BoardPositionRecognizerConstructorTests
{
    [Fact]
    public void ThreeArgumentInfrastructureConstructor_UsesDefaultVectorizer()
    {
        BoardPositionRecognizer recognizer = new(
            new DirectoryTrackingPieceImageRepository(null),
            new DefaultTrackingPieceTemplateRenderer(),
            new DefaultTrackingTemplatePathResolver());

        Assert.False(recognizer.HasTemplates);
    }

    [Fact]
    public void FullInfrastructureConstructor_AcceptsRecognitionOptions()
    {
        BoardPositionRecognizer recognizer = new(
            new DirectoryTrackingPieceImageRepository(null),
            new DefaultTrackingPieceTemplateRenderer(),
            new DefaultTrackingTemplateVectorizer(),
            new DefaultTrackingTemplatePathResolver(),
            new DefaultBoardImageNormalizer(),
            BoardRecognitionOptions.Default);

        Assert.False(recognizer.HasTemplates);
    }

    [Fact]
    public void FullInfrastructureConstructor_RejectsInvalidRecognitionOptions()
    {
        BoardRecognitionOptions invalidOptions = BoardRecognitionOptions.Default with
        {
            MaxGenericPieceTemplateVariants = 0
        };

        Assert.Throws<ArgumentOutOfRangeException>(() => new BoardPositionRecognizer(
            new DirectoryTrackingPieceImageRepository(null),
            new DefaultTrackingPieceTemplateRenderer(),
            new DefaultTrackingTemplateVectorizer(),
            new DefaultTrackingTemplatePathResolver(),
            new DefaultBoardImageNormalizer(),
            invalidOptions));
    }
}
