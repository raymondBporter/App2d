using App2d.Core;
using App2d.Core.Collision.Queries;
using App2d.Core.Geometry;
using App2d.Core.Shapes;
using System.Numerics;

namespace App2d.Tests.Geometry;

public sealed class LinearGeometry2DTests
{
    [Fact]
    public void LinesExtendBothWaysWhileRaysStartAtTheirOrigin()
    {
        var line = Line2D.FromPoints(new(2, 3), new(5, 7));
        var ray = Ray2D.FromPoints(new(2, 3), new(5, 7));
        Assert.True(line.IsValid);
        Assert.True(ray.IsValid);
        Assert.Equal(new Vector2(.6f, .8f), line.Direction);
        Assert.Equal(line.Direction, ray.Direction);
        Assert.Equal(new Vector2(-1, -1), line.GetPoint(-5));
        Assert.Equal(new Vector2(5, 7), ray.GetPoint(5));
        Assert.Equal(line.GetPoint(-5), line.PointAt(-5));
        Assert.Equal(ray.GetPoint(5), ray.PointAt(5));
        Assert.True(line.ContainsPoint(line.GetPoint(-5)));
        Assert.False(ray.ContainsPoint(line.GetPoint(-5)));
        Assert.Throws<ArgumentOutOfRangeException>(() => ray.GetPoint(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => ray.PointAt(-1));
    }

    [Fact]
    public void FromPointsHandlesVeryDistantOriginsWithoutOverflowingTheDirection()
    {
        var from = new Vector2(float.MaxValue, 0);
        var to = new Vector2(-float.MaxValue, 0);
        var line = Line2D.FromPoints(from, to);
        var ray = Ray2D.FromPoints(from, to);
        Assert.Equal(-Vector2.UnitX, line.Direction);
        Assert.Equal(line.Direction, ray.Direction);
        Assert.Equal(Vector2.Zero, line.PointAt(float.MaxValue));
        Assert.Equal(Vector2.Zero, ray.PointAt(float.MaxValue));
    }

    [Fact]
    public void WhichSideUsesTheRequestedDeterminantAndDistanceTolerance()
    {
        var line = Line2D.FromPoints(new(10, 20), new(13, 20));
        Assert.Equal(1, line.WhichSide(new(12, 19)));
        Assert.Equal(-1, line.WhichSide(new(12, 21)));
        Assert.Equal(0, line.WhichSide(new(-100, 20)));
        Assert.Equal(0, line.WhichSide(new(12, 19.875f), tolerance: .125f));
        Assert.Equal(1, line.WhichSide(new(12, 19.75f), tolerance: .125f));
        Assert.Equal(0, line.WhichSide(new(12, 20.125f), tolerance: .125f));
        Assert.Equal(-1, Line2D.FromPoints(new(13, 20), new(10, 20)).WhichSide(new(12, 19)));
        Assert.Throws<ArgumentOutOfRangeException>(() => line.WhichSide(default, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => line.WhichSide(default, float.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => line.WhichSide(new(float.PositiveInfinity, 0)));
        Assert.Throws<ArgumentOutOfRangeException>(() => default(Line2D).WhichSide(default));
    }

    [Theory]
    [InlineData(float.Epsilon)]
    [InlineData(1f)]
    [InlineData(1e30f)]
    [InlineData(float.MaxValue)]
    public void DirectionsNormalizeWithoutOverflowOrUnderflow(float scale)
    {
        var ray = new Ray2D(default, new(scale, scale));
        var line = new Line2D(default, new(scale, scale));
        Assert.Equal(1 / MathF.Sqrt(2), ray.Direction.X, 6);
        Assert.Equal(ray.Direction.X, ray.Direction.Y);
        Assert.Equal(ray.Direction, line.Direction);
        Assert.Equal(new Vector2(2, 2), ClosestPoint2D.OnLine(new(1, 3), default, new(scale, scale)));
        Assert.Equal(MathF.Sqrt(2), Distance2D.DistanceToLine(new(1, 3), default, new(scale, scale)), 5);
    }

    [Fact]
    public void PointQueriesClampAtRayOriginAndAcceptRawNonUnitDirections()
    {
        var origin = new Vector2(1, 2);
        var direction = new Vector2(4, 0);
        var point = new Vector2(-2, 6);
        var line = new Line2D(origin, direction);
        var ray = new Ray2D(origin, direction);
        Assert.Equal(new Vector2(-2, 2), line.ClosestPoint(point));
        Assert.Equal(origin, ray.ClosestPoint(point));
        Assert.Equal(line.ClosestPoint(point), ClosestPoint2D.OnLine(point, origin, direction));
        Assert.Equal(ray.ClosestPoint(point), ClosestPoint2D.OnRay(point, origin, direction));
        Assert.Equal(4, line.DistanceTo(point));
        Assert.Equal(5, ray.DistanceTo(point));
        Assert.Equal(4, Distance2D.Distance(line, point));
        Assert.Equal(5, Distance2D.Distance(ray, point));
        Assert.Equal(4, ray.DistanceTo(new(4, 6)));
    }

    [Fact]
    public void GeometryContractIncludesShapesLinesAndRaysWithExplicitPointTolerance()
    {
        IGeometry2D[] geometries = [new Circle2D(2), new Line2D(default, Vector2.UnitX), new Ray2D(default, Vector2.UnitX)];
        Assert.All(geometries, geometry => Assert.True(geometry.ContainsPoint(Vector2.UnitX)));
        Assert.All(geometries, geometry => Assert.False(geometry.ContainsPoint(new(3, 1))));
        var line = new Line2D(default, Vector2.UnitX);
        var ray = new Ray2D(default, Vector2.UnitX);
        Assert.True(line.ContainsPoint(new(100, .000001f)));
        Assert.False(line.ContainsPoint(new(100, .000001f), tolerance: 0));
        Assert.True(ray.ContainsPoint(new(-.000001f, 0)));
        Assert.False(ray.ContainsPoint(new(-.000001f, 0), tolerance: 0));
        Assert.True(ray.ContainsPoint(default, tolerance: 0));
        Assert.False(ray.ContainsPoint(new(-1, 1), tolerance: 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => line.ContainsPoint(default, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => ray.ContainsPoint(default, float.NaN));
    }

    [Fact]
    public void LineIntersectionsHandleCrossingParallelAndCoincidentLines()
    {
        var horizontal = new Line2D(default, Vector2.UnitX);
        Assert.True(horizontal.Intersects(new Line2D(new(-5, 10), Vector2.UnitY)));
        Assert.False(horizontal.Intersects(new Line2D(Vector2.UnitY, Vector2.UnitX)));
        Assert.True(horizontal.Intersects(new Line2D(new(5, 0), -Vector2.UnitX)));
        var away = new Ray2D(new(-5, 1), Vector2.UnitY);
        Assert.False(horizontal.Intersects(away));
        Assert.False(away.Intersects(horizontal));
        Assert.True(horizontal.Intersects(new Ray2D(new(-5, 1), -Vector2.UnitY)));
        Assert.True(horizontal.Intersects(new Ray2D(new(-5, 0), -Vector2.UnitX)));
    }

    [Theory]
    [InlineData(5, -1, 0, 1, true)]
    [InlineData(5, 1, 0, 1, false)]
    [InlineData(-5, -1, 0, 1, false)]
    [InlineData(0, 1, 0, -1, true)]
    [InlineData(0, 0, 0, 1, true)]
    [InlineData(5, 0, 1, 0, true)]
    [InlineData(-5, 0, 1, 0, true)]
    [InlineData(5, 0, -1, 0, true)]
    [InlineData(-5, 0, -1, 0, false)]
    [InlineData(0, 0, -1, 0, true)]
    [InlineData(0, 1, 1, 0, false)]
    public void RayIntersectionsRespectBothForwardDirections(float x, float y, float dx, float dy, bool expected)
    {
        var first = new Ray2D(default, Vector2.UnitX);
        var second = new Ray2D(new(x, y), new(dx, dy));
        Assert.Equal(expected, first.Intersects(second));
        Assert.Equal(expected, second.Intersects(first));
    }

    [Fact]
    public void NearlyParallelRaysStillFindDistantCrossings()
    {
        var horizontal = new Ray2D(default, Vector2.UnitX);
        Assert.True(horizontal.Intersects(new Ray2D(Vector2.UnitY, new(1, -1e-8f))));
        Assert.False(horizontal.Intersects(new Ray2D(Vector2.UnitY, new(1, 1e-8f))));
        // Opposite extreme origins must not overflow their difference before intersection math.
        Assert.True(new Ray2D(new(-float.MaxValue, 0), Vector2.UnitX)
            .Intersects(new Ray2D(new(float.MaxValue, -1), Vector2.UnitY)));
    }

    [Fact]
    public void InvalidAndUninitializedGeometryIsRejectedIncludingByExistingRaycasts()
    {
        Assert.False(default(Line2D).IsValid);
        Assert.False(default(Ray2D).IsValid);
        Assert.Throws<ArgumentOutOfRangeException>(() => new Line2D(default, default));
        Assert.Throws<ArgumentOutOfRangeException>(() => Ray2D.FromPoints(Vector2.One, Vector2.One));
        Assert.Throws<ArgumentOutOfRangeException>(() => Line2D.FromPoints(Vector2.One, Vector2.One));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Ray2D(new(float.NaN, 0), Vector2.UnitX));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Line2D(default, new(float.PositiveInfinity, 1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => default(Ray2D).GetPoint(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => default(Line2D).ClosestPoint(default));
        Assert.Throws<ArgumentOutOfRangeException>(() => default(Ray2D).ContainsPoint(default));
        Assert.Throws<ArgumentOutOfRangeException>(() => default(Line2D).Intersects(new Line2D(default, Vector2.UnitX)));
        Assert.Throws<ArgumentOutOfRangeException>(() => RayIntersection2D.IntersectsBounds(default, Rect2D.Unbounded, 10));
        Assert.Throws<ArgumentOutOfRangeException>(() => Array.Empty<SpatialObject2D>().Raycast(default, 10, out _));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Line2D(default, Vector2.UnitX).GetPoint(float.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Ray2D(default, Vector2.UnitX).DistanceTo(new(float.NaN, 0)));
    }

    [Fact]
    public void AxisAlignedFactoriesProduceHorizontalAndVerticalLines()
    {
        var horizontal = Line2D.Horizontal(3);
        var vertical = Line2D.Vertical(-2);
        Assert.Equal(Vector2.UnitX, horizontal.Direction);
        Assert.Equal(Vector2.UnitY, vertical.Direction);
        Assert.True(horizontal.ContainsPoint(new(1000, 3), tolerance: 0));
        Assert.False(horizontal.ContainsPoint(new(1000, 3.5f)));
        Assert.True(vertical.ContainsPoint(new(-2, -1000), tolerance: 0));
        Assert.Equal(4, horizontal.DistanceTo(new(7, 7)));
        Assert.Equal(9, vertical.DistanceTo(new(7, 7)));
        Assert.Equal(new Vector2(7, 3), horizontal.ClosestPoint(new(7, 7)));
        Assert.Equal(new Vector2(-2, 7), vertical.ClosestPoint(new(7, 7)));
        Assert.True(horizontal.Intersects(vertical));
        Assert.False(horizontal.Intersects(Line2D.Horizontal(4)));
        Assert.Equal(1, horizontal.WhichSide(new(0, 2)));
        Assert.Equal(-1, horizontal.WhichSide(new(0, 4)));
    }
}
