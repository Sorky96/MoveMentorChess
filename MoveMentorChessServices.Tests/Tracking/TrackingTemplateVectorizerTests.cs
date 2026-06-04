using System.Drawing;
using MoveMentorChess.Tracking;
using Xunit;

namespace MoveMentorChessServices.Tests.Tracking;

public sealed class TrackingTemplateVectorizerTests
{
    [Fact]
    public void ToVector_ReturnsNormalizedGrayValues()
    {
        DefaultTrackingTemplateVectorizer vectorizer = new();
        using Bitmap bitmap = new(8, 8);
        using Graphics graphics = Graphics.FromImage(bitmap);
        graphics.Clear(Color.White);

        float[] vector = vectorizer.ToVector(bitmap);

        Assert.Equal(16 * 16, vector.Length);
        Assert.All(vector, value => Assert.Equal(1f, value));
    }

    [Fact]
    public void ToTemplateGrayVector_AppliesAlpha()
    {
        DefaultTrackingTemplateVectorizer vectorizer = new();
        using Bitmap bitmap = new(16, 16);
        using Graphics graphics = Graphics.FromImage(bitmap);
        graphics.Clear(Color.Transparent);

        float[] vector = vectorizer.ToTemplateGrayVector(bitmap);

        Assert.Equal(16 * 16, vector.Length);
        Assert.All(vector, value => Assert.Equal(0f, value));
    }

    [Fact]
    public void ToMaskVector_ReturnsMaskAndMetrics()
    {
        DefaultTrackingTemplateVectorizer vectorizer = new();
        using Bitmap bitmap = new(24, 24);
        using Graphics graphics = Graphics.FromImage(bitmap);
        graphics.Clear(Color.White);
        using Brush brush = new SolidBrush(Color.Black);
        graphics.FillRectangle(brush, 8, 8, 8, 8);

        float[] vector = vectorizer.ToMaskVector(
            bitmap,
            out double occupancy,
            out double centralOccupancy,
            out double pieceLuminance,
            out double backgroundLuminance);

        Assert.Equal(24 * 24, vector.Length);
        Assert.InRange(occupancy, 0.0, 1.0);
        Assert.True(centralOccupancy > occupancy);
        Assert.True(pieceLuminance < backgroundLuminance);
    }

    [Fact]
    public void ToTemplateMaskVector_UsesAlphaAndDarkness()
    {
        DefaultTrackingTemplateVectorizer vectorizer = new();
        using Bitmap bitmap = new(24, 24);
        bitmap.SetPixel(0, 0, Color.FromArgb(255, 0, 0, 0));

        float[] vector = vectorizer.ToTemplateMaskVector(bitmap);

        Assert.Equal(24 * 24, vector.Length);
        Assert.Equal(1f, vector[0]);
    }
}
