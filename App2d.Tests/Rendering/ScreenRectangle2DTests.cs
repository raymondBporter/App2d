using App2d.Rendering;

namespace App2d.Tests.Rendering;

public sealed class ScreenRectangle2DTests
{
    [Fact]
    public void ReportsDeviceSpaceDimensionsAndMidpoint()
    {
        var bounds = new ScreenRectangle2D(10f, 20f, 50f, 80f);

        Assert.Equal(40f, bounds.Width);
        Assert.Equal(60f, bounds.Height);
        Assert.Equal(30f, bounds.MidX);
        Assert.Equal(50f, bounds.MidY);
    }

    [Fact]
    public void ContainsUsesHalfOpenPixelBounds()
    {
        var bounds = new ScreenRectangle2D(10f, 20f, 50f, 80f);

        Assert.True(bounds.Contains(10f, 20f));
        Assert.True(bounds.Contains(49.999f, 79.999f));
        Assert.False(bounds.Contains(50f, 40f));
        Assert.False(bounds.Contains(30f, 80f));
    }

    [Fact]
    public void InflatedByMovesEveryEdgeOutward()
    {
        var bounds = new ScreenRectangle2D(10f, 20f, 50f, 80f);

        Assert.Equal(new ScreenRectangle2D(7f, 16f, 53f, 84f), bounds.InflatedBy(3f, 4f));
    }

    [Fact]
    public void InsetByMovesEveryEdgeInward()
    {
        var bounds = new ScreenRectangle2D(10f, 20f, 50f, 80f);

        Assert.Equal(new ScreenRectangle2D(13f, 24f, 47f, 76f), bounds.InsetBy(3f, 4f));
    }
}

