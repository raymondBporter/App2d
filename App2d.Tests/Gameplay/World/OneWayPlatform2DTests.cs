using App2d.Core.Physics;
using App2d.Core.Shapes;
using App2d.Core.Spatial;
using App2d.Gameplay.World;

namespace App2d.Tests.Gameplay.World;

public sealed class OneWayPlatform2DTests
{
    [Fact]
    public void VerticalEdgeDoesNotBlockHorizontalMovement()
    {
        var world = CreateWorld();
        AddOneWayPlatform(world, Vector2f.Zero, new Vector2f(32f, 8f));
        var actor = AddDynamicBox(
            world,
            new Vector2f(-21f, 0f),
            new Vector2f(10f, 24f));
        actor.LinearVelocity = new Vector2f(60f, 0f);

        world.Step(1f / 60f);

        Assert.Equal(-20f, actor.WorldObject.Position.X, 3);
        Assert.Equal(60f, actor.LinearVelocity.X, 3);
        Assert.Empty(world.LastContacts);
    }

    [Fact]
    public void RoundedBodyDoesNotCatchOnTopCornerWhileMovingPastVerticalEdge()
    {
        var world = CreateWorld();
        AddOneWayPlatform(world, Vector2f.Zero, new Vector2f(32f, 8f));
        var actorObject = new SpatialObject2D(Circle2D.Builder.Create(5f))
        {
            Position = new Vector2f(-18f, 8.5f)
        };
        var actor = world.AddBody(actorObject, BodyMotionType2D.Dynamic);
        actor.Restitution = 0f;
        actor.LinearVelocity = new Vector2f(60f, 0f);

        world.Step(1f / 60f);

        Assert.Equal(new Vector2f(-17f, 8.5f), actorObject.Position);
        Assert.Equal(new Vector2f(60f, 0f), actor.LinearVelocity);
        Assert.Empty(world.LastContacts);
    }

    [Fact]
    public void RoundedBodyStillLandsOnTopSurface()
    {
        var world = CreateWorld();
        AddOneWayPlatform(world, Vector2f.Zero, new Vector2f(32f, 8f));
        var actorObject = new SpatialObject2D(Circle2D.Builder.Create(5f))
        {
            Position = new Vector2f(0f, 10f)
        };
        var actor = world.AddBody(actorObject, BodyMotionType2D.Dynamic);
        actor.Restitution = 0f;
        actor.LinearVelocity = new Vector2f(0f, -120f);

        world.Step(1f / 60f);

        Assert.Equal(9f, actorObject.Position.Y, 3);
        Assert.Equal(0f, actor.LinearVelocity.Y, 3);
        Assert.Single(world.LastContacts);
    }

    [Fact]
    public void RoundedBodyStillPassesUpThroughPlatform()
    {
        var world = CreateWorld();
        AddOneWayPlatform(world, Vector2f.Zero, new Vector2f(32f, 8f));
        var actorObject = new SpatialObject2D(Circle2D.Builder.Create(5f))
        {
            Position = new Vector2f(0f, -10f)
        };
        var actor = world.AddBody(actorObject, BodyMotionType2D.Dynamic);
        actor.Restitution = 0f;
        actor.LinearVelocity = new Vector2f(0f, 120f);

        world.Step(1f / 60f);

        Assert.Equal(-8f, actorObject.Position.Y, 3);
        Assert.Equal(120f, actor.LinearVelocity.Y, 3);
        Assert.Empty(world.LastContacts);
    }

    [Fact]
    public void OneWaySurfaceNormalWorksForAWallAsWellAsAFloor()
    {
        var blockedWorld = CreateWorld();
        var blockedWall = AddOneWayPlatform(blockedWorld, Vector2f.Zero, new Vector2f(8f, 32f));
        PlatformCollisionRules2D.Attach(blockedWorld).Surface(blockedWall).OneWaySurfaceNormal = Vector2f.UnitX;
        var blocked = AddDynamicBox(blockedWorld, new Vector2f(7f, 0f), new Vector2f(4f));
        blocked.LinearVelocity = new Vector2f(-120f, 0f);

        blockedWorld.Step(1f / 60f);

        Assert.Equal(6f, blocked.WorldObject.Position.X, 3);
        Assert.Equal(0f, blocked.LinearVelocity.X, 3);
        Assert.Single(blockedWorld.LastContacts);

        var passWorld = CreateWorld();
        var passWall = AddOneWayPlatform(passWorld, Vector2f.Zero, new Vector2f(8f, 32f));
        PlatformCollisionRules2D.Attach(passWorld).Surface(passWall).OneWaySurfaceNormal = Vector2f.UnitX;
        var passing = AddDynamicBox(passWorld, new Vector2f(-7f, 0f), new Vector2f(4f));
        passing.LinearVelocity = new Vector2f(120f, 0f);

        passWorld.Step(1f / 60f);

        Assert.Equal(-5f, passing.WorldObject.Position.X, 3);
        Assert.Equal(120f, passing.LinearVelocity.X, 3);
        Assert.Empty(passWorld.LastContacts);
    }

    [Fact]
    public void BodyRestingOnOneWayFloorDoesNotBounce()
    {
        var world = CreateWorld();
        world.Gravity = new Vector2f(0f, -100f);
        AddOneWayPlatform(world, Vector2f.Zero, new Vector2f(32f, 8f));
        var actor = AddDynamicBox(world, new Vector2f(0f, 9f), new Vector2f(10f));

        for (var frame = 0; frame < 120; frame++)
        {
            world.Step(1f / 60f);
            Assert.InRange(actor.LinearVelocity.Y, -0.001f, 0.001f);
        }

        Assert.InRange(actor.WorldObject.Position.Y, 8.999f, 9.001f);
        Assert.Single(world.LastContacts);
    }

    [Fact]
    public void GenericKinematicSurfaceCarriesContactButAllowsJump()
    {
        var world = CreateWorld();
        world.Gravity = new Vector2f(0f, -100f);
        var surfaceObject = new SpatialObject2D(AxisAlignedRectangle2D.Builder.CreateFromSize(32f, 8f));
        var surface = world.AddBody(surfaceObject, BodyMotionType2D.Kinematic);
        PlatformCollisionRules2D.Attach(world).Surface(surface).OneWaySurfaceNormal = Vector2f.UnitY;
        surface.TransfersContactMotion = true;
        surface.Restitution = 0f;
        var actor = AddDynamicBox(world, new Vector2f(0f, 6f), new Vector2f(4f));
        const float dt = 1f / 60f;

        world.Step(dt);
        surface.LinearVelocity = new Vector2f(30f, 0f);
        world.Step(dt);
        Assert.Equal(0.5f, actor.WorldObject.Position.X, 3);
        Assert.Equal(6f, actor.WorldObject.Position.Y, 3);

        actor.LinearVelocity = new Vector2f(0f, 30f);
        world.Step(dt);
        Assert.True(actor.WorldObject.Position.Y > 6f);
        Assert.True(actor.LinearVelocity.Y > 0f);
    }

    [Fact]
    public void OneWayNormalCanFaceDiagonallyOnARotatedSurface()
    {
        var world = CreateWorld();
        var normal = Vector2f.Normalize(new Vector2f(-1f, 1f));
        var surfaceObject = new SpatialObject2D(AxisAlignedRectangle2D.Builder.CreateFromSize(8f, 32f))
        {
            Rotation = MathF.PI / 4f
        };
        var surface = world.AddBody(surfaceObject, BodyMotionType2D.Static);
        PlatformCollisionRules2D.Attach(world).Surface(surface).OneWaySurfaceNormal = normal;
        surface.Restitution = 0f;
        var actorObject = new SpatialObject2D(Circle2D.Builder.Create(2f))
        {
            Position = normal * 20f
        };
        var actor = world.AddBody(actorObject, BodyMotionType2D.Dynamic);
        actor.Restitution = 0f;
        actor.LinearVelocity = -normal * 180f;

        world.Step(1f / 60f);

        Assert.InRange(Vector2f.Dot(actorObject.Position, normal), 17.999f, 18.001f);
        Assert.Single(world.LastContacts);
    }

    private static PhysicsWorld2D CreateWorld() =>
        new()
        {
            Gravity = Vector2f.Zero,
            MaxSubstepSeconds = 1f / 60f,
            PositionIterations = 4,
            VelocityIterations = 1
        };

    private static RigidBody2D AddOneWayPlatform(
        PhysicsWorld2D world,
        Vector2f position,
        Vector2f size)
    {
        var platformObject = new SpatialObject2D(
            AxisAlignedRectangle2D.Builder.CreateFromSize(size))
        {
            Position = position
        };
        var platform = world.AddBody(
            platformObject,
            BodyMotionType2D.Static);
        PlatformCollisionRules2D.Attach(world).Surface(platform).OneWaySurfaceNormal = Vector2f.UnitY;
        platform.Restitution = 0f;
        return platform;
    }

    private static RigidBody2D AddDynamicBox(
        PhysicsWorld2D world,
        Vector2f position,
        Vector2f size)
    {
        var actorObject = new SpatialObject2D(
            AxisAlignedRectangle2D.Builder.CreateFromSize(size))
        {
            Position = position
        };
        var actor = world.AddBody(
            actorObject,
            BodyMotionType2D.Dynamic);
        actor.Restitution = 0f;
        return actor;
    }
}
