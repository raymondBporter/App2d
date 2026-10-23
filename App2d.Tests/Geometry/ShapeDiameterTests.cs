using App2d.Core.Geometry.Operations.Bounds;
using App2d.Core.Geometry.Primitives;
using App2d.Core.Geometry.Shapes;
using App2d.Core.Shapes;
using System.Numerics;

namespace App2d.Tests.Geometry;

public sealed class ShapeDiameterTests
{
    [Fact]
    public void PrimitiveDiametersMeasureTheFarthestPair()
    {
        Assert.Equal(6f, Circle2D.Builder.Create(3, new(10, -2)).Diameter);
        Assert.Equal(8f, Ellipse2D.Builder.Create(new(2, 4)).Diameter);
        Assert.Equal(8f, Ellipse2D.Builder.Create(new(4, 2)).Diameter);
        Assert.Equal(7f, Capsule2D.Builder.Create(new(1, 2), new(4, 6), 1).Diameter);
        Assert.Equal(2f, Capsule2D.Builder.Create(new(1, 2), new(1, 2), 1).Diameter);
        Assert.Equal(5f, Rectangle2D.Builder.Create(3, 4).Diameter);
        Assert.Equal(5f, AxisAlignedRectangle2D.Builder.Create(3, 4).Diameter);
        Assert.Equal(5f, Triangle2D.Builder.Create(new(0, 0), new(3, 0), new(0, 4)).Diameter);
        Assert.Equal(float.PositiveInfinity, HalfSpace2D.Builder.Create(Vector2.UnitY, 2).Diameter);
    }

    [Fact]
    public void RoundedRectangleUsesTheInnerDiagonalAndTwoRadii()
    {
        var shape = RoundedRectangle2D.Builder.Create(6, 4, 1, new(3, 2));
        Assert.Equal(new Rect2D(new(0, 0), new(6, 4)), ShapeBounds2D.Calculate(shape));
        Assert.Equal(new Vector2(2, 1), shape.InnerHalfSize);
        Assert.Equal(MathF.Sqrt(20) + 2, shape.Diameter, 5);
        Assert.Equal(shape.Diameter / 2, ShapeBounds2D.CalculateBoundingCircle(shape).Radius);
        Assert.Equal(5f, RoundedRectangle2D.Builder.Create(3, 4, 0).Diameter);
        Assert.Equal(6f, RoundedRectangle2D.Builder.Create(6, 6, 3).Diameter);
        Assert.Equal(6f, RoundedRectangle2D.Builder.Create(6, 2, 1).Diameter);
    }

    [Fact]
    public void PolygonDiameterIgnoresWindingAndConcaveIndentations()
    {
        Vector2[] points = [new(0, 0), new(5, 0), new(5, 1), new(1, 1), new(1, 4), new(0, 4)];
        var polygon = SimplePolygon2D.Builder.Create(points);
        Assert.Equal(MathF.Sqrt(41), polygon.Diameter, 5);
        Assert.Equal(polygon.Diameter, SimplePolygon2D.Builder.Create(points.Reverse()).Diameter);
        Assert.Equal(polygon.Diameter, ConvexPolygon2D.Builder.Create([new(0, 0), new(5, 0), new(0, 4)]).Diameter);
    }

    [Fact]
    public void CompositeDiameterIncludesPairsFromDifferentParts()
    {
        var circles = CompositeShape2D.Builder.Create([Circle2D.Builder.Create(1, new(-3, 0)), Circle2D.Builder.Create(1, new(3, 0))]);
        Assert.Equal(8f, circles.Diameter);
        var mixed = CompositeShape2D.Builder.Create([Rectangle2D.Builder.Create(2, 2), Circle2D.Builder.Create(1, new(5, 0))]);
        Assert.Equal(MathF.Sqrt(37) + 1, mixed.Diameter, 5);
    }

    [Fact]
    public void EllipticalCompositeUsesTheCurvesRatherThanADrawingApproximation()
    {
        var a = Ellipse2D.Builder.Create(new(3, 1));
        var b = Ellipse2D.Builder.Create(new(1, 2), new(4, 3));
        var union = CompositeShape2D.Builder.Create([a, b]);
        var sampled = 0f;
        for (var i = 0; i < 16384; i++)
        {
            var angle = i * MathF.Tau / 16384;
            var direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            sampled = MathF.Max(sampled, Vector2.Distance(a.GetSupportPoint(direction), b.GetSupportPoint(-direction)));
        }
        Assert.InRange(MathF.Abs(sampled - union.Diameter), 0, .00002f);
        Assert.True(union.Diameter > MathF.Max(a.Diameter, b.Diameter));
    }

    [Fact]
    public void CustomConvexSupportsAlsoWorkInCompositeDiameters()
    {
        IConvexShape2D custom = new CustomDisk();
        Assert.Equal(2.5f, custom.Diameter, 5);
        var composite = CompositeShape2D.Builder.Create([custom, Circle2D.Builder.Create(.5f, new(4, 3))]);
        Assert.Equal(6.75f, composite.Diameter, 4);
    }

    [Fact]
    public void APolygonsEnclosingCircleCanRequireMoreThanHalfItsDiameter()
    {
        var triangle = Triangle2D.Builder.Create(new(0, 0), new(2, 0), new(1, MathF.Sqrt(3)));
        var (center, radius) = ShapeBounds2D.CalculateBoundingCircle(triangle);
        Assert.Equal(2f, triangle.Diameter, 5);
        Assert.True(radius > triangle.Diameter / 2);
        foreach (var point in new[] { triangle.A, triangle.B, triangle.C })
        {
            Assert.InRange(Vector2.Distance(point, center), 0, radius + .00001f);
        }
    }

    [Fact]
    public void LargeFiniteCoordinatesDoNotOverflowIntermediateSquares()
    {
        var shape = Rectangle2D.Builder.Create(3e20f, 4e20f);
        Assert.InRange(shape.Diameter / 5e20f, .999999f, 1.000001f);
        Assert.True(float.IsFinite(ShapeBounds2D.CalculateBoundingCircle(shape).Radius));
        var circle = Circle2D.Builder.Create(float.MaxValue * .75f);
        Assert.True(float.IsPositiveInfinity(circle.Diameter));
        Assert.Equal(circle.Radius, ShapeBounds2D.CalculateBoundingCircle(circle).Radius);
    }

    private sealed class CustomDisk : IConvexShape2D
    {
        public string Kind => "custom-disk";
        public float Area => MathF.PI * 1.25f * 1.25f;
        public bool ContainsPoint(Vector2 point) => point.Length() <= 1.25f;
        public Vector2 GetSupportPoint(Vector2 direction) => Vector2.Normalize(direction) * 1.25f;
    }
}
