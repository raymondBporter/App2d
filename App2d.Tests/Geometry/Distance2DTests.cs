using App2d.Core;
using App2d.Core.Geometry;
using App2d.Core.Geometry.Functions;
using App2d.Core.Geometry.Shapes;
using System.Numerics;

namespace App2d.Tests.Geometry;

public sealed class Distance2DTests
{
    [Theory]
    [InlineData(0, -2)]
    [InlineData(2, 0)]
    [InlineData(5, 3)]
    public void PointQueriesDistinguishInteriorBoundaryAndExterior(float x, float expected)
    {
        IShape2D[] shapes =
        [
            new Circle2D(2), new Capsule2D(new(0, -1), new(0, 1), 2),
            Rectangle2D.FromSize(new(4, 6)),
            new ConvexPolygon2D([new(-2, -3), new(2, -3), new(2, 3), new(-2, 3)]),
            new HalfSpace2D(Vector2.UnitX, 2)
        ];
        var point = new Vector2(x, 0);
        foreach (var shape in shapes)
        {
            Assert.Equal(expected, Distance2D.SignedDistance(point, shape), 5);
            Assert.Equal(expected, Distance2D.SignedDistance(shape, point), 5);
            Assert.Equal(Math.Max(0, expected), Distance2D.Distance(point, shape), 5);
        }
    }

    [Fact]
    public void RectangleCornerDistanceIsEuclideanAndZeroAreaRectsWork()
    {
        var rectangle = new Rect2D(new(-1), new(1));
        Assert.Equal(5f, Distance2D.SignedDistance(new Vector2(4, 5), rectangle));
        Assert.Equal(5f, Distance2D.SignedDistance(rectangle, new Rect2D(new(4, 5), new(6, 7))));
        Assert.Equal(-1f, Distance2D.SignedDistance(Vector2.Zero, rectangle));
        Assert.Equal(0f, Distance2D.Distance(Vector2.Zero, rectangle));
        Assert.Equal(5f, Distance2D.Distance(new Vector2(3, 4), default(Rect2D)));
        Assert.Equal(0f, Distance2D.SignedDistance(Vector2.Zero, default(Rect2D)));
        Assert.Equal(-1f, Distance2D.SignedDistance(default(Rect2D), rectangle));
        Assert.Equal(5f, Distance2D.Distance(default(Rect2D), new Rect2D(new(3, 4), new(3, 4))));
    }

    [Fact]
    public void AnyRectangleContractGetsDistanceQueries()
    {
        var region = new Region(new(-1), new(1));
        var other = new Bounds2D(new(4, 5), new(5, 6));
        Assert.Equal(-1f, region.SignedDistanceTo(Vector2.Zero));
        Assert.Equal(0f, region.DistanceTo(Vector2.Zero));
        Assert.Equal(5f, region.DistanceTo(other));
        Assert.Equal(5f, region.SignedDistanceTo(other));
    }

    [Fact]
    public void IntervalContainmentNeedsEscapeDepthNotJustIntersectionLength()
    {
        var inner = new Interval1D(-1, 1);
        var outer = new Interval1D(-5, 5);
        Assert.Equal(-6f, Distance2D.SignedDistance(inner, outer, out var direction));
        Assert.Equal(-1f, direction);
        Assert.Equal(-6f, Distance2D.SignedDistance(outer, inner));
        Assert.Equal(0f, Distance2D.SignedDistance(inner, new Interval1D(1, 3)));
        Assert.Equal(2f, Distance2D.Distance(inner, new Interval1D(3, 5)));
    }

    [Fact]
    public void PolygonPointQueryReturnsBoundaryFeatureAndSupportsEitherWinding()
    {
        Vector2[] vertices = [new(-1), new(1, -1), new(1), new(-1, 1)];
        var signed = Distance2D.SignedDistanceToConvexPolygon(new(4, 5), vertices, out var closest, out var edge);
        Assert.Equal(5f, signed);
        Assert.Equal(new Vector2(1, 1), closest);
        Assert.Equal(1, edge);
        Array.Reverse(vertices);
        Assert.Equal(-1f, Distance2D.SignedDistanceToConvexPolygon(Vector2.Zero, vertices));
        Assert.Equal(5f, Distance2D.SignedDistanceToConvexPolygon(new(4, 5), vertices));
        Assert.True(Distance2D.SignedDistanceToConvexPolygon(new(1.000001f, 0), vertices) > 0f);
        Assert.Throws<ArgumentOutOfRangeException>(() => Distance2D.SignedDistanceToConvexPolygon(default, []));
    }

    [Theory]
    [InlineData(0, -3)]
    [InlineData(2, -1)]
    [InlineData(3, 0)]
    [InlineData(6, 3)]
    public void CirclePairsIncludeCoincidentCentersAndTouching(float x, float expected)
    {
        AssertPair(new Circle2D(1), new Circle2D(2, new(x, 0)), expected);
        Assert.Equal(expected, Distance2D.SignedDistanceBetweenCircles(default, 1, new(x, 0), 2));
    }

    [Fact]
    public void PolygonPairsHandleCornerGapsContainmentAndContact()
    {
        var box = Rectangle2D.FromSize(new(2, 2));
        var diagonal = new ConvexPolygon2D([new(4, 5), new(6, 5), new(6, 7), new(4, 7)]);
        AssertPair(box, diagonal, 5);
        AssertPair(box, Rectangle2D.FromSize(new(10, 10)), -6);
        AssertPair(box, Rectangle2D.FromSize(new(2, 2), new(2, 0)), 0);
        var diamond = new ConvexPolygon2D([new(0, -3), new(3, 0), new(0, 3), new(-3, 0)]);
        AssertPair(box, diamond, -5 / MathF.Sqrt(2));
    }

    [Fact]
    public void CirclePolygonPairsHandleInsideAndOutsideCenters()
    {
        var box = Rectangle2D.FromSize(new(2, 2));
        AssertPair(new Circle2D(.5f), box, -1.5f);
        AssertPair(new Circle2D(2, new(4, 5)), box, 3);
        AssertPair(new Circle2D(1, new(2, 0)), box, 0);
        AssertPair(new Circle2D(2, new(2, 0)), box, -1);
    }

    [Fact]
    public void CrossingCapsulesNeedMoreThanTheirCombinedRadiiToEscape()
    {
        var horizontal = new Capsule2D(new(-2, 0), new(2, 0), 1);
        var vertical = new Capsule2D(new(0, -2), new(0, 2), 1);
        AssertPair(horizontal, vertical, -4);
        Assert.Equal(-4f, Distance2D.SignedDistanceBetweenCapsules(horizontal.Start, horizontal.End, 1, vertical.Start, vertical.End, 1));
        AssertPair(horizontal, new Capsule2D(new(-2, 1.5f), new(2, 1.5f), 1), -.5f);
        AssertPair(horizontal, new Capsule2D(new(5, 0), new(7, 0), 1), 1);
        AssertPair(new Capsule2D(default, default, 1), new Capsule2D(new(5, 0), new(5, 0), 1), 3);
        AssertPair(new Circle2D(1), horizontal, -2);
    }

    [Fact]
    public void CapsulePolygonPairsUseBothEndCapsAndInteriorPenetration()
    {
        var box = Rectangle2D.FromSize(new(2, 2));
        AssertPair(new Capsule2D(new(-2, 0), new(2, 0), 1), box, -2);
        AssertPair(new Capsule2D(new(4, 5), new(4, 7), 1), box, 4);
    }

    [Fact]
    public void RawConvexQueriesSupportRepeatedVerticesPointsAndSegments()
    {
        Vector2[] box = [new(-1), new(1, -1), new(1, -1), new(1), new(-1, 1)];
        Assert.Equal(-1f, Distance2D.SignedDistanceBetweenConvexPolygons(box, [Vector2.Zero]));
        Assert.Equal(5f, Distance2D.SignedDistanceBetweenConvexPolygons(box, [new(4, 5)]));
        Assert.Equal(3f, Distance2D.SignedDistanceBetweenConvexPolygons([new(0, 0), new(2, 0)], [new(5, 0)]));
        Assert.Equal(3f, Distance2D.SignedDistanceBetweenConvexPolygons([new(0, 0), new(2, 0)], [new(5, 0), new(6, 0)]));
        Assert.Equal(0f, Distance2D.DistanceBetweenConvexPolygons([new(-2, 0), new(2, 0)], [new(0, -2), new(0, 2)]));
        Assert.Throws<ArgumentOutOfRangeException>(() => Distance2D.SignedDistanceBetweenConvexPolygons([], box));
    }

    [Fact]
    public void HalfSpaceQueriesMeasureTheDeepestPoint()
    {
        var floor = new HalfSpace2D(new(0, 2), 4); // boundary y = 2
        AssertPair(new Circle2D(1, new(0, 5)), floor, 2);
        AssertPair(new Circle2D(1), floor, -3);
        AssertPair(new Capsule2D(new(0, 3), new(0, 5), 1), floor, 0);
        AssertPair(Rectangle2D.FromSize(new(2, 4)), floor, -4);
    }

    [Fact]
    public void SpatialQueriesRespectRotationReflectionScaleAndWorldUnits()
    {
        var box = new SpatialObject2D(Rectangle2D.FromSize(new(2, 4)));
        box.Transform.Scale = new(-2, 2);
        box.Transform.Rotation = MathF.PI / 2;
        box.Transform.Position = new(10, 20); // world extents x=6..14, y=18..22
        var circle = new SpatialObject2D(new Circle2D(1));
        circle.Transform.Scale = new(2);
        circle.Transform.Position = new(19, 22);
        Assert.Equal(3f, Distance2D.SignedDistance(box, circle), 4);
        Assert.Equal(3f, Distance2D.SignedDistance(circle, box), 4);
        Assert.Equal(3f, Distance2D.Distance(box, circle), 4);
        Assert.Equal(-2f, Distance2D.SignedDistance(new Vector2(10, 20), box), 4);
        Assert.Equal(5f, Distance2D.SignedDistance(new Vector2(17, 26), box), 4);
        Assert.Equal(3f, Distance2D.SignedDistance(box.Shape, box.CollisionPose, circle.Shape, circle.CollisionPose), 4);
        circle.Transform.Position = new(15, 22);
        Assert.Equal(-1f, Distance2D.SignedDistance(box, circle), 4); // pose cache refreshes
        box.Transform.Scale = new(2, 1);
        Assert.Throws<InvalidOperationException>(() => Distance2D.Distance(box, circle));
    }

    [Fact]
    public void TransformedHalfSpaceUsesItsWorldBoundary()
    {
        var wall = new SpatialObject2D(new HalfSpace2D(Vector2.UnitX, 1));
        wall.Transform.Scale = new(2);
        wall.Transform.Rotation = MathF.PI / 2;
        wall.Transform.Position = new(3, 5); // boundary y = 7
        var circle = new SpatialObject2D(new Circle2D(1));
        circle.Transform.Position = new(3, 10);
        Assert.Equal(2f, Distance2D.SignedDistance(circle, wall), 4);
        Assert.Equal(2f, Distance2D.SignedDistance(wall, circle), 4);
    }

    [Fact]
    public void CompositeUnsignedDistancePreservesGapsButSignedQueriesFailExplicitly()
    {
        var union = new CompositeShape2D([new Circle2D(1, new(-3, 0)), new Circle2D(1, new(3, 0))]);
        Assert.Equal(2f, Distance2D.Distance(Vector2.Zero, union));
        Assert.Equal(0f, Distance2D.Distance(new Vector2(3, 0), union));
        Assert.Equal(1f, Distance2D.Distance(union, new Circle2D(1)));
        Assert.Equal(1f, Distance2D.Distance(new Circle2D(1), union));
        Assert.Equal(0f, Distance2D.Distance(union, union));
        Assert.Throws<NotSupportedException>(() => Distance2D.SignedDistance(Vector2.Zero, union));
        Assert.Throws<NotSupportedException>(() => Distance2D.SignedDistance(union, new Circle2D(1)));
        Assert.Throws<NotSupportedException>(() => Distance2D.SignedDistance(new HalfSpace2D(Vector2.UnitY, 0), new HalfSpace2D(-Vector2.UnitY, 0)));
    }

    [Fact]
    public void RawSegmentQueriesIncludeDegenerateSegmentsAndIntersections()
    {
        Assert.Equal(5f, Distance2D.DistanceToSegment(new(3, 4), default, default));
        Assert.Equal(0f, Distance2D.DistanceBetweenSegments(new(-2, 0), new(2, 0), new(0, -2), new(0, 2)));
        Assert.Equal(5f, Distance2D.DistanceBetweenSegments(default, default, new(3, 4), new(3, 4)));
        Assert.Equal(3f, Distance2D.DistanceToCapsule(new(4, 5), default, new(4, 0), 2));
    }

    [Theory]
    [InlineData(.1f, .1f)]
    [InlineData(-.1f, 0f)]
    public void AlmostParallelSegmentsStillFindTheClosestEndOrIntersection(float endY, float expected)
    {
        var firstStart = Vector2.Zero;
        var firstEnd = new Vector2(1000, 0);
        var secondStart = new Vector2(0, .2f);
        var secondEnd = new Vector2(1000, endY);
        Assert.Equal(expected, Distance2D.DistanceBetweenSegments(firstStart, firstEnd, secondStart, secondEnd), 5);
        Assert.Equal(expected, Distance2D.DistanceBetweenSegments(secondStart, secondEnd, firstStart, firstEnd), 5);
        var closest = ClosestPoint2D.BetweenSegments(firstStart, firstEnd, secondStart, secondEnd);
        Assert.InRange(closest.FirstParameter, 0f, 1f);
        Assert.InRange(closest.SecondParameter, 0f, 1f);
        Assert.True(Vector2.Distance(Vector2.Lerp(firstStart, firstEnd, closest.FirstParameter), closest.First) < .0001f);
        Assert.True(Vector2.Distance(Vector2.Lerp(secondStart, secondEnd, closest.SecondParameter), closest.Second) < .0001f);
    }

    private static void AssertPair(IShape2D first, IShape2D second, float expected)
    {
        Assert.Equal(expected, Distance2D.SignedDistance(first, second), 4);
        Assert.Equal(expected, Distance2D.SignedDistance(second, first), 4);
        Assert.Equal(Math.Max(0f, expected), Distance2D.Distance(first, second), 4);
    }

    private readonly record struct Region(Vector2 Min, Vector2 Max) : IRect2D;
}
