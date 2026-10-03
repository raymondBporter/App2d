using App2d.Core.Geometry;
using App2d.Core.Rendering;
using System.Numerics;

namespace App2d.Tests.Rendering;

public sealed class ScreenRectangle2DTests
{
    [Fact]
    public void PositiveSizeCanCreateAndBeReadFromScreenBounds()
    {
        var size = new Size2D(40f, 60f);
        var bounds = ScreenRectangle2D.FromTopLeftAndSize(new Vector2(10f, 20f), size);

        Assert.Equal(new ScreenRectangle2D(10f, 20f, 50f, 80f), bounds);
        Assert.True(bounds.TryGetPositiveSize(out var measured));
        Assert.Equal(size, measured);
        Assert.False(bounds.InsetBy(100f, 0f).TryGetPositiveSize(out _));
        Assert.Throws<ArgumentException>(() => ScreenRectangle2D.FromTopLeftAndSize(Vector2.Zero, default));
    }

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
