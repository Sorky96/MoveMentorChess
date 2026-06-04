using System;
using System.Drawing;
using MoveMentorChess.Tracking;
using Xunit;

namespace MoveMentorChessServices.Tests.Tracking;

public sealed class BoardImageNormalizerTests
{
    [Fact]
    public void Normalize_CropsPaddedStandardBoard()
    {
        DefaultBoardImageNormalizer normalizer = new();
        using Bitmap padded = new(500, 460);
        using (Graphics graphics = Graphics.FromImage(padded))
        {
            graphics.Clear(Color.FromArgb(32, 32, 32));
            DrawStandardBoard(graphics, left: 60, top: 40, tileSize: 48);
        }

        using Bitmap normalized = normalizer.Normalize(padded);

        Assert.Equal(384, normalized.Width);
        Assert.Equal(384, normalized.Height);
    }

    [Fact]
    public void ExtractSquare_ReturnsInsetSquare()
    {
        DefaultBoardImageNormalizer normalizer = new();
        using Bitmap board = new(400, 400);

        using Bitmap square = normalizer.ExtractSquare(board, screenX: 3, screenY: 4);

        Assert.Equal(38, square.Width);
        Assert.Equal(38, square.Height);
    }

    [Theory]
    [InlineData(-1, 0, "screenX")]
    [InlineData(8, 0, "screenX")]
    [InlineData(0, -1, "screenY")]
    [InlineData(0, 8, "screenY")]
    public void ExtractSquare_RejectsOutOfRangeCoordinates(int screenX, int screenY, string expectedParameterName)
    {
        DefaultBoardImageNormalizer normalizer = new();
        using Bitmap board = new(400, 400);

        ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => normalizer.ExtractSquare(board, screenX, screenY));
        Assert.Equal(expectedParameterName, exception.ParamName);
    }

    [Fact]
    public void Normalize_RejectsNullBitmap()
    {
        DefaultBoardImageNormalizer normalizer = new();

        Assert.Throws<ArgumentNullException>(() => normalizer.Normalize(null!));
    }

    private static void DrawStandardBoard(Graphics graphics, int left, int top, int tileSize)
    {
        using Brush lightSquareBrush = new SolidBrush(TrackingBoardPalette.LightSquare);
        using Brush darkSquareBrush = new SolidBrush(TrackingBoardPalette.DarkSquare);

        for (int y = 0; y < 8; y++)
        {
            for (int x = 0; x < 8; x++)
            {
                Rectangle rect = new(left + (x * tileSize), top + (y * tileSize), tileSize, tileSize);
                graphics.FillRectangle(((x + y) % 2 == 0) ? lightSquareBrush : darkSquareBrush, rect);
            }
        }
    }
}
