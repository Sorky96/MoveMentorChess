using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

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
        if (screenX is < 0 or > 7)
        {
            throw new ArgumentOutOfRangeException(nameof(screenX), screenX, "screenX must be between 0 and 7.");
        }

        if (screenY is < 0 or > 7)
        {
            throw new ArgumentOutOfRangeException(nameof(screenY), screenY, "screenY must be between 0 and 7.");
        }

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

        matchingPixels = CountBoardColoredPixels(boardImage, rowMatches, columnMatches);

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

    private static int CountBoardColoredPixels(Bitmap boardImage, int[] rowMatches, int[] columnMatches)
    {
        int bytesPerPixel = GetBytesPerPixel(boardImage.PixelFormat);
        if (bytesPerPixel == 0)
        {
            return CountBoardColoredPixelsWithGetPixel(boardImage, rowMatches, columnMatches);
        }

        Rectangle bounds = new(0, 0, boardImage.Width, boardImage.Height);
        BitmapData? data = null;

        try
        {
            data = boardImage.LockBits(bounds, ImageLockMode.ReadOnly, boardImage.PixelFormat);
            int stride = data.Stride;
            int rowByteCount = Math.Abs(stride);
            byte[] pixels = new byte[rowByteCount * boardImage.Height];
            Marshal.Copy(data.Scan0, pixels, 0, pixels.Length);

            int matchingPixels = 0;
            for (int y = 0; y < boardImage.Height; y++)
            {
                int rowOffset = stride >= 0
                    ? y * stride
                    : (boardImage.Height - 1 - y) * rowByteCount;

                for (int x = 0; x < boardImage.Width; x++)
                {
                    int pixelOffset = rowOffset + x * bytesPerPixel;
                    int blue = pixels[pixelOffset];
                    int green = pixels[pixelOffset + 1];
                    int red = pixels[pixelOffset + 2];
                    if (IsBoardColor(red, green, blue))
                    {
                        matchingPixels++;
                        rowMatches[y]++;
                        columnMatches[x]++;
                    }
                }
            }

            return matchingPixels;
        }
        finally
        {
            if (data is not null)
            {
                boardImage.UnlockBits(data);
            }
        }
    }

    private static int CountBoardColoredPixelsWithGetPixel(Bitmap boardImage, int[] rowMatches, int[] columnMatches)
    {
        int matchingPixels = 0;
        for (int y = 0; y < boardImage.Height; y++)
        {
            for (int x = 0; x < boardImage.Width; x++)
            {
                Color pixel = boardImage.GetPixel(x, y);
                if (IsBoardColor(pixel.R, pixel.G, pixel.B))
                {
                    matchingPixels++;
                    rowMatches[y]++;
                    columnMatches[x]++;
                }
            }
        }

        return matchingPixels;
    }

    private static int GetBytesPerPixel(PixelFormat pixelFormat)
    {
        return pixelFormat switch
        {
            PixelFormat.Format24bppRgb => 3,
            PixelFormat.Format32bppArgb
                or PixelFormat.Format32bppPArgb
                or PixelFormat.Format32bppRgb => 4,
            _ => 0
        };
    }

    private static bool IsBoardColor(int red, int green, int blue)
    {
        return ColorDistance(red, green, blue, LightSquareColor) <= 58
            || ColorDistance(red, green, blue, DarkSquareColor) <= 58;
    }

    private static double ColorDistance(Color left, Color right)
    {
        return ColorDistance(left.R, left.G, left.B, right);
    }

    private static double ColorDistance(int red, int green, int blue, Color right)
    {
        int dr = red - right.R;
        int dg = green - right.G;
        int db = blue - right.B;
        return Math.Sqrt((double)dr * dr + (double)dg * dg + (double)db * db);
    }
}
