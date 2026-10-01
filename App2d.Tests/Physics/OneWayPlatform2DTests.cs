using App2d.Core;
using App2d.Core.Physics;
using App2d.Core.Shapes;
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

    [Fact]
    public void OneWaySurfaceNormalWorksForAWallAsWellAsAFloor()
    {
        var blockedWorld = CreateWorld();
        var blockedWall = AddOneWayPlatform(blockedWorld, Vector2.Zero, new Vector2(8f, 32f));
        blockedWall.OneWaySurfaceNormal = Vector2.UnitX;
        var blocked = AddDynamicBox(blockedWorld, new Vector2(7f, 0f), new Vector2(4f));
        blocked.LinearVelocity = new Vector2(-120f, 0f);

        blockedWorld.Step(1f / 60f);

        Assert.Equal(6f, blocked.WorldObject.Transform.Position.X, 3);
        Assert.Equal(0f, blocked.LinearVelocity.X, 3);
        Assert.Single(blockedWorld.LastContacts);

        var passWorld = CreateWorld();
        var passWall = AddOneWayPlatform(passWorld, Vector2.Zero, new Vector2(8f, 32f));
        passWall.OneWaySurfaceNormal = Vector2.UnitX;
        var passing = AddDynamicBox(passWorld, new Vector2(-7f, 0f), new Vector2(4f));
        passing.LinearVelocity = new Vector2(120f, 0f);

        passWorld.Step(1f / 60f);

        Assert.Equal(-5f, passing.WorldObject.Transform.Position.X, 3);
        Assert.Equal(120f, passing.LinearVelocity.X, 3);
        Assert.Empty(passWorld.LastContacts);
    }

    [Fact]
    public void BodyRestingOnOneWayFloorDoesNotBounce()
    {
        var world = CreateWorld();
        world.Gravity = new Vector2(0f, -100f);
        AddOneWayPlatform(world, Vector2.Zero, new Vector2(32f, 8f));
        var actor = AddDynamicBox(world, new Vector2(0f, 9f), new Vector2(10f));

        for (var frame = 0; frame < 120; frame++)
        {
            world.Step(1f / 60f);
            Assert.InRange(actor.LinearVelocity.Y, -0.001f, 0.001f);
        }

        Assert.InRange(actor.WorldObject.Transform.Position.Y, 8.999f, 9.001f);
        Assert.Single(world.LastContacts);
    }

    [Fact]
    public void GenericKinematicSurfaceCarriesContactButAllowsJump()
    {
        var world = CreateWorld();
        world.Gravity = new Vector2(0f, -100f);
        var surfaceObject = new SpatialObject2D(AxisAlignedRectangle2D.FromSize(new Vector2(32f, 8f)));
        var surface = world.AddBody(surfaceObject, BodyMotionType2D.Kinematic);
        surface.OneWaySurfaceNormal = Vector2.UnitY;
        surface.TransfersContactMotion = true;
        surface.Restitution = 0f;
        var actor = AddDynamicBox(world, new Vector2(0f, 6f), new Vector2(4f));
        const float dt = 1f / 60f;

        world.Step(dt);
        surface.LinearVelocity = new Vector2(30f, 0f);
        world.Step(dt);
        Assert.Equal(0.5f, actor.WorldObject.Transform.Position.X, 3);
        Assert.Equal(6f, actor.WorldObject.Transform.Position.Y, 3);

        actor.LinearVelocity = new Vector2(0f, 30f);
        world.Step(dt);
        Assert.True(actor.WorldObject.Transform.Position.Y > 6f);
        Assert.True(actor.LinearVelocity.Y > 0f);
    }

    [Fact]
    public void OneWayNormalCanFaceDiagonallyOnARotatedSurface()
    {
        var world = CreateWorld();
        var normal = Vector2.Normalize(new Vector2(-1f, 1f));
        var surfaceObject = new SpatialObject2D(AxisAlignedRectangle2D.FromSize(new Vector2(8f, 32f)));
        surfaceObject.Transform.Rotation = MathF.PI / 4f;
        var surface = world.AddBody(surfaceObject, BodyMotionType2D.Static);
        surface.OneWaySurfaceNormal = normal;
        surface.Restitution = 0f;
        var actorObject = new SpatialObject2D(new Circle2D(2f));
        actorObject.Transform.Position = normal * 20f;
        var actor = world.AddBody(actorObject, BodyMotionType2D.Dynamic);
        actor.Restitution = 0f;
        actor.LinearVelocity = -normal * 180f;

        world.Step(1f / 60f);

        Assert.InRange(Vector2.Dot(actorObject.Transform.Position, normal), 17.999f, 18.001f);
        Assert.Single(world.LastContacts);
        var checkpoint = world.CaptureSimulation();
        surface.OneWaySurfaceNormal = null;
        world.RestoreSimulation(checkpoint);
        Assert.InRange(Vector2.Distance(normal, surface.OneWaySurfaceNormal!.Value), 0f, 0.00001f);
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
