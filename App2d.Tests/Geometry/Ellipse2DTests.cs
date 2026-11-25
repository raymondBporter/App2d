using App2d.Core.Geometry;
using App2d.Core.Geometry.Bounds;
using App2d.Core.Geometry.Convex;
using App2d.Core.Geometry.Distance;
using App2d.Core.Geometry.Intersection.Raycast;
using App2d.Core.Geometry.Primitives;
using App2d.Core.Geometry.Serialization;
using App2d.Core.Geometry.Shapes;
using App2d.Core.Mathematics.Transforms;
using static App2d.Tests.Geometry.VectorComparers;

namespace App2d.Tests.Geometry;

public sealed class Ellipse2DTests
{
    // Radii (3, 1) at (2, 1), rotated 30 degrees.
    private static readonly Ellipse2D Tilted = Ellipse2D.Builder.Create(new(3, 1), new(2, 1), MathF.PI / 6);

    [Fact]
    public void ContainsSupportDistanceAndBoundsAgreeWithTheOutline()
    {
        var outline = new Vector2f[512];
        Tilted.WriteVertices(outline);
        var bounds = ShapeBounds2D.Calculate(Tilted);
        Assert.Equal(Rect2f.FromPoints(outline).Min, bounds.Min, Loose);
        Assert.Equal(Rect2f.FromPoints(outline).Max, bounds.Max, Loose);
        foreach (var v in outline) Assert.InRange(ShapeDistance2D.SignedDistance(v, Tilted), -1e-3f, 1e-3f);
        var tip = Tilted.Frame.FromFrame(new(3, 0));
        Assert.Equal(tip, Tilted.GetSupportPoint(Tilted.Axis), Near);
        Assert.True(Tilted.ContainsPoint(Tilted.Frame.FromFrame(new(2.9f, 0))));
        Assert.False(Tilted.ContainsPoint(new(4.9f, 1)));
    }

    [Fact]
    public void TransformedBoundsComposeTheFrame()
    {
        var rotation = Similarity2D.FromAxis(Vector2f.Zero, OrientedFrame2D.AxisFromAngle(-MathF.PI / 6));
        var turnBack = rotation.Translated(Tilted.Center - rotation.TransformPoint(Tilted.Center));
        var bounds = ShapeBounds2D.CalculateTransformed(Tilted, turnBack);
        Assert.Equal(new Vector2f(-1, 0), bounds.Min, Loose);
        Assert.Equal(new Vector2f(5, 2), bounds.Max, Loose);
    }

    [Fact]
    public void RaycastHitsTheRotatedPerimeter()
    {
        var origin = Tilted.Frame.FromFrame(new(10, 0));
        Assert.True(ShapeRaycast2D.TryIntersect(origin, -Tilted.Axis, Tilted, 100, out var hit));
        Assert.Equal(Tilted.Frame.FromFrame(new(3, 0)), hit.Point, Loose);
        Assert.Equal(Tilted.Axis, hit.Normal, Loose);
        Assert.Equal(7f, hit.Distance, 3);
    }

    [Fact]
    public void CircleQueryAgainstARotatedEllipseMatchesTheUnrotatedOne()
    {
        var flat = Ellipse2D.Builder.Create(new(3, 1), new(2, 1));
        var pose = Similarity2D.FromAxis(Vector2f.Zero, OrientedFrame2D.AxisFromAngle(MathF.PI / 6));
        var probe = Circle2D.Builder.Create(.5f, new(4.5f, 1.2f));
        var viaFlatPosed = ShapeConvexQuery2D.Query(probe, Similarity2D.Identity, flat, pose);
        var viaTilted = ShapeConvexQuery2D.Query(probe, Similarity2D.Identity, Ellipse2D.Builder.Create(new(3, 1), pose.TransformPoint(new(2, 1)), MathF.PI / 6), Similarity2D.Identity);
        Assert.Equal(viaFlatPosed.SignedDistance, viaTilted.SignedDistance, 4);
    }

    [Fact]
    public void PairSolversFallBackForRotatedEllipsesAndStayExactForAligned()
    {
        var a = Ellipse2D.Builder.Create(new(2, 1), new(-1, 0));
        var b = Ellipse2D.Builder.Create(new(2, 1), new(2, 0));
        var composite = CompositeShape2D.Builder.Create([a, b], includeOverlap: false, areaOutlineSegments: 256);
        var turned = Similarity2D.FromAxis(Vector2f.Zero, OrientedFrame2D.AxisFromAngle(.7f));
        var ar = Ellipse2D.Builder.Create(new(2, 1), turned.TransformPoint(new(-1, 0)), .7f);
        var br = Ellipse2D.Builder.Create(new(2, 1), turned.TransformPoint(new(2, 0)), .7f);
        var rotated = CompositeShape2D.Builder.Create([ar, br], includeOverlap: false, areaOutlineSegments: 256);
        Assert.Equal(composite.Area, rotated.Area, 2);
        Assert.Equal(composite.Diameter, rotated.Diameter, 2);
    }

    [Fact]
    public void EllipseJsonRoundTripsAndReadsWithoutAnAngle()
    {
        Assert.Equal(MathF.PI / 6, Assert.IsType<Ellipse2D>(GeometryJson2D.ShapeFromJson(Tilted.ToJson<IShape2D>()).Validated()).AngleRadians, 5);
        Assert.True(Assert.IsType<Ellipse2D>(GeometryJson2D.ShapeFromJson("{\"kind\":\"ellipse\",\"center\":{\"x\":0,\"y\":0},\"radii\":{\"x\":2,\"y\":1}}").Validated()).IsAxisAligned);
    }
}
