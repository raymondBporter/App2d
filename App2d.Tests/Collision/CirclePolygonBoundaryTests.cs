using App2d.Core.Collision.Contacts;
using App2d.Core.Mathematics.Transforms;
using App2d.Core.Physics;
using App2d.Core.Shapes;
using App2d.Core.Spatial;
using System.Numerics;

namespace App2d.Tests.Collision;

public sealed class CirclePolygonBoundaryTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CollinearSurfaceVerticesProduceUpwardNormalsInEitherWinding(bool reverse)
    {
        Vector2[] vertices = [new(-10, 0), new(-2, 0), new(0, 0), new(2, 0), new(10, 0), new(10, -10), new(-10, -10)];
        if (reverse) Array.Reverse(vertices);
        var polygon = SimplePolygon2D.Builder.Create(vertices);
        var wheel = Circle2D.Builder.Create(1f);
        foreach (var x in new[] { -2.01f, -2f, -1.99f, 0f, 1.99f, 2f, 2.01f })
        {
            var pose = Similarity2D.FromTranslation(new(x, 0.75f));
            Assert.True(ShapeCollision2D.TryGetContact(wheel, pose, polygon, Similarity2D.Identity, out var contact));
            Assert.InRange(Vector2.Distance(Vector2.UnitY, contact.Normal), 0f, 0.00001f);
            Assert.Equal(0.25f, contact.PenetrationDepth, 4);
            Assert.Equal(0f, contact.Point.Y, 4);
        }
    }

    [Fact]
    public void ConcaveValleyStaysEmptyInsteadOfCollidingWithItsConvexHull()
    {
        var polygon = SimplePolygon2D.Builder.Create([new(-10, 4), new(0, 0), new(10, 4), new(10, -10), new(-10, -10)]);
        var wheel = Circle2D.Builder.Create(0.5f);
        Assert.False(ShapeCollision2D.TryGetContact(wheel, Similarity2D.FromTranslation(new(0, 3)), polygon, Similarity2D.Identity, out _));
        Assert.True(ShapeCollision2D.TryGetContact(wheel, Similarity2D.FromTranslation(new(0, 0.25f)), polygon, Similarity2D.Identity, out var contact));
        Assert.True(contact.Normal.Y > 0.9f);
    }

    [Fact]
    public void InsideCenterExitsThroughTheBoundaryAndTouchingDoesNotPenetrate()
    {
        var polygon = SimplePolygon2D.Builder.Create([new(-10, 0), new(10, 0), new(10, -10), new(-10, -10)]);
        var wheel = Circle2D.Builder.Create(1f);
        Assert.False(ShapeCollision2D.TryGetContact(wheel, Similarity2D.FromTranslation(new(0, 1)), polygon, Similarity2D.Identity, out _));
        Assert.True(ShapeCollision2D.TryGetContact(wheel, Similarity2D.FromTranslation(new(0, -0.5f)), polygon, Similarity2D.Identity, out var contact));
        Assert.Equal(Vector2.UnitY, contact.Normal);
        Assert.Equal(1.5f, contact.PenetrationDepth, 4);
        Assert.True(ShapeCollision2D.TryGetContact(wheel, Similarity2D.Identity, polygon, Similarity2D.Identity, out var onEdge));
        Assert.Equal(Vector2.UnitY, onEdge.Normal);
        Assert.Equal(1f, onEdge.PenetrationDepth, 4);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BoundaryContactRespectsRotationScaleMirrorAndArgumentOrder(bool mirrored)
    {
        var polygon = SimplePolygon2D.Builder.Create([new(-10, 0), new(10, 0), new(10, -10), new(-10, -10)]);
        var polygonPose = Similarity2D.FromAxis(new(20, -13), new(0.8f, 0.6f), mirror: mirrored, scale: 2f);
        var wheel = Circle2D.Builder.Create(0.5f, new(2, 3));
        var worldCenter = polygonPose.TransformPoint(new(1f, 0.375f));
        var wheelPose = Similarity2D.FromTranslation(worldCenter - wheel.Center);
        Assert.False(ShapeCollision2D.TryGetContact(wheel, wheelPose, polygon, polygonPose, out _));
        wheelPose = wheelPose.Translated(-polygonPose.YAxis * 0.25f);
        Assert.True(ShapeCollision2D.TryGetContact(wheel, wheelPose, polygon, polygonPose, out var forward));
        Assert.True(ShapeCollision2D.TryGetContact(polygon, polygonPose, wheel, wheelPose, out var reverse));
        Assert.InRange(Vector2.Distance(Vector2.Normalize(polygonPose.YAxis), forward.Normal), 0f, 0.00001f);
        Assert.Equal(0.25f, forward.PenetrationDepth, 4);
        Assert.Equal(forward.PenetrationDepth, reverse.PenetrationDepth);
        Assert.Equal(-forward.Normal, reverse.Normal);
    }

    [Fact]
    public void RollingWheelCrossesSurfaceVerticesWithoutLosingHorizontalSpeed()
    {
        var world = new PhysicsWorld2D { Gravity = new(0f, -10f) };
        var polygon = SimplePolygon2D.Builder.Create([new(-20, 0), new(-2, 0), new(0, 0), new(2, 0), new(20, 0), new(20, -10), new(-20, -10)]);
        var ground = world.AddBody(new SpatialObject2D(polygon), BodyMotionType2D.Static);
        ground.Friction = 1f;
        ground.Restitution = 0f;
        var wheel = world.AddBody(new SpatialObject2D(Circle2D.Builder.Create(1f)), BodyMotionType2D.Dynamic);
        wheel.SetMassFromShape(1f);
        wheel.WorldObject.Transform.Position = new(-5f, 1f);
        wheel.LinearVelocity = new(4f, 0f);
        wheel.AngularVelocity = -4f;
        wheel.FreezeRotation = false;
        wheel.Friction = 1f;
        wheel.Restitution = 0f;
        for (var step = 0; step < 300; step++) world.Step(1f / 120f);
        Assert.Equal(5f, wheel.WorldCenterOfMass.X, 3);
        Assert.Equal(4f, wheel.LinearVelocity.X, 4);
        Assert.Equal(-4f, wheel.AngularVelocity, 4);
        Assert.Equal(1f, wheel.WorldCenterOfMass.Y, 4);
    }
}
