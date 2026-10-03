using App2d.Core;
using App2d.Core.Collision.Contacts;
using App2d.Core.Collision.Queries;
using App2d.Core.Geometry;
using App2d.Core.Shapes;
using System.Numerics;

namespace App2d.Tests.Geometry;

public sealed class Ellipse2DTests
{
    [Fact]
    public void AreaContainmentAndBoundsUseTheExactEllipse()
    {
        var ellipse = new Ellipse2D(new(3, 2), new(4, -1));
        Assert.Equal(6f * MathF.PI, ellipse.Area, 5);
        Assert.Equal(new Rect2D(new(1, -3), new(7, 1)), ShapeBounds2D.Calculate(ellipse));
        Assert.True(ellipse.ContainsPoint(new(7, -1)));
        Assert.True(ellipse.ContainsPoint(new(4, 1)));
        Assert.False(ellipse.ContainsPoint(new(7, 1)));
        Assert.Equal(ellipse.Center, ellipse.GetSupportPoint(Vector2.Zero));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Ellipse2D(new(0, 1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Ellipse2D(new(float.NaN, 1)));
    }

    [Fact]
    public void SupportPointMaximizesProjectionInAnyDirection()
    {
        var ellipse = new Ellipse2D(new(3, 1), new(2, -4));
        foreach (var direction in new[] { Vector2.UnitX, -Vector2.UnitY, Vector2.Normalize(new Vector2(1, 2)) })
        {
            var support = ellipse.GetSupportPoint(direction);
            Assert.InRange(Containment2D.NormalizedEllipseRadius(support, ellipse.Center, ellipse.Radii), .99999f, 1.00001f);
            for (var i = 0; i < 256; i++)
            {
                var sample = VertexGenerator2D.PointOnEllipse(ellipse.Center, ellipse.Radii, i * MathF.Tau / 256);
                Assert.True(Vector2.Dot(support, direction) >= Vector2.Dot(sample, direction) - 1e-5f);
            }
        }
        Assert.Equal(new Vector2(5, -4), ellipse.GetSupportPoint(Vector2.UnitX));
    }

    [Fact]
    public void RaycastFindsTheExactEllipseBoundary()
    {
        var ellipse = new SpatialObject2D(new Ellipse2D(new(2, 1), new(1, 0)));
        Assert.True(RayIntersection2D.TryIntersect(new Ray2D(new(-5, 0), Vector2.UnitX), ellipse, 20, out var hit));
        Assert.Equal(4f, hit.Distance, 5);
        Assert.Equal(new Vector2(-1, 0), hit.Point);
        Assert.Equal(-Vector2.UnitX, hit.Normal);
        Assert.False(RayIntersection2D.TryIntersect(new Ray2D(new(-5, 2), Vector2.UnitX), ellipse, 20, out _));

        var broad = new SpatialObject2D(new Ellipse2D(new(2000, 1)));
        Assert.True(RayIntersection2D.TryIntersect(new Ray2D(new(-3000, 0), Vector2.UnitX), broad, 2000, out var broadHit));
        Assert.Equal(1000f, broadHit.Distance, 3);
    }

    [Fact]
    public void PointAndShapeDistancesIncludeEllipse()
    {
        var ellipse = new Ellipse2D(new(2, 1));
        Assert.InRange(ShapeDistance2D.SignedDistance(new Vector2(3, 0), ellipse), .999f, 1.001f);
        Assert.InRange(ShapeDistance2D.SignedDistance(Vector2.Zero, ellipse), -1.001f, -.99f);
        var first = new SpatialObject2D(ellipse);
        var second = new SpatialObject2D(new Circle2D(1));
        second.Transform.Position = new(4, 0);
        Assert.InRange(ShapeDistance2D.Distance(first, second), .999f, 1.001f);
    }

    public static TheoryData<IShape2D> ConvexPartners =>
    [
        new Circle2D(.75f),
        new Capsule2D(new(0, -.5f), new(0, .5f), .5f),
        Rectangle2D.FromSize(new Vector2(1, 1)),
        new Triangle2D(new(-.5f, -.5f), new(.5f, -.5f), new(0, .5f)),
        new ConvexPolygon2D([new(-.5f, -.5f), new(.5f, -.5f), new(.5f, .5f), new(-.5f, .5f)]),
        new Ellipse2D(new(.75f, .5f))
    ];

    [Theory]
    [MemberData(nameof(ConvexPartners))]
    public void ContactWorksAgainstEveryFiniteConvexPartnerInEitherOrder(IShape2D partnerShape)
    {
        var ellipse = new SpatialObject2D(new Ellipse2D(new(2, 1)));
        var partner = new SpatialObject2D(partnerShape);
        partner.Transform.Position = new(1.7f, 0);
        Assert.True(ShapeCollision2D.TryGetContact(ellipse, partner, out var forward));
        Assert.True(ShapeCollision2D.TryGetContact(partner, ellipse, out var reverse));
        Assert.True(forward.PenetrationDepth > 0);
        Assert.InRange(MathF.Abs(forward.PenetrationDepth - reverse.PenetrationDepth), 0, .01f);
        Assert.InRange(Vector2.Distance(forward.Normal, -reverse.Normal), 0, .01f);
    }

    [Fact]
    public void HalfSpaceContactUsesExactSupportPoint()
    {
        var ellipse = new SpatialObject2D(new Ellipse2D(new(2, 1)));
        ellipse.Transform.Position = new(0, .5f);
        var ground = new SpatialObject2D(new HalfSpace2D(Vector2.UnitY, 0));
        Assert.True(ShapeCollision2D.TryGetContact(ellipse, ground, out var contact));
        Assert.Equal(Vector2.UnitY, contact.Normal);
        Assert.Equal(.5f, contact.PenetrationDepth, 5);
    }
}
