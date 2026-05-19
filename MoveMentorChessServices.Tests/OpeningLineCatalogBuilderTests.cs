using MoveMentorChess.Domain;
using Xunit;

namespace MoveMentorChessServices.Tests;

public sealed class OpeningLineCatalogBuilderTests
{
    [Fact]
    public void OpeningLineCatalogBuilder_CreatesEscapedStableKeys()
    {
        OpeningLineCatalogItem item = OpeningLineCatalogBuilder.CreateItem(
            "C20",
            "King|Pawn",
            "Main\\Line",
            RepertoireSide.White,
            new OpeningPositionKey("fen|with\\pipes"),
            "fen root",
            12,
            3);

        Assert.Equal(new OpeningKey("C20|King\\|Pawn"), item.OpeningKey);
        Assert.Equal(new OpeningLineKey("C20|King\\|Pawn|Main\\\\Line|White|fen\\|with\\\\pipes"), item.LineKey);
        Assert.Equal("King|Pawn: Main\\Line (C20)", item.DisplayName);
        Assert.Equal(12, item.BookGameCount);
        Assert.Equal(3, item.BookBranchCount);
    }

    [Fact]
    public void OpeningLineCatalogBuilder_UsesCatalogNameWhenOpeningNameIsMissing()
    {
        OpeningLineCatalogItem item = OpeningLineCatalogBuilder.CreateItem(
            "C20",
            "",
            "",
            RepertoireSide.Black,
            new OpeningPositionKey("root"),
            "fen root",
            5,
            2);

        Assert.Equal("King's Pawn Game (C20)", item.DisplayName);
        Assert.Equal(new OpeningKey("C20|"), item.OpeningKey);
    }
}
