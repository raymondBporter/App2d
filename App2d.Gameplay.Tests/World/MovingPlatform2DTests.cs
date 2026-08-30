using System.Numerics;
using App2d.Collision;
using App2d.Core;
using App2d.Core.Geometry;
using App2d.Gameplay.World;
using App2d.Physics;
using App2d.Rendering;
using SkiaSharp;
using Xunit;

namespace App2d.Gameplay.Tests.World;

public sealed class MovingPlatform2DTests
{
    [Fact]
    public void PlatformReflectsAtPathEndsWithoutOvershooting()
    {
        var scene = new Scene2D();
        var physics = CreatePhysics();
        var platform = CreatePlatform(scene, physics, new Vector2(10f, 0f), speed: 4f);

        platform.Update(2f);
        physics.Step(2f);
        Assert.Equal(new Vector2(8f, 0f), platform.WorldObject.Transform.Position);

        platform.Update(1f);
        physics.Step(1f);
        Assert.Equal(new Vector2(8f, 0f), platform.WorldObject.Transform.Position);

        platform.Update(2f);
        physics.Step(2f);
        Assert.Equal(Vector2.Zero, platform.WorldObject.Transform.Position);
    }

    [Fact]
    public void PlatformCarriesSupportedDynamicBodyAlongItsPath()
    {
        var scene = new Scene2D();
        var physics = CreatePhysics();
        physics.Gravity = new Vector2(0f, -100f);
        var platform = CreatePlatform(scene, physics, new Vector2(20f, 0f), speed: 10f);
        var riderObject = new SpatialObject2D(AxisAlignedRectangle2D.FromSize(new Vector2(10f)));
        riderObject.Transform.Position = new Vector2(0f, 10f);
        var rider = physics.AddBody(riderObject, BodyMotionType2D.Dynamic);
        rider.Restitution = 0f;

        platform.Update(0.1f);
        physics.Step(0.1f);
        Assert.Contains(physics.LastContacts, contact =>
            ReferenceEquals(contact.First, platform.Body) ||
            ReferenceEquals(contact.Second, platform.Body));

        var riderXBeforeCarry = riderObject.Transform.Position.X;
        platform.Update(0.1f);

        Assert.Equal(riderXBeforeCarry + 1f, riderObject.Transform.Position.X, 4);
    }

    [Fact]
    public void RisingPlatformKeepsRiderSupportedWithoutDoubleVerticalCarry()
    {
        var scene = new Scene2D();
        var physics = CreatePhysics();
        physics.Gravity = new Vector2(0f, -100f);
        var platform = CreatePlatform(
            scene,
            physics,
            new Vector2(0f, 20f),
            speed: 10f);
        var riderObject = new SpatialObject2D(
            AxisAlignedRectangle2D.FromSize(new Vector2(10f)));
        riderObject.Transform.Position = new Vector2(0f, 10f);
        var rider = physics.AddBody(riderObject, BodyMotionType2D.Dynamic);
        rider.Restitution = 0f;

        platform.Update(0.1f);
        physics.Step(0.1f);
        var firstGap = riderObject.WorldBounds.Bottom -
            platform.WorldObject.WorldBounds.Top;

        platform.Update(0.1f);
        physics.Step(0.1f);
        var secondGap = riderObject.WorldBounds.Bottom -
            platform.WorldObject.WorldBounds.Top;

        Assert.InRange(MathF.Abs(firstGap), 0f, 0.001f);
        Assert.InRange(MathF.Abs(secondGap), 0f, 0.001f);
        Assert.Equal(10f, rider.LinearVelocity.Y, 3);
        Assert.Contains(physics.LastContacts, contact =>
            ReferenceEquals(contact.First, platform.Body) ||
            ReferenceEquals(contact.Second, platform.Body));
    }

    [Fact]
    public void PlatformUsesKinematicOneWayCollision()
    {
        var scene = new Scene2D();
        var physics = CreatePhysics();

        var platform = CreatePlatform(scene, physics, Vector2.UnitY, speed: 1f);

        Assert.Equal(BodyMotionType2D.Kinematic, platform.Body.MotionType);
        Assert.True(platform.Body.IsOneWayPlatform);
        Assert.Equal(Vector2.Zero, platform.Start);
        Assert.Equal(Vector2.UnitY, platform.End);
    }

    private static PhysicsWorld2D CreatePhysics() =>
        new(new CollisionSystem2D())
        {
            Gravity = Vector2.Zero,
            MaxSubstepSeconds = 2f,
            PositionIterations = 3,
            VelocityIterations = 1
        };

    private static MovingPlatform2D CreatePlatform(
        Scene2D scene,
        PhysicsWorld2D physics,
        Vector2 travel,
        float speed) =>
        new(
            scene,
            physics,
            Vector2.Zero,
            travel,
            new Vector2(40f, 10f),
            speed,
            collisionLayer: 1u,
            collisionMask: uint.MaxValue,
            new SKColor(37, 210, 190));
}
