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
}
