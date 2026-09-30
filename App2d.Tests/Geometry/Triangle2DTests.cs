using App2d.Core;
using App2d.Core.Collision.Contacts;
using App2d.Core.Collision.Queries;
using App2d.Core.Geometry;
using App2d.Core.Shapes;
using System.Numerics;

namespace App2d.Tests.Geometry;

public sealed class Triangle2DTests
{
    private static Triangle2D RightTriangle(bool reversed = false) => reversed
        ? new(new(0, 0), new(0, 3), new(4, 0))
        : new(new(0, 0), new(4, 0), new(0, 3));

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AreaContainmentSupportAndBoundsWorkInEitherWinding(bool reversed)
    {
        var triangle = RightTriangle(reversed);
        Assert.Equal(6f, triangle.Area);
        Assert.True(triangle.ContainsPoint(new(1, 1)));
        Assert.True(triangle.ContainsPoint(new(2, 0)));
        Assert.True(triangle.ContainsPoint(new(0, 3)));
        Assert.False(triangle.ContainsPoint(new(3, 2)));
        Assert.False(triangle.ContainsPoint(new(-.1f, 1)));
        Assert.Equal(new Vector2(4, 0), triangle.GetSupportPoint(Vector2.UnitX));
        Assert.Equal(new Vector2(0, 3), triangle.GetSupportPoint(Vector2.UnitY));
        Assert.Equal(new Rect2D(default, new(4, 3)), ShapeBounds2D.Calculate(triangle));
        Assert.Equal(new Rect2D(default, new(4, 3)), new SpatialObject2D(triangle).LocalBounds);
        Assert.Equal(-1f, Distance2D.SignedDistance(new Vector2(1, 1), triangle), 5);
        Assert.Equal(1f, Distance2D.Distance(new Vector2(5, 0), triangle), 5);
    }

    [Fact]
    public void VerticesAreWrittenWithoutAnArrayOwnedByTheShape()
    {
        var triangle = RightTriangle();
        Span<Vector2> buffer = stackalloc Vector2[4];
        buffer[3] = new(99, 99);
        triangle.WriteVertices(buffer);
        Assert.Equal(new Vector2(0, 0), buffer[0]);
        Assert.Equal(new Vector2(4, 0), buffer[1]);
        Assert.Equal(new Vector2(0, 3), buffer[2]);
        Assert.Equal(new Vector2(99, 99), buffer[3]);
        Assert.Throws<ArgumentOutOfRangeException>(() => triangle.WriteVertices(new Vector2[2]));
    }

    [Fact]
    public void InvalidOrDegenerateTrianglesAreRejected()
    {
        Assert.Throws<ArgumentException>(() => new Triangle2D(default, Vector2.One, new(2, 2)));
        Assert.Throws<ArgumentException>(() => new Triangle2D(default, default, Vector2.UnitX));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Triangle2D(new(float.NaN, 0), Vector2.UnitX, Vector2.UnitY));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Triangle2D(default, new(float.PositiveInfinity, 0), Vector2.UnitY));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Triangle2D(default, new(float.MaxValue, 0), new(0, float.MaxValue)));
        Assert.Throws<ArgumentOutOfRangeException>(() => RightTriangle().ContainsPoint(new(float.NaN, 0)));
        Assert.Throws<ArgumentOutOfRangeException>(() => RightTriangle().GetSupportPoint(new(float.NaN, 0)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RaycastsHitTransformedTrianglesAndRespectTheNearestSurface(bool reversed)
    {
        var local = reversed
            ? new Triangle2D(new(0, -1), new(0, 1), new(2, -1))
            : new Triangle2D(new(0, -1), new(2, -1), new(0, 1));
        var item = new SpatialObject2D(local);
        item.Transform.Position = new(10, 0);
        Assert.True(RayIntersection2D.TryIntersect(new Ray2D(default, Vector2.UnitX), item, 20, out var hit));
        Assert.Equal(10f, hit.Distance, 5);
        Assert.Equal(new Vector2(10, 0), hit.Point);
        Assert.Equal(-Vector2.UnitX, hit.Normal);
        Assert.False(RayIntersection2D.TryIntersect(new Ray2D(default, Vector2.UnitX), item, 9, out _));
        item.Transform.Rotation = MathF.PI / 2;
        Assert.True(RayIntersection2D.TryIntersect(new Ray2D(new(10, -5), Vector2.UnitY), item, 20, out _));
    }

    [Fact]
    public void CollisionContactsWorkInBothOrdersWithEachSupportedShapeFamily()
    {
        var triangle = new SpatialObject2D(RightTriangle());
        IShape2D[] others =
        [
            new Circle2D(.4f, new(1, 1)),
            new Capsule2D(new(.5f, .5f), new(2, .5f), .3f),
            new Rectangle2D(new(.5f, .5f), new(2, 1.5f)),
            new Triangle2D(new(.5f, .5f), new(2, .5f), new(.5f, 2)),
            new ConvexPolygon2D([new(.5f, .5f), new(2, .5f), new(2, 1.5f), new(.5f, 1.5f)]),
            new HalfSpace2D(Vector2.UnitY, 1),
            new CompositeShape2D([new Circle2D(.4f, new(1, 1)), new Circle2D(.4f, new(10, 10))]),
        ];
        foreach (var shape in others)
        {
            var other = new SpatialObject2D(shape);
            Assert.True(ShapeCollision2D.TryGetContact(triangle, other, out var forward), shape.GetType().Name);
            Assert.True(ShapeCollision2D.TryGetContact(other, triangle, out var reverse), shape.GetType().Name);
            Assert.True(forward.PenetrationDepth > 0f, shape.GetType().Name);
            Assert.Equal(forward.PenetrationDepth, reverse.PenetrationDepth, 4);
            Assert.True(Vector2.Distance(-forward.Normal, reverse.Normal) < .0001f, shape.GetType().Name);
        }
    }
}
