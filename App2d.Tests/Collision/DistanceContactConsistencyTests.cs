using App2d.Core.Collision.Contacts;
using App2d.Core;
using App2d.Core.Geometry;
using System.Numerics;

namespace App2d.Tests.Collision;

public sealed class DistanceContactConsistencyTests
{
    [Fact]
    public void SignedDistancesAgreeWithExistingContactDepthsAcrossSupportedPairsAndPoses()
    {
        IShape2D circle = new Circle2D(1.3f, new(.2f, -.1f));
        IShape2D capsule = new Capsule2D(new(-1.5f, -.2f), new(1, .4f), .6f);
        IShape2D rectangle = Rectangle2D.FromSize(new(2, 3));
        IShape2D polygon = new ConvexPolygon2D([new(-1, -1), new(2, -1), new(.5f, 2)]);
        IShape2D halfSpace = new HalfSpace2D(Vector2.UnitY, .3f);
        (IShape2D First, IShape2D Second)[] pairs =
        [
            (circle, circle), (circle, capsule), (circle, rectangle), (circle, polygon),
            (capsule, capsule), (capsule, rectangle), (rectangle, rectangle),
            (circle, halfSpace), (capsule, halfSpace), (rectangle, halfSpace), (polygon, halfSpace)
        ];
        var random = new Random(1984);
        foreach (var (firstShape, secondShape) in pairs)
        for (var i = 0; i < 50; i++)
        {
            var first = Place(firstShape);
            var second = Place(secondShape);
            var distance = Distance2D.SignedDistance(first, second);
            var reversed = Distance2D.SignedDistance(second, first);
            Assert.True(MathF.Abs(distance - reversed) < .0001f, $"Asymmetric distance: {distance}, {reversed}");
            var hasContact = ShapeCollision2D.TryGetContact(first, second, out var contact);
            Assert.Equal(distance < 0f, hasContact);
            if (hasContact)
                Assert.True(MathF.Abs(contact.PenetrationDepth + distance) < .0002f,
                    $"{firstShape.GetType().Name}/{secondShape.GetType().Name}: depth {contact.PenetrationDepth}, signed distance {distance}");
        }

        SpatialObject2D Place(IShape2D shape)
        {
            var placed = new SpatialObject2D(shape);
            placed.Transform.Position = new(random.NextSingle() * 6 - 3, random.NextSingle() * 6 - 3);
            placed.Transform.Rotation = random.NextSingle() * MathF.Tau;
            var scale = .5f + random.NextSingle() * 1.5f;
            placed.Transform.Scale = new(random.Next(2) == 0 ? -scale : scale, scale);
            return placed;
        }
    }
}
