using App2d.Core;
using App2d.Core.Geometry;
using App2d.Physics;
using System.Numerics;

namespace App2d.Tests.Physics;

public sealed class OneWayPlatform2DTests
{
    [Fact]
    public void VerticalEdgeDoesNotBlockHorizontalMovement()
    {
        var world = CreateWorld();
        AddOneWayPlatform(world, Vector2.Zero, new Vector2(32f, 8f));
        var actor = AddDynamicBox(
            world,
            new Vector2(-21f, 0f),
            new Vector2(10f, 24f));
        actor.LinearVelocity = new Vector2(60f, 0f);

        world.Step(1f / 60f);

        Assert.Equal(-20f, actor.WorldObject.Transform.Position.X, 3);
        Assert.Equal(60f, actor.LinearVelocity.X, 3);
        Assert.Empty(world.LastContacts);
    }

    [Fact]
    public void RoundedBodyDoesNotCatchOnTopCornerWhileMovingPastVerticalEdge()
    {
        var world = CreateWorld();
        AddOneWayPlatform(world, Vector2.Zero, new Vector2(32f, 8f));
        var actorObject = new SpatialObject2D(new Circle2D(5f));
        actorObject.Transform.Position = new Vector2(-18f, 8.5f);
        var actor = world.AddBody(actorObject, BodyMotionType2D.Dynamic);
        actor.Restitution = 0f;
        actor.LinearVelocity = new Vector2(60f, 0f);

        world.Step(1f / 60f);

        Assert.Equal(new Vector2(-17f, 8.5f), actorObject.Transform.Position);
        Assert.Equal(new Vector2(60f, 0f), actor.LinearVelocity);
        Assert.Empty(world.LastContacts);
    }

    [Fact]
    public void RoundedBodyStillLandsOnTopSurface()
    {
        var world = CreateWorld();
        AddOneWayPlatform(world, Vector2.Zero, new Vector2(32f, 8f));
        var actorObject = new SpatialObject2D(new Circle2D(5f));
        actorObject.Transform.Position = new Vector2(0f, 10f);
        var actor = world.AddBody(actorObject, BodyMotionType2D.Dynamic);
        actor.Restitution = 0f;
        actor.LinearVelocity = new Vector2(0f, -120f);

        world.Step(1f / 60f);

        Assert.Equal(9f, actorObject.Transform.Position.Y, 3);
        Assert.Equal(0f, actor.LinearVelocity.Y, 3);
        Assert.Single(world.LastContacts);
    }

    [Fact]
    public void RoundedBodyStillPassesUpThroughPlatform()
    {
        var world = CreateWorld();
        AddOneWayPlatform(world, Vector2.Zero, new Vector2(32f, 8f));
        var actorObject = new SpatialObject2D(new Circle2D(5f));
        actorObject.Transform.Position = new Vector2(0f, -10f);
        var actor = world.AddBody(actorObject, BodyMotionType2D.Dynamic);
        actor.Restitution = 0f;
        actor.LinearVelocity = new Vector2(0f, 120f);

        world.Step(1f / 60f);

        Assert.Equal(-8f, actorObject.Transform.Position.Y, 3);
        Assert.Equal(120f, actor.LinearVelocity.Y, 3);
        Assert.Empty(world.LastContacts);
    }

    private static PhysicsWorld2D CreateWorld() =>
        new()
        {
            Gravity = Vector2.Zero,
            MaxSubstepSeconds = 1f / 60f,
            PositionIterations = 4,
            VelocityIterations = 1
        };

    private static PhysicsBody2D AddOneWayPlatform(
        PhysicsWorld2D world,
        Vector2 position,
        Vector2 size)
    {
        var platformObject = new SpatialObject2D(
            AxisAlignedRectangle2D.FromSize(size));
        platformObject.Transform.Position = position;
        var platform = world.AddBody(
            platformObject,
            BodyMotionType2D.Static);
        platform.IsOneWayPlatform = true;
        platform.Restitution = 0f;
        return platform;
    }

    private static PhysicsBody2D AddDynamicBox(
        PhysicsWorld2D world,
        Vector2 position,
        Vector2 size)
    {
        var actorObject = new SpatialObject2D(
            AxisAlignedRectangle2D.FromSize(size));
        actorObject.Transform.Position = position;
        var actor = world.AddBody(
            actorObject,
            BodyMotionType2D.Dynamic);
        actor.Restitution = 0f;
        return actor;
    }
}
