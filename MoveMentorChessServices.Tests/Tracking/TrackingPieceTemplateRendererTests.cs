using System.Drawing;
using MoveMentorChess.Tracking;
using Xunit;

namespace MoveMentorChessServices.Tests.Tracking;

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

    [Theory]
    [InlineData(-1)]
    [InlineData(32)]
    public void RenderImageTemplate_RejectsInvalidInset(int inset)
    {
        DefaultTrackingPieceTemplateRenderer renderer = new();
        using Bitmap source = new(16, 16);

        ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => renderer.RenderImageTemplate(source, isLightSquare: true, inset));

        Assert.Equal("inset", exception.ParamName);
        Assert.Contains("between 0 and 31", exception.Message, StringComparison.Ordinal);
        Assert.Contains(inset.ToString(System.Globalization.CultureInfo.InvariantCulture), exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(32)]
    public void RenderTransparentImageTemplate_RejectsInvalidInset(int inset)
    {
        DefaultTrackingPieceTemplateRenderer renderer = new();
        using Bitmap source = new(16, 16);

        ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => renderer.RenderTransparentImageTemplate(source, inset));

        Assert.Equal("inset", exception.ParamName);
        Assert.Contains("between 0 and 31", exception.Message, StringComparison.Ordinal);
        Assert.Contains(inset.ToString(System.Globalization.CultureInfo.InvariantCulture), exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RenderImageTemplate_RejectsNullImage()
    {
        DefaultTrackingPieceTemplateRenderer renderer = new();

        Assert.Throws<ArgumentNullException>(() => renderer.RenderImageTemplate(null!, isLightSquare: true, inset: 0));
    }

    [Fact]
    public void RenderTransparentImageTemplate_RejectsNullImage()
    {
        DefaultTrackingPieceTemplateRenderer renderer = new();

        Assert.Throws<ArgumentNullException>(() => renderer.RenderTransparentImageTemplate(null!, inset: 0));
    }
}
