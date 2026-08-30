using App2d.Collision.Contacts;
using App2d.Collision.Queries;
using App2d.Core;
using App2d.Core.Geometry;
using System.Numerics;

namespace App2d.Tests.Collision;

public sealed class CollisionQueries2DTests
{
    [Fact]
    public void OverlappingCirclesReturnTheirPenetration()
    {
        var first = new SpatialObject2D(new Circle2D(10f));
        var second = new SpatialObject2D(new Circle2D(10f));
        second.Transform.Position = new Vector2(15f, 0f);

        var intersects = ShapeCollision2D.TryGetContact(first, second, out var contact);

        Assert.True(intersects);
        Assert.Equal(5f, contact.PenetrationDepth, 5);
        Assert.Equal(new Vector2(-1f, 0f), contact.Normal);
    }

    [Fact]
    public void RaycastReturnsTheNearestSpatialObject()
    {
        var farther = new SpatialObject2D(new Circle2D(2f));
        farther.Transform.Position = new Vector2(10f, 0f);
        var nearer = new SpatialObject2D(new Circle2D(2f));
        nearer.Transform.Position = new Vector2(5f, 0f);
        SpatialObject2D[] objects = [farther, nearer];

        var found = objects.Raycast(
            new Ray2D(Vector2.Zero, Vector2.UnitX),
            20f,
            out var hit);

        Assert.True(found);
        Assert.Same(nearer, hit.Item);
        Assert.Equal(3f, hit.Distance, 5);
        Assert.Equal(new Vector2(3f, 0f), hit.Point);
        Assert.Equal(new Vector2(-1f, 0f), hit.Normal);
    }

    [Fact]
    public void RaycastHitsARotatedRectangle()
    {
        var box = new SpatialObject2D(Rectangle2D.FromSize(new Vector2(4f, 4f)));
        box.Transform.Position = new Vector2(10f, 0f);
        box.Transform.Rotation = MathF.PI / 4f;

        var found = new[] { box }.Raycast(
            new Ray2D(Vector2.Zero, Vector2.UnitX), 20f, out var hit);

        Assert.True(found);
        // The rotated box's near corner sits at x = 10 - 2âˆš2; the ray meets one
        // of its two diagonal edges, so the normal's X is -âˆš2/2 either way.
        Assert.Equal(10f - 2f * MathF.Sqrt(2f), hit.Distance, 3);
        Assert.Equal(-MathF.Sqrt(2f) / 2f, hit.Normal.X, 3);
    }
}
