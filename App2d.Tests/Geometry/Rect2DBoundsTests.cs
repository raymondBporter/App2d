using App2d.Core.Geometry.Operations.Bounds;
using App2d.Core.Geometry.Operations.Containment;
using App2d.Core.Geometry.Operations.Convex;
using App2d.Core.Geometry.Operations.Vertices;
using App2d.Core.Geometry.Primitives;
using App2d.Core.Geometry.Shapes;
using App2d.Core.Shapes;
using App2d.Core.Spatial;
using System.Numerics;

namespace App2d.Tests.Geometry;

public sealed class Rect2DBoundsTests
{
    [Fact]
    public void RawPrimitiveBoundsIncludeOffsetsAndReversedCapsuleEndpoints()
    {
        Assert.Equal(new Rect2D(new(-2, -7), new(6, 1)), Rect2D.FromCircle(new(2, -3), 4));
        var capsule = Rect2D.FromCapsule(new(5, -2), new(-3, 4), 2);
        Assert.Equal(new Rect2D(new(-5, -4), new(7, 6)), capsule);
        Assert.Equal(capsule, Rect2D.FromCapsule(new(-3, 4), new(5, -2), 2));
        Assert.Equal(Rect2D.FromCircle(new(1, 2), 3), Rect2D.FromCapsule(new(1, 2), new(1, 2), 3));
        Assert.Equal(new Rect2D(new(1, 2), new(1, 2)), Rect2D.FromCircle(new(1, 2), 0));
        Assert.Equal(new Rect2D(new(-3, -4), new(5, 6)),
            new Rect2D(new(-3, -4), new(5, 6)).ToRect());
    }

    [Fact]
    public void PointBoundsAndUnionHandleDifferentExtremaAndSinglePoints()
    {
        Vector2[] points = [new(3, 4), new(-2, 7), new(5, -1)];
        var bounds = Rect2D.FromPoints(points);
        Assert.Equal(new Rect2D(new(-2, -1), new(5, 7)), bounds);
        Assert.Equal(new Rect2D(new(3, 4), new(3, 4)), Rect2D.FromPoints(points.AsSpan(0, 1)));
        Assert.Equal(new Rect2D(new(-2, -4), new(10, 7)),
            bounds.Union(new Rect2D(new(8, -4), new(10, 3))));
        Assert.Throws<ArgumentOutOfRangeException>(() => Rect2D.FromPoints([]));
    }

    [Fact]
    public void ShapesCalculateBoundsWithoutOwningThem()
    {
        var expected = new Rect2D(new(-2, -3), new(4, 5));
        IShape2D[] shapes =
        [
            Rectangle2D.Builder.Create(expected.Min, expected.Max),
            AxisAlignedRectangle2D.Builder.Create(expected.Min, expected.Max),
            Capsule2D.Builder.Create(new(1, 0), new(1, 2), 3),
            ConvexPolygon2D.Builder.Create([new(-2, -3), new(4, -3), new(1, 5)]),
            CompositeShape2D.Builder.Create([Circle2D.Builder.Create(1, new(-1, -2)), Rectangle2D.Builder.Create(new(1, 2), new(4, 5))]),
        ];
        foreach (var shape in shapes)
        {
            Assert.Equal(expected, ShapeBounds2D.Calculate(shape));
            Assert.Equal(expected, new SpatialObject2D(shape).LocalBounds);
        }
        Assert.Equal(new Rect2D(new(-1, 0), new(5, 6)), ShapeBounds2D.Calculate(Circle2D.Builder.Create(3, new(2, 3))));
    }

    [Fact]
    public void SpatialObjectCalculatesLocalBoundsOnceAndRefreshesWorldBoundsForEveryTransformChange()
    {
        var shape = new CountedConvex();
        var item = new SpatialObject2D(shape);
        var local = new Rect2D(new(-2, -1), new(4, 3));
        Assert.Equal(4, shape.SupportCalls);
        Assert.Equal(local, item.LocalBounds);
        Assert.Equal(local, item.WorldBounds);

        item.Transform.Position = new(10, -5);
        Assert.Equal(new Rect2D(new(8, -6), new(14, -2)), item.WorldBounds);
        item.Transform.Scale = new(-2, 3);
        Assert.Equal(new Rect2D(new(2, -8), new(14, 4)), item.WorldBounds);
        item.Transform.Rotation = MathF.PI / 2;
        Near(new(new(1, -13), new(13, -1)), item.WorldBounds);
        var rotated = item.WorldBounds;
        for (var i = 0; i < 10; i++) Assert.Equal(rotated, item.WorldBounds);
        Assert.Equal(local, item.LocalBounds);
        Assert.Equal(4, shape.SupportCalls);
    }

    [Theory]
    [InlineData(1f, 1f)]
    [InlineData(-2f, 3f)]
    [InlineData(2f, -3f)]
    [InlineData(0f, 2f)]
    [InlineData(0f, 0f)]
    public void NonrotatingTransformsAgreeWithAllFourTransformedCorners(float xScale, float yScale)
    {
        var local = new Rect2D(new(-2, -1), new(4, 3));
        var scale = new Vector2(xScale, yScale);
        var translation = new Vector2(10, -5);
        var matrix = Matrix3x2.CreateScale(scale) * Matrix3x2.CreateTranslation(translation);
        var expected = CornerBounds(local, matrix);
        Near(expected, local.TransformedBy(matrix));
        if (scale == Vector2.One) Near(expected, local.TranslatedBy(translation));
    }

    [Theory]
    [InlineData(.000001f, false)]
    [InlineData(.7f, false)]
    [InlineData(1.5707963f, false)]
    [InlineData(-.4f, true)]
    public void RotatedAndShearedTransformsEncloseTheBox(float angle, bool shear)
    {
        var local = new Rect2D(new(-2, -1), new(4, 3));
        var matrix = Matrix3x2.CreateScale(-2, 3) * Matrix3x2.CreateRotation(angle) * Matrix3x2.CreateTranslation(10, -5);
        if (shear) matrix.M21 += .6f;
        Near(CornerBounds(local, matrix), local.TransformedBy(matrix));
    }

    [Fact]
    public void RotatingACircleKeepsItsWorldBoundsTight()
    {
        var circle = Circle2D.Builder.Create(2, new(1, -3));
        var item = new SpatialObject2D(circle);
        item.Transform.Rotation = MathF.PI / 4;
        item.Transform.Position = new(10, 20);
        var world = item.WorldBounds;
        Assert.InRange(MathF.Abs(world.Size.X - 4f), 0, .00001f);
        Assert.InRange(MathF.Abs(world.Size.Y - 4f), 0, .00001f);
        for (var i = 0; i < 64; i++)
        {
            var point = VertexGenerator2D.PointOnEllipse(circle.Center, new(circle.Radius), i * MathF.Tau / 64);
            Assert.InRange(world.DistanceTo(Vector2.Transform(point, item.Transform.Matrix)), 0, .00001f);
        }
    }

    [Fact]
    public void AxisAlignedHalfSpacesRetainTheirBoundaryThroughTranslationAndScale()
    {
        var item = new SpatialObject2D(HalfSpace2D.Builder.Create(Vector2.UnitY, 0));
        var local = new Rect2D(new(float.NegativeInfinity), new(float.PositiveInfinity, 0));
        Assert.Equal(local, item.LocalBounds);
        Assert.Equal(local, item.WorldBounds);
        item.Transform.Position = new(100, -25);
        Assert.Equal(new Rect2D(new(float.NegativeInfinity), new(float.PositiveInfinity, -25)), item.WorldBounds);
        item.Transform.Scale = new(-2, 3);
        Assert.Equal(new Rect2D(new(float.NegativeInfinity), new(float.PositiveInfinity, -25)), item.WorldBounds);
        item.Transform.Rotation = .7f;
        Assert.Equal(Rect2D.Unbounded, item.WorldBounds);
        // A box transform remains conservative; the shape-aware path above retains the half-space itself.
        Assert.Equal(Rect2D.Unbounded, item.LocalBounds.TransformedBy(Matrix3x2.CreateScale(0f)));
        Assert.Equal(new Rect2D(new(float.NegativeInfinity), new(float.PositiveInfinity, 5)), item.LocalBounds.TranslatedBy(new(5, 5)));
    }

    [Fact]
    public void UnknownNonconvexShapesFailExplicitlyAndNullShapesAreRejected()
    {
        Assert.Throws<NotSupportedException>(() => ShapeBounds2D.Calculate(new UnknownShape()));
        Assert.Throws<ArgumentNullException>(() => ShapeBounds2D.Calculate(null!));
        Assert.Throws<ArgumentNullException>(() => new SpatialObject2D(null!));
    }

    private static Rect2D CornerBounds(Rect2D bounds, Matrix3x2 matrix)
    {
        Vector2[] corners = [bounds.Min, new(bounds.Max.X, bounds.Min.Y), bounds.Max, new(bounds.Min.X, bounds.Max.Y)];
        var transformed = corners.Select(point => Vector2.Transform(point, matrix)).ToArray();
        return new(new(transformed.Min(p => p.X), transformed.Min(p => p.Y)),
            new(transformed.Max(p => p.X), transformed.Max(p => p.Y)));
    }

    private static void Near(Rect2D expected, Rect2D actual)
    {
        Assert.True(Vector2.Distance(expected.Min, actual.Min) < .00001f, $"Min: expected {expected.Min}, got {actual.Min}.");
        Assert.True(Vector2.Distance(expected.Max, actual.Max) < .00001f, $"Max: expected {expected.Max}, got {actual.Max}.");
    }

    private sealed class CountedConvex : IConvexShape2D
    {
        public string Kind => "counted-convex";
        public int SupportCalls { get; private set; }
        public float Area => 24;
        public bool ContainsPoint(Vector2 point) => Containment2D.Rectangle(point, new(-2, -1), new(4, 3));
        public Vector2 GetSupportPoint(Vector2 direction)
        {
            SupportCalls++;
            return SupportPoint2D.Rectangle(direction, new(-2, -1), new(4, 3));
        }
    }

    private sealed class UnknownShape : IShape2D
    {
        public string Kind => "unknown";
        public float Area => 0;
        public bool ContainsPoint(Vector2 point) => false;
    }
}
