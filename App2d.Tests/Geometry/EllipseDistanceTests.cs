using App2d.Core;
using App2d.Core.Collision.Contacts;
using App2d.Core.Geometry;
using App2d.Core.Mathematics;
using App2d.Core.Shapes;
using System.Numerics;

namespace App2d.Tests.Geometry;

public sealed class EllipseDistanceTests
{
    public static TheoryData<float, float> Radii => new() { { 2, 1 }, { 1, 2 }, { 3, 3 }, { 10, 1 }, { 1, 10 }, { 100, 1.5f }, { .25f, 4 } };

    [Theory]
    [MemberData(nameof(Radii))]
    public void ClosestPerimeterPointBeatsEveryDenseSampleAndLiesOnTheEllipse(float a, float b)
    {
        var center = new Vector2(3, -2);
        var radii = new Vector2(a, b);
        var random = new Random(7);
        Vector2[] queries =
        [
            center, center + new Vector2(a, 0), center + new Vector2(0, b), center + new Vector2(-a * 3, 0), center + new Vector2(0, b * 3),
            center + new Vector2(a * .5f, 0), center + new Vector2(0, b * .5f), center + new Vector2(a * 2, b * 2), center + new Vector2(-a * .1f, b * .9f),
            .. Enumerable.Range(0, 60).Select(_ => center + new Vector2(random.NextFloat(-3f, 3f) * a, random.NextFloat(-3f, 3f) * b))
        ];
        foreach (var point in queries)
        {
            var closest = ClosestPoint2D.OnEllipsePerimeter(point, center, radii);
            Assert.InRange(Containment2D.NormalizedEllipseRadius(closest, center, radii), .99999f, 1.00001f);
            var distance = Vector2.Distance(point, closest);
            var sampled = float.PositiveInfinity;
            for (var i = 0; i < 8192; i++)
                sampled = MathF.Min(sampled, Vector2.Distance(point, VertexGenerator2D.PointOnEllipse(center, radii, i * MathF.Tau / 8192)));
            var scale = MathF.Max(a, b);
            Assert.True(distance <= sampled + 1e-4f * scale, $"Point {point}: iterative {distance} is worse than sampled {sampled}.");
            Assert.True(distance >= sampled - MathF.Max(1e-3f * scale, 4e-6f * scale * scale), $"Point {point}: iterative {distance} is unrealistically better than sampled {sampled}.");
        }
    }

    [Fact]
    public void SignedDistanceIsExactForAxisPointsAndNegativeInside()
    {
        var radii = new Vector2(2, 1);
        Assert.Equal(1f, Distance2D.SignedDistanceToEllipse(new(3, 0), default, radii), 5);
        Assert.Equal(2f, Distance2D.SignedDistanceToEllipse(new(0, 3), default, radii), 5);
        Assert.Equal(-1f, Distance2D.SignedDistanceToEllipse(default, default, radii), 5);
        Assert.Equal(-.5f, Distance2D.SignedDistanceToEllipse(new(0, .5f), default, radii), 5);
        Assert.Equal(0f, Distance2D.SignedDistanceToEllipse(new(2, 0), default, radii), 5);
        Assert.Equal(0f, Distance2D.DistanceToEllipse(new(1, .3f), default, radii));
        Assert.Equal(1f, ShapeDistance2D.SignedDistance(new Vector2(3, 0), new Ellipse2D(radii)), 5);
        Assert.Equal(-1f, ShapeDistance2D.SignedDistance(Vector2.Zero, new Ellipse2D(radii)), 5);
        var placed = new SpatialObject2D(new Ellipse2D(radii));
        placed.Transform.Rotation = MathF.PI / 2;
        placed.Transform.Scale = new(2);
        placed.Transform.Position = new(10, 10);
        Assert.Equal(2f, ShapeDistance2D.SignedDistance(new Vector2(10, 16), placed), 4); // vertical semi-axis is now 4
        Assert.Equal(4f, ShapeDistance2D.SignedDistance(new Vector2(16, 10), placed), 4); // horizontal semi-axis is now 2
    }

    [Fact]
    public void CircleAgainstEllipseIsExactInBothDirectionsAndMatchesSignedDistance()
    {
        var ellipse = new SpatialObject2D(new Ellipse2D(new(2, 1)));
        ellipse.Transform.Rotation = .4f;
        ellipse.Transform.Scale = new(-1.5f, 1.5f);
        ellipse.Transform.Position = new(5, -3);
        var circle = new SpatialObject2D(new Circle2D(.75f));
        var random = new Random(11);
        var contacts = 0;
        for (var i = 0; i < 200; i++)
        {
            circle.Transform.Position = ellipse.Transform.Position + new Vector2(random.NextFloat(-4f, 4f), random.NextFloat(-4f, 4f));
            var signed = ShapeDistance2D.SignedDistance(circle, ellipse);
            Assert.Equal(signed, ShapeDistance2D.SignedDistance(ellipse, circle), 5);
            var hit = ShapeCollision2D.TryGetContact(circle, ellipse, out var contact);
            Assert.Equal(signed < 0f, hit);
            if (!hit) continue;
            contacts++;
            Assert.Equal(-signed, contact.PenetrationDepth, 4);
            Assert.True(ShapeCollision2D.TryGetContact(ellipse, circle, out var reverse));
            Assert.Equal(contact.PenetrationDepth, reverse.PenetrationDepth, 4);
            Assert.True(Vector2.Distance(contact.Normal, -reverse.Normal) < 1e-4f);
            Assert.InRange(contact.Normal.Length(), .9999f, 1.0001f);
            // Moving the circle out along the normal by the depth separates it (within float noise).
            circle.Transform.Position += contact.Normal * (contact.PenetrationDepth + 1e-3f);
            Assert.False(ShapeCollision2D.TryGetContact(circle, ellipse, out _));
        }
        Assert.True(contacts > 20);
    }

    [Fact]
    public void CircleInsideEllipseEscapesTowardTheNearestBoundary()
    {
        var ellipse = new SpatialObject2D(new Ellipse2D(new(4, 1)));
        var circle = new SpatialObject2D(new Circle2D(.25f));
        circle.Transform.Position = new(1, .2f);
        Assert.True(ShapeCollision2D.TryGetContact(circle, ellipse, out var contact));
        Assert.True(contact.Normal.Y > .9f, $"Expected an upward escape, got {contact.Normal}.");
        Assert.InRange(contact.PenetrationDepth, .25f + .7f, .25f + .85f);
    }
}
