using App2d.Core;
using App2d.Core.Geometry;
using App2d.Core.Geometry.Functions;
using App2d.Core.Geometry.Shapes;
using System.Numerics;

namespace App2d.Tests.Geometry;

public sealed class BoundsGeometry2DTests
{
    [Fact]
    public void RawPrimitiveBoundsIncludeOffsetsAndReversedCapsuleEndpoints()
    {
        Assert.Equal(new Bounds2D(new(-2, -7), new(6, 1)), BoundsGeometry2D.FromCircle(new(2, -3), 4));
        var capsule = BoundsGeometry2D.FromCapsule(new(5, -2), new(-3, 4), 2);
        Assert.Equal(new Bounds2D(new(-5, -4), new(7, 6)), capsule);
        Assert.Equal(capsule, BoundsGeometry2D.FromCapsule(new(-3, 4), new(5, -2), 2));
        Assert.Equal(BoundsGeometry2D.FromCircle(new(1, 2), 3), BoundsGeometry2D.FromCapsule(new(1, 2), new(1, 2), 3));
        Assert.Equal(new Bounds2D(new(1, 2), new(1, 2)), BoundsGeometry2D.FromCircle(new(1, 2), 0));
        Assert.Equal(new Bounds2D(new(-3, -4), new(5, 6)),
            BoundsGeometry2D.FromRectangle(new Rect2D(new(-3, -4), new(5, 6))));
    }

    [Fact]
    public void PointBoundsAndUnionHandleDifferentExtremaAndSinglePoints()
    {
        Vector2[] points = [new(3, 4), new(-2, 7), new(5, -1)];
        var bounds = BoundsGeometry2D.FromPoints(points);
        Assert.Equal(new Bounds2D(new(-2, -1), new(5, 7)), bounds);
        Assert.Equal(bounds, Bounds2D.FromPoints(points));
        Assert.Equal(new Bounds2D(new(3, 4), new(3, 4)), BoundsGeometry2D.FromPoints(points.AsSpan(0, 1)));
        Assert.Equal(new Bounds2D(new(-2, -4), new(10, 7)),
            BoundsGeometry2D.Union(bounds, new(new(8, -4), new(10, 3))));
        Assert.Throws<ArgumentOutOfRangeException>(() => BoundsGeometry2D.FromPoints([]));
    }

    [Fact]
    public void ShapesCalculateBoundsWithoutOwningThem()
    {
        var expected = new Bounds2D(new(-2, -3), new(4, 5));
        IShape2D[] shapes =
        [
            new Rectangle2D(expected.Min, expected.Max),
            new AxisAlignedRectangle2D(expected.Min, expected.Max),
            new Capsule2D(new(1, 0), new(1, 2), 3),
            new ConvexPolygon2D([new(-2, -3), new(4, -3), new(1, 5)]),
            new CompositeShape2D([new Circle2D(1, new(-1, -2)), new Rectangle2D(new(1, 2), new(4, 5))]),
        ];
        foreach (var shape in shapes)
        {
            Assert.Equal(expected, ShapeBounds2D.Calculate(shape));
            Assert.Equal(expected, new SpatialObject2D(shape).LocalBounds);
        }
        Assert.Equal(new Bounds2D(new(-1, 0), new(5, 6)), ShapeBounds2D.Calculate(new Circle2D(3, new(2, 3))));
    }

    [Fact]
    public void SpatialObjectCalculatesLocalBoundsOnceAndRefreshesWorldBoundsForEveryTransformChange()
    {
        var shape = new CountedConvex();
        var item = new SpatialObject2D(shape);
        var local = new Bounds2D(new(-2, -1), new(4, 3));
        Assert.Equal(4, shape.SupportCalls);
        Assert.Equal(local, item.LocalBounds);
        Assert.Equal(local, item.WorldBounds);

        item.Transform.Position = new(10, -5);
        Assert.Equal(new Bounds2D(new(8, -6), new(14, -2)), item.WorldBounds);
        item.Transform.Scale = new(-2, 3);
        Assert.Equal(new Bounds2D(new(2, -8), new(14, 4)), item.WorldBounds);
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
        var local = new Bounds2D(new(-2, -1), new(4, 3));
        var scale = new Vector2(xScale, yScale);
        var translation = new Vector2(10, -5);
        var matrix = Matrix3x2.CreateScale(scale) * Matrix3x2.CreateTranslation(translation);
        var expected = CornerBounds(local, matrix);
        Near(expected, BoundsGeometry2D.ScaleAndTranslate(local, scale, translation));
        Near(expected, BoundsGeometry2D.Transform(local, matrix));
        Near(expected, local.TransformedBy(matrix));
        if (scale == Vector2.One) Near(expected, BoundsGeometry2D.Translate(local, translation));
    }

    [Theory]
    [InlineData(.000001f, false)]
    [InlineData(.7f, false)]
    [InlineData(1.5707963f, false)]
    [InlineData(-.4f, true)]
    public void RotatedAndShearedTransformsEncloseTheBox(float angle, bool shear)
    {
        var local = new Bounds2D(new(-2, -1), new(4, 3));
        var matrix = Matrix3x2.CreateScale(-2, 3) * Matrix3x2.CreateRotation(angle) * Matrix3x2.CreateTranslation(10, -5);
        if (shear) matrix.M21 += .6f;
        Near(CornerBounds(local, matrix), BoundsGeometry2D.Transform(local, matrix));
    }

    [Fact]
    public void RotatedCircleBoundsRemainConservativeUntilShapeSpecificWorldBoundsAreAdded()
    {
        var circle = new Circle2D(2, new(1, -3));
        var item = new SpatialObject2D(circle);
        item.Transform.Rotation = MathF.PI / 4;
        item.Transform.Position = new(10, 20);
        var world = item.WorldBounds;
        Assert.True(world.Size.X > 4f);
        for (var i = 0; i < 64; i++)
        {
            var point = VertexGenerator2D.PointOnEllipse(circle.Center, new(circle.Radius), i * MathF.Tau / 64);
            Assert.True(world.Contains(Vector2.Transform(point, item.Transform.LocalToWorldMatrix)));
        }
    }

    [Fact]
    public void HalfSpacesStayUnboundedThroughAllTransformPaths()
    {
        var item = new SpatialObject2D(new HalfSpace2D(Vector2.UnitY, 0));
        Assert.Equal(Bounds2D.Unbounded, item.LocalBounds);
        Assert.Equal(Bounds2D.Unbounded, item.WorldBounds);
        item.Transform.Position = new(100, -25);
        Assert.Equal(Bounds2D.Unbounded, item.WorldBounds);
        item.Transform.Scale = new(-2, 3);
        Assert.Equal(Bounds2D.Unbounded, item.WorldBounds);
        item.Transform.Rotation = .7f;
        Assert.Equal(Bounds2D.Unbounded, item.WorldBounds);
        Assert.Equal(Bounds2D.Unbounded, BoundsGeometry2D.ScaleAndTranslate(item.LocalBounds, Vector2.Zero, default));
    }

    [Fact]
    public void UnknownNonconvexShapesFailExplicitlyAndNullShapesAreRejected()
    {
        Assert.Throws<NotSupportedException>(() => ShapeBounds2D.Calculate(new UnknownShape()));
        Assert.Throws<ArgumentNullException>(() => ShapeBounds2D.Calculate(null!));
        Assert.Throws<ArgumentNullException>(() => new SpatialObject2D(null!));
    }

    private static Bounds2D CornerBounds(Bounds2D bounds, Matrix3x2 matrix)
    {
        Vector2[] corners = [bounds.Min, new(bounds.Max.X, bounds.Min.Y), bounds.Max, new(bounds.Min.X, bounds.Max.Y)];
        var transformed = corners.Select(point => Vector2.Transform(point, matrix)).ToArray();
        return new(new(transformed.Min(p => p.X), transformed.Min(p => p.Y)),
            new(transformed.Max(p => p.X), transformed.Max(p => p.Y)));
    }

    private static void Near(Bounds2D expected, Bounds2D actual)
    {
        Assert.True(Vector2.Distance(expected.Min, actual.Min) < .00001f, $"Min: expected {expected.Min}, got {actual.Min}.");
        Assert.True(Vector2.Distance(expected.Max, actual.Max) < .00001f, $"Max: expected {expected.Max}, got {actual.Max}.");
    }

    private sealed class CountedConvex : IConvexShape2D
    {
        public int SupportCalls { get; private set; }
        public float Area => 24;
        public bool ContainsPoint(Vector2 point) => PrimitiveGeometry2D.RectangleContainsPoint(point, new(-2, -1), new(4, 3));
        public Vector2 GetSupportPoint(Vector2 direction)
        {
            SupportCalls++;
            return PrimitiveGeometry2D.RectangleSupportPoint(direction, new(-2, -1), new(4, 3));
        }
    }

    private sealed class UnknownShape : IShape2D
    {
        public float Area => 0;
        public bool ContainsPoint(Vector2 point) => false;
    }
}
