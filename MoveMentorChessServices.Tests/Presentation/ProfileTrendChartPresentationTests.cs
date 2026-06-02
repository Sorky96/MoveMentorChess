using MoveMentorChess.Presentation.Models;
using Xunit;

namespace MoveMentorChessServices.Tests.Presentation;

public sealed class ProfileTrendChartPresentationTests
{
    [Theory]
    [InlineData("#7DD3FC")]
    [InlineData("7DD3FC")]
    [InlineData("#FF7DD3FC")]
    [InlineData("FF7DD3FC")]
    public void ProfileTrendChartSeries_AcceptsHexStrokeTokens(string strokeHex)
    {
        ProfileTrendChartSeries series = new("Rating", strokeHex, []);

        Assert.Equal(strokeHex, series.StrokeHex);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("#12345")]
    [InlineData("#1234567")]
    [InlineData("#GGD3FC")]
    public void ProfileTrendChartSeries_RejectsInvalidStrokeTokens(string strokeHex)
    {
        Assert.Throws<ArgumentException>(() => new ProfileTrendChartSeries("Rating", strokeHex, []));
    }

    [Fact]
    public void ProfileTrendChartSeries_RejectsNullStrokeToken()
    {
        Assert.Throws<ArgumentNullException>(() => new ProfileTrendChartSeries("Rating", null!, []));
    }
}
