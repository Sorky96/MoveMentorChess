using System;
using System.Drawing;

namespace MoveMentorChess.Tracking;

public sealed class DefaultBoardImageNormalizer : IBoardImageNormalizer
{
    private static readonly Color LightSquareColor = TrackingBoardPalette.LightSquare;
    private static readonly Color DarkSquareColor = TrackingBoardPalette.DarkSquare;

    public Bitmap Normalize(Bitmap boardImage)
    {
        ArgumentNullException.ThrowIfNull(boardImage);

        Rectangle detectedBounds = DetectBoardBounds(boardImage);
        return boardImage.Clone(detectedBounds, boardImage.PixelFormat);
    }

    public Bitmap ExtractSquare(Bitmap boardImage, int screenX, int screenY)
    {
        ArgumentNullException.ThrowIfNull(boardImage);

        int left = (int)Math.Round(screenX * (double)boardImage.Width / 8.0);
        int top = (int)Math.Round(screenY * (double)boardImage.Height / 8.0);
        int right = (int)Math.Round((screenX + 1) * (double)boardImage.Width / 8.0);
        int bottom = (int)Math.Round((screenY + 1) * (double)boardImage.Height / 8.0);
        int insetX = Math.Max(1, (int)Math.Round((right - left) * 0.12));
        int insetY = Math.Max(1, (int)Math.Round((bottom - top) * 0.12));
        Rectangle source = Rectangle.FromLTRB(
            left + insetX,
            top + insetY,
            Math.Max(left + insetX + 1, right - insetX),
            Math.Max(top + insetY + 1, bottom - insetY));
        return boardImage.Clone(source, boardImage.PixelFormat);
    }

    private static Rectangle DetectBoardBounds(Bitmap boardImage)
    {
        int[] rowMatches = new int[boardImage.Height];
        int[] columnMatches = new int[boardImage.Width];
        int matchingPixels = 0;

        for (int y = 0; y < boardImage.Height; y++)
        {
            for (int x = 0; x < boardImage.Width; x++)
            {
                Color pixel = boardImage.GetPixel(x, y);
                if (ColorDistance(pixel, LightSquareColor) <= 58
                    || ColorDistance(pixel, DarkSquareColor) <= 58)
                {
                    matchingPixels++;
                    rowMatches[y]++;
                    columnMatches[x]++;
                }
            }
        }

        int totalPixels = boardImage.Width * boardImage.Height;
        if (matchingPixels < totalPixels / 12)
        {
            return new Rectangle(0, 0, boardImage.Width, boardImage.Height);
        }

        int rowThreshold = Math.Max(8, boardImage.Width / 5);
        int columnThreshold = Math.Max(8, boardImage.Height / 5);
        int minY = FindFirstIndex(rowMatches, count => count >= rowThreshold);
        int maxY = FindLastIndex(rowMatches, count => count >= rowThreshold);
        int minX = FindFirstIndex(columnMatches, count => count >= columnThreshold);
        int maxX = FindLastIndex(columnMatches, count => count >= columnThreshold);

        if (minX < 0 || minY < 0 || maxX <= minX || maxY <= minY)
        {
            return new Rectangle(0, 0, boardImage.Width, boardImage.Height);
        }

        int width = maxX - minX + 1;
        int height = maxY - minY + 1;
        int side = Math.Min(Math.Max(width, height), Math.Min(boardImage.Width, boardImage.Height));
        int centerX = minX + width / 2;
        int centerY = minY + height / 2;
        int left = Math.Clamp(centerX - side / 2, 0, boardImage.Width - side);
        int top = Math.Clamp(centerY - side / 2, 0, boardImage.Height - side);
        return new Rectangle(left, top, side, side);
    }

    private static int FindFirstIndex(int[] values, Func<int, bool> predicate)
    {
        for (int i = 0; i < values.Length; i++)
        {
            if (predicate(values[i]))
            {
                return i;
            }
        }

        return -1;
    }

    private static int FindLastIndex(int[] values, Func<int, bool> predicate)
    {
        for (int i = values.Length - 1; i >= 0; i--)
        {
            if (predicate(values[i]))
            {
                return i;
            }
        }

        return -1;
    }

    private static double ColorDistance(Color left, Color right)
    {
        int dr = left.R - right.R;
        int dg = left.G - right.G;
        int db = left.B - right.B;
        return Math.Sqrt((double)dr * dr + (double)dg * dg + (double)db * db);
    }
}
