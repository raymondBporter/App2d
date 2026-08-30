using System.Numerics;
using App2d.Engine;
using App2d.Engine.Collision;
using App2d.Engine.Geometry;

namespace App2d.Tests.Collision;

public sealed class ShapeContactCharacterizationTests
{
    private static readonly ShapeCollisionContactProvider2D Provider = new();

    private static SpatialObject2D At(IShape2D shape, Vector2 position, float rotation = 0f)
    {
        var worldObject = new SpatialObject2D(shape);
        worldObject.Transform.Position = position;
        worldObject.Transform.Rotation = rotation;
        return worldObject;
    }

    [Fact]
    public void RectangleVsHalfSpaceReportsUpNormalAndDepth()
    {
        var box = At(Rectangle2D.FromSize(new Vector2(10f, 10f)), new Vector2(0f, -3f));
        var ground = At(new HalfSpace2D(Vector2.UnitY, 0f), Vector2.Zero);

        Assert.True(Provider.TryGetContact(box, ground, out var contact));
        Assert.Equal(1f, contact.Normal.Y, 3);
        Assert.Equal(8f, contact.PenetrationDepth, 3);

        Assert.True(Provider.TryGetContact(ground, box, out var flipped));
        Assert.Equal(-1f, flipped.Normal.Y, 3);
        Assert.Equal(8f, flipped.PenetrationDepth, 3);
    }

    [Fact]
    public void CapsuleVsHalfSpaceReportsDepthFromDeepestPoint()
    {
        var capsule = At(new Capsule2D(new Vector2(0f, -2f), new Vector2(0f, 2f), 1f), new Vector2(0f, 1f));
        var ground = At(new HalfSpace2D(Vector2.UnitY, 0f), Vector2.Zero);

        Assert.True(Provider.TryGetContact(capsule, ground, out var contact));
        Assert.Equal(1f, contact.Normal.Y, 3);
        Assert.Equal(2f, contact.PenetrationDepth, 3);
    }

    [Fact]
    public void CircleVsHalfSpaceContactPointLiesOnTheBoundary()
    {
        var circle = At(new Circle2D(2f), new Vector2(5f, 1f));
        var ground = At(new HalfSpace2D(Vector2.UnitY, 0f), Vector2.Zero);

        Assert.True(Provider.TryGetContact(circle, ground, out var contact));
        Assert.Equal(0f, contact.Point.Y, 3);
        Assert.Equal(1f, contact.PenetrationDepth, 3);
    }

    [Fact]
    public void OverlappingCapsulesResolveAlongTheShortestAxis()
    {
        var first = At(new Capsule2D(new Vector2(-2f, 0f), new Vector2(2f, 0f), 1f), Vector2.Zero);
        var second = At(new Capsule2D(new Vector2(-2f, 0f), new Vector2(2f, 0f), 1f), new Vector2(0f, 1.5f));

        Assert.True(Provider.TryGetContact(first, second, out var contact));
        Assert.Equal(0f, contact.Normal.X, 3);
        Assert.Equal(-1f, contact.Normal.Y, 3);
        Assert.Equal(0.5f, contact.PenetrationDepth, 3);
    }

    [Fact]
    public void RotatedRectangleVsRectangleFindsMtv()
    {
        var first = At(Rectangle2D.FromSize(new Vector2(4f, 4f)), Vector2.Zero);
        var second = At(Rectangle2D.FromSize(new Vector2(4f, 4f)), new Vector2(3.5f, 0f), MathF.PI / 4f);

        Assert.True(Provider.TryGetContact(first, second, out var contact));
        Assert.True(contact.PenetrationDepth > 0f);
        Assert.True(MathF.Abs(contact.Normal.Length() - 1f) < 0.001f);
    }
}
