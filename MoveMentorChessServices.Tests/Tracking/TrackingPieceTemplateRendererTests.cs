using System.Drawing;
using MoveMentorChess.Tracking;
using Xunit;

namespace MoveMentorChessServices.Tests;

public sealed class TrackingPieceTemplateRendererTests
{
    [Fact]
    public void RenderEmptyBoardSquare_UsesExpectedSquareColor()
    {
        DefaultTrackingPieceTemplateRenderer renderer = new();

        using Bitmap lightSquare = renderer.RenderEmptyBoardSquare(isLightSquare: true);
        using Bitmap darkSquare = renderer.RenderEmptyBoardSquare(isLightSquare: false);

        Assert.Equal(new Size(64, 64), lightSquare.Size);
        Assert.Equal(Color.FromArgb(238, 238, 210).ToArgb(), lightSquare.GetPixel(0, 0).ToArgb());
        Assert.Equal(Color.FromArgb(118, 150, 86).ToArgb(), darkSquare.GetPixel(0, 0).ToArgb());
    }

    [Fact]
    public void RenderTransparentImageTemplate_KeepsTransparentBackground()
    {
        DefaultTrackingPieceTemplateRenderer renderer = new();
        using Bitmap source = new(16, 16);
        using Graphics graphics = Graphics.FromImage(source);
        graphics.Clear(Color.Red);

        using Bitmap template = renderer.RenderTransparentImageTemplate(source, inset: 8);

        Assert.Equal(new Size(64, 64), template.Size);
        Assert.Equal(0, template.GetPixel(0, 0).A);
        Assert.True(template.GetPixel(32, 32).A > 0);
    }

    [Fact]
    public void RenderFallbackTemplate_DrawsPieceOnSquare()
    {
        DefaultTrackingPieceTemplateRenderer renderer = new();

        using Bitmap template = renderer.RenderFallbackTemplate("K", isWhitePiece: true, isLightSquare: true);

        Assert.Equal(new Size(64, 64), template.Size);
        Assert.NotEqual(Color.FromArgb(238, 238, 210).ToArgb(), template.GetPixel(32, 32).ToArgb());
    }
}
