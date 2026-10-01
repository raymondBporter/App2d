using App2d.Core.Geometry;
using System.Numerics;

namespace App2d.Tests.Geometry;

public sealed class Interval1DTests
{
    [Fact]
    public void LengthContainmentAndOverlapIncludeTheEnds()
    {
        var interval = new Interval1D(-1, 3);
        Assert.Equal(4, interval.Length);
        Assert.True(interval.Contains(-1));
        Assert.True(interval.Contains(3));
        Assert.False(interval.Contains(3.01f));
        Assert.True(interval.Overlaps(new(3, 5)));
        Assert.True(new Interval1D(3, 5).Overlaps(interval));
        Assert.False(interval.Overlaps(new(3.01f, 5)));
        Assert.True(interval.Overlaps(new(0, 0)));
        Assert.Equal(0, new Interval1D(2, 2).Length);
    }

    [Fact]
    public void ProjectionsProduceIntervals()
    {
        ReadOnlySpan<Vector2> square = [new(0f, 0f), new(2f, 0f), new(2f, 2f), new(0f, 2f)];
        var polygon = Projection2D.Polygon(square, Vector2.UnitX);
        Assert.Equal(0f, polygon.Min, 3);
        Assert.Equal(2f, polygon.Max, 3);

        var capsule = Projection2D.Capsule(new Vector2(-1f, 0f), new Vector2(3f, 0f), 0.5f, Vector2.UnitX);
        Assert.Equal(-1.5f, capsule.Min, 3);
        Assert.Equal(3.5f, capsule.Max, 3);

        var ellipse = Projection2D.Ellipse(new(1, 1), new(2, 1), Vector2.UnitX);
        Assert.Equal(-1f, ellipse.Min, 5);
        Assert.Equal(3f, ellipse.Max, 5);
        var diagonal = Projection2D.Ellipse(default, new(2, 1), Vector2.Normalize(new(1, 1)));
        Assert.Equal(MathF.Sqrt(2.5f), diagonal.Max, 5);
        Assert.Equal(-diagonal.Max, diagonal.Min, 5);
    }
}
