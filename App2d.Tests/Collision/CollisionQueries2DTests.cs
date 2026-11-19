using App2d.Core.Collision.Contacts;
using App2d.Core.Collision.Queries;
using App2d.Core.Geometry.Primitives;
using App2d.Core.Geometry.Shapes;
using App2d.Core.Spatial;

namespace App2d.Tests.Collision;

public sealed class CollisionQueries2DTests
{
    [Fact]
    public void OverlappingCirclesReturnTheirPenetration()
    {
        var first = new SpatialObject2D(Circle2D.Builder.Create(10f));
        var second = new SpatialObject2D(Circle2D.Builder.Create(10f))
        {
            Position = new Vector2f(15f, 0f)
        };

        var intersects = ShapeCollision2D.TryGetContact(first, second, out var contact);

        Assert.True(intersects);
        Assert.Equal(5f, contact.PenetrationDepth, 5);
        Assert.Equal(new Vector2f(-1f, 0f), contact.Normal);
    }

    [Fact]
    public void RaycastReturnsTheNearestSpatialObject()
    {
        var farther = new SpatialObject2D(Circle2D.Builder.Create(2f))
        {
            Position = new Vector2f(10f, 0f)
        };
        var nearer = new SpatialObject2D(Circle2D.Builder.Create(2f))
        {
            Position = new Vector2f(5f, 0f)
        };
        SpatialObject2D[] objects = [farther, nearer];

        var found = objects.Raycast(
            new Ray2D(Vector2f.Zero, Vector2f.UnitX),
            20f,
            out var hit);

        Assert.True(found);
        Assert.Same(nearer, hit.Item);
        Assert.Equal(3f, hit.Distance, 5);
        Assert.Equal(new Vector2f(3f, 0f), hit.Point);
        Assert.Equal(new Vector2f(-1f, 0f), hit.Normal);
    }

    [Fact]
    public void RaycastHitsARotatedRectangle()
    {
        var box = new SpatialObject2D(Rectangle2D.Builder.CreateFromSize(4f, 4f))
        {
            Position = new Vector2f(10f, 0f),
            Rotation = MathF.PI / 4f
        };

        var found = new[] { box }.Raycast(
            new Ray2D(Vector2f.Zero, Vector2f.UnitX), 20f, out var hit);

        Assert.True(found);
        // The rotated box's near corner sits at x = 10 - 2âˆš2; the ray meets one
        // of its two diagonal edges, so the normal's X is -âˆš2/2 either way.
        Assert.Equal(10f - 2f * MathF.Sqrt(2f), hit.Distance, 3);
        Assert.Equal(-MathF.Sqrt(2f) / 2f, hit.Normal.X, 3);
    }
}
