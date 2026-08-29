using System.Numerics;
using App2d.Engine;
using App2d.Engine.Collision.Contacts;
using App2d.Engine.Collision.Queries;
using App2d.Engine.Geometry;

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
}
