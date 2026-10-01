using App2d.Core;
using App2d.Core.Collision.Contacts;
using App2d.Core.Shapes;
using System.Numerics;

namespace App2d.Tests.Collision;

public sealed class PolygonContactTests
{
    private static SpatialObject2D At(IShape2D shape, Vector2 position, float rotation = 0f)
    {
        var placed = new SpatialObject2D(shape);
        placed.Transform.Position = position;
        placed.Transform.Rotation = rotation;
        return placed;
    }

    private static ConvexPolygon2D Diamond(float size) => new([new(0, -size), new(size, 0), new(0, size), new(-size, 0)]);

    public static TheoryData<IShape2D> Partners => new()
    {
        Diamond(1),
        Rectangle2D.FromSize(new Vector2(2, 2)),
        new AxisAlignedRectangle2D(new(-1), new(1)),
        new Triangle2D(new(-1, -1), new(1, -1), new(0, 1)),
        new Capsule2D(new(-.5f, 0), new(.5f, 0), .5f),
        new Circle2D(1),
        new Ellipse2D(new(1.2f, .8f)),
        new HalfSpace2D(-Vector2.UnitX, 1),
    };

    [Theory]
    [MemberData(nameof(Partners))]
    public void ConvexPolygonCollidesWithEveryShapeInEitherOrder(IShape2D partnerShape)
    {
        var polygon = At(Diamond(1.5f), Vector2.Zero, .3f);
        var partner = At(partnerShape, new(1.2f, .1f));
        Assert.True(ShapeCollision2D.TryGetContact(polygon, partner, out var forward), partnerShape.GetType().Name);
        Assert.True(ShapeCollision2D.TryGetContact(partner, polygon, out var reverse), partnerShape.GetType().Name);
        Assert.True(forward.PenetrationDepth > 0f);
        Assert.Equal(forward.PenetrationDepth, reverse.PenetrationDepth, 4);
        Assert.True(Vector2.Distance(forward.Normal, -reverse.Normal) < 1e-4f);
        Assert.InRange(forward.Normal.Length(), .9999f, 1.0001f);
    }

    [Fact]
    public void SeparatedPolygonsProduceNoContactAndTouchingBoxesResolveAlongTheBoxEdge()
    {
        var diamond = At(Diamond(1), Vector2.Zero);
        Assert.False(ShapeCollision2D.TryGetContact(diamond, At(Diamond(1), new(2.01f, 0)), out _));
        Assert.False(ShapeCollision2D.TryGetContact(diamond, At(Rectangle2D.FromSize(new Vector2(2, 2)), new(2.01f, 0)), out _));
        Assert.False(ShapeCollision2D.TryGetContact(diamond, At(new Capsule2D(new(0, -1), new(0, 1), .5f), new(1.51f, 0)), out _));

        // A box edge normal is the shortest escape when a diamond tip pokes into a box.
        var box = At(Rectangle2D.FromSize(new Vector2(2, 2)), Vector2.Zero);
        Assert.True(ShapeCollision2D.TryGetContact(box, At(Diamond(1), new(1.9f, 0)), out var contact));
        Assert.Equal(.1f, contact.PenetrationDepth, 4);
        Assert.Equal(-Vector2.UnitX, contact.Normal);

        // Tip to tip, the diamonds escape along a diagonal edge normal, which is shorter than the axis overlap.
        Assert.True(ShapeCollision2D.TryGetContact(diamond, At(Diamond(1), new(1.9f, 0)), out var tips));
        Assert.Equal(.1f / MathF.Sqrt(2), tips.PenetrationDepth, 4);
    }
}
