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

    public static TheoryData<string> Partners =>
    [
        nameof(ConvexPolygon2D),
        nameof(Rectangle2D),
        nameof(AxisAlignedRectangle2D),
        nameof(Triangle2D),
        nameof(Capsule2D),
        nameof(Circle2D),
        nameof(Ellipse2D),
        nameof(HalfSpace2D),
    ];

    private static IShape2D CreatePartner(string shapeName) => shapeName switch
    {
        nameof(ConvexPolygon2D) => Diamond(1),
        nameof(Rectangle2D) => Rectangle2D.FromSize(new Vector2(2, 2)),
        nameof(AxisAlignedRectangle2D) => new AxisAlignedRectangle2D(new(-1), new(1)),
        nameof(Triangle2D) => new Triangle2D(new(-1, -1), new(1, -1), new(0, 1)),
        nameof(Capsule2D) => new Capsule2D(new(-.5f, 0), new(.5f, 0), .5f),
        nameof(Circle2D) => new Circle2D(1),
        nameof(Ellipse2D) => new Ellipse2D(new(1.2f, .8f)),
        nameof(HalfSpace2D) => new HalfSpace2D(-Vector2.UnitX, 1),
        _ => throw new ArgumentOutOfRangeException(nameof(shapeName), shapeName, null)
    };

    [Theory]
    [MemberData(nameof(Partners))]
    public void ConvexPolygonCollidesWithEveryShapeInEitherOrder(string shapeName)
    {
        var partnerShape = CreatePartner(shapeName);
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
