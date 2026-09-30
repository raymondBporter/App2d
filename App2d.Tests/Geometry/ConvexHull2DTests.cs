using App2d.Core.Geometry;
using System.Numerics;

namespace App2d.Tests.Geometry;

public sealed class ConvexHull2DTests
{
    [Fact]
    public void UnorderedPointsProduceMinimalCounterClockwisePerimeter()
    {
        Vector2[] points =
        [
            new(2, 2), new(1, 1), new(0, 2), new(2, 0), new(0, 0),
            new(1, 0), new(2, 1), new(0, 1), new(0, 0)
        ];

        var hull = PolygonGeometry2D.ConvexHull(points);

        Assert.Equal<Vector2>([new(0, 0), new(2, 0), new(2, 2), new(0, 2)], hull);
    }

    [Fact]
    public void RejectsDegenerateAndNonFiniteInput()
    {
        Assert.Throws<ArgumentNullException>(() => PolygonGeometry2D.ConvexHull(null!));
        Assert.Throws<ArgumentException>(() => PolygonGeometry2D.ConvexHull([new(0, 0), new(1, 0)]));
        Assert.Throws<ArgumentException>(() => PolygonGeometry2D.ConvexHull(
            [new(0, 0), new(1, 0), new(2, 0)]));
        Assert.Throws<ArgumentException>(() => PolygonGeometry2D.ConvexHull(
            [new(0, 0), new(1, 0), new(float.NaN, 1)]));
    }

    [Fact]
    public void LargeCoordinatesKeepTheirOrientation()
    {
        var hull = PolygonGeometry2D.ConvexHull(
            [new(1e30f, 1e30f), new(-1e30f, 1e30f), new(1e30f, -1e30f), new(-1e30f, -1e30f)]);

        Assert.Equal(4, hull.Length);
        Assert.True(PolygonGeometry2D.SignedAreaTwiceDouble(hull) > 0d);
    }
}
