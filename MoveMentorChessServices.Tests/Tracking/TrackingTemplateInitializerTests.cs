using System.Drawing;
using MoveMentorChess.Tracking;
using Xunit;

namespace MoveMentorChessServices.Tests.Tracking;

public sealed class TrackingTemplateInitializerTests
{
    [Fact]
    public void EnsureInitialized_AddsFallbackColdStartAndGenericTemplates()
    {
        TrackingTemplateBank coldStartBoardTemplates = new();
        TrackingTemplateBank genericShapeTemplates = new();
        TrackingTemplateBank genericPieceTemplates = new();
        TrackingTemplateInitializer initializer = CreateInitializer(
            coldStartBoardTemplates,
            genericShapeTemplates,
            genericPieceTemplates);

        initializer.EnsureInitialized();

        Assert.True(coldStartBoardTemplates.Count > 0);
        Assert.True(genericShapeTemplates.Count > 0);
        Assert.True(genericPieceTemplates.Count > 0);
    }

    [Fact]
    public void EnsureInitialized_IsIdempotent()
    {
        TrackingTemplateBank coldStartBoardTemplates = new();
        TrackingTemplateBank genericShapeTemplates = new();
        TrackingTemplateBank genericPieceTemplates = new();
        TrackingTemplateInitializer initializer = CreateInitializer(
            coldStartBoardTemplates,
            genericShapeTemplates,
            genericPieceTemplates);

        initializer.EnsureInitialized();
        int firstColdStartVariantCount = CountVariants(coldStartBoardTemplates);
        int firstShapeVariantCount = CountVariants(genericShapeTemplates);
        int firstPieceVariantCount = CountVariants(genericPieceTemplates);

        initializer.EnsureInitialized();

        Assert.Equal(firstColdStartVariantCount, CountVariants(coldStartBoardTemplates));
        Assert.Equal(firstShapeVariantCount, CountVariants(genericShapeTemplates));
        Assert.Equal(firstPieceVariantCount, CountVariants(genericPieceTemplates));
    }

    [Fact]
    public void Constructor_RejectsInvalidOptions()
    {
        BoardRecognitionOptions invalidOptions = BoardRecognitionOptions.Default with
        {
            MaxGenericPieceTemplateVariants = 0
        };

        Assert.Throws<ArgumentOutOfRangeException>(() => CreateInitializer(options: invalidOptions));
    }

    private static TrackingTemplateInitializer CreateInitializer(
        TrackingTemplateBank? coldStartBoardTemplates = null,
        TrackingTemplateBank? genericShapeTemplates = null,
        TrackingTemplateBank? genericPieceTemplates = null,
        BoardRecognitionOptions? options = null)
    {
        return new TrackingTemplateInitializer(
            new UnavailablePieceImageRepository(),
            new DefaultTrackingPieceTemplateRenderer(),
            new DefaultTrackingTemplateVectorizer(),
            options ?? BoardRecognitionOptions.Default,
            coldStartBoardTemplates ?? new TrackingTemplateBank(),
            genericShapeTemplates ?? new TrackingTemplateBank(),
            genericPieceTemplates ?? new TrackingTemplateBank());
    }

    private static int CountVariants(TrackingTemplateBank bank)
    {
        int count = 0;
        foreach ((_, IReadOnlyList<float[]> variants) in bank.Enumerate())
        {
            count += variants.Count;
        }

        return count;
    }

    private sealed class UnavailablePieceImageRepository : ITrackingPieceImageRepository
    {
        public bool IsAvailable => false;

        public bool TryLoadPieceImage(string fileName, out Image? image, out string? path)
        {
            image = null;
            path = null;
            return false;
        }
    }
}
