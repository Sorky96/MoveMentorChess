using MoveMentorChess.Tracking;
using Xunit;

namespace MoveMentorChessServices.Tests;

public sealed class BoardRecognitionOptionsTests
{
    [Fact]
    public void DefaultOptions_AreValid()
    {
        BoardRecognitionOptions.Default.Validate();
    }

    [Fact]
    public void Validate_RejectsInvalidConfidence()
    {
        BoardRecognitionOptions options = BoardRecognitionOptions.Default with
        {
            LearnedRecognitionMinConfidence = 1.5
        };

        Assert.Throws<ArgumentOutOfRangeException>(() => options.Validate());
    }

    [Fact]
    public void Validate_RejectsWeightsThatDoNotAddUpToOne()
    {
        BoardRecognitionOptions options = BoardRecognitionOptions.Default with
        {
            SimilarityConfidenceWeight = 0.9,
            SeparationConfidenceWeight = 0.9
        };

        Assert.Throws<ArgumentException>(() => options.Validate());
    }
}
