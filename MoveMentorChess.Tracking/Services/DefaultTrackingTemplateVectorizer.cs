using System;
using System.Drawing;

namespace MoveMentorChess.Tracking;

public sealed class DefaultTrackingTemplateVectorizer : ITrackingTemplateVectorizer
{
    public float[] ToVector(Bitmap bitmap)
    {
        using Bitmap resized = new(bitmap, new Size(16, 16));
        float[] vector = new float[16 * 16];
        int index = 0;

        for (int y = 0; y < resized.Height; y++)
        {
            for (int x = 0; x < resized.Width; x++)
            {
                Color pixel = resized.GetPixel(x, y);
                vector[index++] = (pixel.R + pixel.G + pixel.B) / (255f * 3f);
            }
        }

        return vector;
    }

    public float[] ToPieceGrayVector(Bitmap bitmap)
    {
        using Bitmap resized = new(bitmap, new Size(16, 16));
        Color background = EstimateBackgroundColor(resized);
        float[] vector = new float[16 * 16];
        int index = 0;

        for (int y = 0; y < resized.Height; y++)
        {
            for (int x = 0; x < resized.Width; x++)
            {
                Color pixel = resized.GetPixel(x, y);
                double distance = ColorDistance(pixel, background);
                float weight = (float)Math.Clamp((distance - 12.0) / 90.0, 0.0, 1.0);
                float gray = (pixel.R + pixel.G + pixel.B) / (255f * 3f);
                vector[index++] = gray * weight;
            }
        }

        return vector;
    }

    public float[] ToBoardTemplateVector(Bitmap bitmap)
    {
        using Bitmap cropped = CropSquareMargins(bitmap);
        return ToPieceGrayVector(cropped);
    }

    public float[] ToTemplateGrayVector(Bitmap bitmap)
    {
        using Bitmap resized = new(bitmap, new Size(16, 16));
        float[] vector = new float[16 * 16];
        int index = 0;

        for (int y = 0; y < resized.Height; y++)
        {
            for (int x = 0; x < resized.Width; x++)
            {
                Color pixel = resized.GetPixel(x, y);
                float alpha = pixel.A / 255f;
                float gray = (pixel.R + pixel.G + pixel.B) / (255f * 3f);
                vector[index++] = gray * alpha;
            }
        }

        return vector;
    }

    public float[] ToMaskVector(Bitmap bitmap, out double occupancy, out double centralOccupancy, out double pieceLuminance, out double backgroundLuminance)
    {
        using Bitmap resized = new(bitmap, new Size(24, 24));
        Color background = EstimateBackgroundColor(resized);
        backgroundLuminance = GetLuminance(background);

        float[] vector = new float[24 * 24];
        double occupancySum = 0;
        double centralOccupancySum = 0;
        int centralSamples = 0;
        double weightedLuminanceSum = 0;
        double weightSum = 0;
        int index = 0;

        for (int y = 0; y < resized.Height; y++)
        {
            for (int x = 0; x < resized.Width; x++)
            {
                Color pixel = resized.GetPixel(x, y);
                double distance = ColorDistance(pixel, background);
                float weight = (float)Math.Clamp((distance - 12.0) / 90.0, 0.0, 1.0);
                vector[index++] = weight;
                occupancySum += weight;
                if (x >= 6 && x < 18 && y >= 6 && y < 18)
                {
                    centralOccupancySum += weight;
                    centralSamples++;
                }
                weightedLuminanceSum += GetLuminance(pixel) * weight;
                weightSum += weight;
            }
        }

        occupancy = occupancySum / vector.Length;
        centralOccupancy = centralSamples > 0 ? centralOccupancySum / centralSamples : occupancy;
        pieceLuminance = weightSum > 0.0001
            ? weightedLuminanceSum / weightSum
            : backgroundLuminance;
        return vector;
    }

    public float[] ToTemplateMaskVector(Bitmap bitmap)
    {
        using Bitmap resized = new(bitmap, new Size(24, 24));
        float[] vector = new float[24 * 24];
        int index = 0;

        for (int y = 0; y < resized.Height; y++)
        {
            for (int x = 0; x < resized.Width; x++)
            {
                Color pixel = resized.GetPixel(x, y);
                float alpha = pixel.A / 255f;
                float darkness = 1f - ((pixel.R + pixel.G + pixel.B) / (255f * 3f));
                vector[index++] = Math.Clamp(Math.Max(alpha, darkness * alpha), 0f, 1f);
            }
        }

        return vector;
    }

    private static Bitmap CropSquareMargins(Bitmap bitmap)
    {
        int insetX = Math.Max(1, (int)Math.Round(bitmap.Width * 0.12));
        int insetY = Math.Max(1, (int)Math.Round(bitmap.Height * 0.12));
        Rectangle source = Rectangle.FromLTRB(
            insetX,
            insetY,
            Math.Max(insetX + 1, bitmap.Width - insetX),
            Math.Max(insetY + 1, bitmap.Height - insetY));
        return bitmap.Clone(source, bitmap.PixelFormat);
    }

    private static Color EstimateBackgroundColor(Bitmap bitmap)
    {
        List<Color> samples = new();
        int maxX = bitmap.Width - 1;
        int maxY = bitmap.Height - 1;

        foreach (Point point in new[]
        {
            new Point(1, 1),
            new Point(maxX - 1, 1),
            new Point(1, maxY - 1),
            new Point(maxX - 1, maxY - 1),
            new Point(bitmap.Width / 2, 1),
            new Point(bitmap.Width / 2, maxY - 1),
            new Point(1, bitmap.Height / 2),
            new Point(maxX - 1, bitmap.Height / 2)
        })
        {
            samples.Add(bitmap.GetPixel(
                Math.Clamp(point.X, 0, maxX),
                Math.Clamp(point.Y, 0, maxY)));
        }

        int r = 0;
        int g = 0;
        int b = 0;
        foreach (Color sample in samples)
        {
            r += sample.R;
            g += sample.G;
            b += sample.B;
        }

        return Color.FromArgb(r / samples.Count, g / samples.Count, b / samples.Count);
    }

    private static double ColorDistance(Color left, Color right)
    {
        int dr = left.R - right.R;
        int dg = left.G - right.G;
        int db = left.B - right.B;
        return Math.Sqrt((double)dr * dr + (double)dg * dg + (double)db * db);
    }

    private static double GetLuminance(Color color)
    {
        return (color.R + color.G + color.B) / (255.0 * 3.0);
    }
}
