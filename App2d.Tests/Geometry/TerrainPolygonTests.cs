using App2d.Core.Shapes;
using System.Numerics;

namespace App2d.Tests.Geometry;

public sealed class TerrainPolygonTests
{
    [Fact]
    public void FilledPolylineHasExpectedAreaAndDoesNotFillValleys()
    {
        var ground = SimplePolygon2D.Builder.BelowPolyline([new(0, 4), new(5, 0), new(10, 4)], -10f);
        Assert.Equal(120f, ground.Area, 4);
        Assert.True(ground.ContainsPoint(new(5f, -1f)));
        Assert.False(ground.ContainsPoint(new(5f, 1f)));
        Assert.False(ground.ContainsPoint(new(-1f, -1f)));
        Assert.False(ground.ContainsPoint(new(5f, -11f)));
    }

    [Fact]
    public void FilledPolylineRejectsInvalidGroundRegions()
    {
        Assert.Throws<ArgumentException>(() => SimplePolygon2D.Builder.BelowPolyline([new(0, 0), new(0, 1)], -10f));
        Assert.Throws<ArgumentException>(() => SimplePolygon2D.Builder.BelowPolyline([new(0, 0), new(-1, 1)], -10f));
        Assert.Throws<ArgumentException>(() => SimplePolygon2D.Builder.BelowPolyline([new(0, 0), new(1, -10)], -10f));
    }
}
