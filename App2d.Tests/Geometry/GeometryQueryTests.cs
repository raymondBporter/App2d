using App2d.Core.Geometry;
using App2d.Core.Geometry.Functions;
using App2d.Core.Mathematics;
using System.Numerics;

namespace App2d.Tests.Geometry;

public sealed class GeometryQueryTests
{
    [Theory]
    [InlineData(1f)]
    [InlineData(2f)]
    [InlineData(-2f)]
    [InlineData(0f)]
    public void RoundPrimitiveProjectionsScaleWithTheAxis(float scale)
    {
        var axis = new Vector2(3, 4) * scale;
        var circle = Projection2D.Circle(new(-1, 2), 2, axis);
        Assert.Equal(Math.Min(-5 * scale, 15 * scale), circle.Min, 5);
        Assert.Equal(Math.Max(-5 * scale, 15 * scale), circle.Max, 5);
        var capsule = Projection2D.Capsule(default, new(4, 0), 2, axis);
        Assert.Equal(Math.Min(-10 * scale, 22 * scale), capsule.Min, 5);
        Assert.Equal(Math.Max(-10 * scale, 22 * scale), capsule.Max, 5);
        Assert.Equal(capsule, Interval1D.ProjectCapsule(default, new(4, 0), 2, axis));
    }

    [Fact]
    public void PolygonProjectionSupportsSpansListsAndTranslations()
    {
        Vector2[] points = [new(-1, -1), new(1, -1), new(1, 1), new(-1, 1)];
        var axis = new Vector2(2, -3);
        var offset = new Vector2(4, 2);
        Assert.Equal(new Interval1D(-3, 7), Projection2D.Polygon(points.AsSpan(), axis, offset));
        Assert.Equal(new Interval1D(-3, 7), Projection2D.Polygon((IReadOnlyList<Vector2>)points, axis, offset));
        Assert.Equal(new Interval1D(0, 0), Projection2D.Polygon(points.AsSpan(), default, offset));
        Assert.Throws<ArgumentOutOfRangeException>(() => Projection2D.Polygon([], axis));
    }

    [Fact]
    public void ConvexOverlapChecksAxesFromBothShapes()
    {
        Vector2[] box = [new(-1, -1), new(1, -1), new(1, 1), new(-1, 1)];
        Vector2[] diamond = [new(0, -1.5f), new(1.5f, 0), new(0, 1.5f), new(-1.5f, 0)];
        Assert.False(PolygonGeometry2D.OverlapsConvex(box, diamond, default, new(2, 2)));
        Assert.False(PolygonGeometry2D.OverlapsConvex(diamond, box, new(2, 2), default));
        Assert.True(PolygonGeometry2D.OverlapsConvex(box, diamond, default, new(1.75f, 1.75f)));
        Array.Reverse(diamond);
        Assert.True(PolygonGeometry2D.OverlapsConvex(box, diamond, new(10, 20), new(11.75f, 21.75f)));
    }

    [Fact]
    public void ConvexOverlapIgnoresRepeatedVerticesAndIncludesTouching()
    {
        Vector2[] box = [new(-1, -1), new(1, -1), new(1, -1), new(1, 1), new(-1, 1)];
        Assert.True(PolygonGeometry2D.OverlapsConvex(box, box, default, new(2, 0)));
        Assert.False(PolygonGeometry2D.OverlapsConvex(box, box, default, new(2.01f, 0)));
    }

    [Fact]
    public void PrimitiveQueriesIncludeBoundariesAndHandleZeroDirections()
    {
        Assert.True(PrimitiveGeometry2D.CircleContainsPoint(new(5, 2), new(2, 2), 3));
        Assert.False(PrimitiveGeometry2D.CircleContainsPoint(new(5.1f, 2), new(2, 2), 3));
        Assert.True(PrimitiveGeometry2D.CapsuleContainsPoint(new(4, 1), default, new(4, 0), 1));
        Assert.False(PrimitiveGeometry2D.CapsuleContainsPoint(new(5.1f, 0), default, new(4, 0), 1));
        Assert.True(PrimitiveGeometry2D.RectangleContainsPoint(new(2, 3), new(-1), new(2, 3)));
        Assert.False(PrimitiveGeometry2D.RectangleContainsPoint(new(2, 3.1f), new(-1), new(2, 3)));
        Assert.Equal(new Vector2(3, 4), PrimitiveGeometry2D.CircleSupportPoint(default, new(3, 4), 2));
        Assert.Equal(new Vector2(4, 0), PrimitiveGeometry2D.CapsuleSupportPoint(default, default, new(4, 0), 2));
        Assert.Equal(new Vector2(6, 0), PrimitiveGeometry2D.CapsuleSupportPoint(Vector2.UnitX, default, new(4, 0), 2));
        Assert.Equal(new Vector2(-1, 3), PrimitiveGeometry2D.RectangleSupportPoint(new(-2, 0), new(-1), new(2, 3)));
    }

    [Fact]
    public void NormalizedRadiusIsDimensionlessWhileSegmentDistanceUsesWorldUnits()
    {
        var center = new Vector2(10, 20);
        Assert.Equal(1, PrimitiveGeometry2D.NormalizedEllipseRadius(new(14, 20), center, new(4, 2)));
        Assert.Equal(1, PrimitiveGeometry2D.NormalizedEllipseRadius(new(10, 22), center, new(4, 2)));
        Assert.True(PrimitiveGeometry2D.EllipseContainsPoint(new(10, 22), center, new(4, 2)));
        Assert.False(PrimitiveGeometry2D.EllipseContainsPoint(new(14, 22), center, new(4, 2)));
        Assert.Equal(1, PrimitiveGeometry2D.NormalizedRectangleRadius(new(14, 22), center, new(4, 2)));
        Assert.Equal(3, PrimitiveGeometry2D.DistanceToSegment(new(2, 3), default, new(4, 0)));
        Assert.Equal(5, PrimitiveGeometry2D.DistanceToSegment(new(3, 4), default, default));
        Assert.Equal(5, PrimitiveGeometry2D.DistanceToSegment(new(7, 4), default, new(4, 0)));
    }

    [Fact]
    public void XyRotationPreservesDepthAndCanBeInverted()
    {
        var original = new Vector3(3, 4, 17);
        var rotated = Rotation2D.ApplyXY(original, MathF.PI / 2);
        Assert.Equal(-4, rotated.X, 5);
        Assert.Equal(3, rotated.Y, 5);
        Assert.Equal(17, rotated.Z);
        Assert.True(Vector3.Distance(original, Rotation2D.ApplyXY(rotated, -MathF.PI / 2)) < .00001f);
        Assert.Equal(original, Rotation2D.ApplyXY(original, 0));
    }
}
