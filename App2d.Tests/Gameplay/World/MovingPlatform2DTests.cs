using App2d.Contracts.Combat;
using App2d.Contracts.Player;
using App2d.Core;
using App2d.Core.Collision;
using App2d.Core.Geometry;
using App2d.Core.Physics;
using App2d.Core.Shapes;
using App2d.Gameplay.Persons;
using App2d.Gameplay.World;
using App2d.Levels;
using System.Numerics;

namespace App2d.Tests.Gameplay.World;

public sealed class MovingPlatform2DTests
{
    private const uint WorldLayer = 1u << 0;
    private const uint PlayerLayer = 1u << 1;

    [Fact]
    public void PlatformReflectsAtPathEndsWithoutOvershooting()
    {
        var physics = CreatePhysics();
        var platform = CreatePlatform(physics, new Vector2(10f, 0f), speed: 4f);

        platform.Update(2f);
        physics.Step(2f);
        Assert.Equal(new Vector2(8.96f, 0f), platform.WorldObject.Transform.Position);

        platform.Update(1f);
        physics.Step(1f);
        Assert.Equal(new Vector2(8.96f, 0f), platform.WorldObject.Transform.Position);

        platform.Update(2f);
        physics.Step(2f);
        Assert.Equal(Vector2.Zero, platform.WorldObject.Transform.Position);
    }

    [Fact]
    public void PlatformCarriesSupportedDynamicBodyAlongItsPath()
    {
        var physics = CreatePhysics();
        physics.Gravity = new Vector2(0f, -100f);
        var platform = CreatePlatform(physics, new Vector2(20f, 0f), speed: 10f);
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
        var platformXBeforeCarry = platform.WorldObject.Transform.Position.X;
        platform.Update(0.1f);
        physics.Step(0.1f);

        var platformDisplacement = platform.WorldObject.Transform.Position.X - platformXBeforeCarry;
        Assert.Equal(riderXBeforeCarry + platformDisplacement, riderObject.Transform.Position.X, 4);
    }

    [Fact]
    public void StandingRiderStaysLevelThroughPlatformReversal()
    {
        var physics = CreatePhysics();
        physics.Gravity = new Vector2(0f, -100f);
        var platform = CreatePlatform(physics, new Vector2(20f, 0f), speed: 10f);
        var riderObject = new SpatialObject2D(AxisAlignedRectangle2D.FromSize(new Vector2(10f)));
        riderObject.Transform.Position = new Vector2(0f, 10f);
        var rider = physics.AddBody(riderObject, BodyMotionType2D.Dynamic);
        rider.Restitution = 0f;
        const float dt = 1f / 120f;

        for (var frame = 0; frame < 500; frame++)
        {
            platform.Update(dt);
            physics.Step(dt);
            Assert.InRange(riderObject.Transform.Position.Y, 9.999f, 10.001f);
            Assert.InRange(rider.LinearVelocity.Y, -0.001f, 0.001f);
        }

        // The first update has no prior support contact, so carry begins one tick later.
        Assert.InRange(MathF.Abs(riderObject.Transform.Position.X - platform.WorldObject.Transform.Position.X),
            0f, 10f * dt + 0.001f);
    }

    [Fact]
    public void StandingRiderStaysSupportedThroughVerticalReversal()
    {
        var physics = CreatePhysics();
        physics.Gravity = new Vector2(0f, -100f);
        var platform = CreatePlatform(physics, new Vector2(0f, 20f), speed: 10f);
        var riderObject = new SpatialObject2D(AxisAlignedRectangle2D.FromSize(new Vector2(10f)));
        riderObject.Transform.Position = new Vector2(0f, 10f);
        var rider = physics.AddBody(riderObject, BodyMotionType2D.Dynamic);
        rider.Restitution = 0f;
        const float dt = 1f / 120f;

        for (var frame = 0; frame < 500; frame++)
        {
            platform.Update(dt);
            physics.Step(dt);
            var gap = riderObject.WorldBounds.Bottom - platform.WorldObject.WorldBounds.Top;
            Assert.InRange(gap, -0.001f, 0.001f);
        }
    }

    [Fact]
    public void RisingPlatformKeepsRiderSupportedWithoutDoubleVerticalCarry()
    {
        var physics = CreatePhysics();
        physics.Gravity = new Vector2(0f, -100f);
        var platform = CreatePlatform(
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
        Assert.Equal(platform.Body.LinearVelocity.Y, rider.LinearVelocity.Y, 3);
        Assert.Contains(physics.LastContacts, contact =>
            ReferenceEquals(contact.First, platform.Body) ||
            ReferenceEquals(contact.Second, platform.Body));
    }

    [Fact]
    public void DescendingPlatformDoesNotRepeatedlyLandRider()
    {
        var collision = new CollisionSystem2D();
        var physics = CreatePhysics(collision);
        physics.Gravity = new Vector2(0f, -1_900f);
        physics.MaxSubstepSeconds = 1f / 120f;
        var platform = CreatePlatform(
            physics,
            new Vector2(0f, -100f),
            speed: 90f);
        var traversal = TraversalMetricsLoader2D.Load(TestAssetPath.Root);
        var rider = CreateRider(collision, physics, platform, traversal);
        const float deltaSeconds = 1f / 120f;

        StepPerson(rider, physics, deltaSeconds);
        Assert.True(rider.IsGrounded);

        var landingSpeeds = new List<float>();
        rider.Landed += landingSpeeds.Add;
        for (var frame = 0; frame < 20; frame++)
        {
            platform.Update(deltaSeconds);
            StepPerson(rider, physics, deltaSeconds);
        }

        Assert.True(rider.IsGrounded);
        Assert.Empty(landingSpeeds);
    }

    [Theory]
    [InlineData(80f)]
    [InlineData(-80f)]
    public void IdlePersonTracksVerticalPlatformThroughFullCycle(float travelY)
    {
        var collision = new CollisionSystem2D();
        var physics = CreatePhysics(collision);
        physics.Gravity = new Vector2(0f, -1_900f);
        physics.MaxSubstepSeconds = 1f / 120f;
        var platform = CreatePlatform(physics, new Vector2(0f, travelY), speed: 90f);
        var traversal = TraversalMetricsLoader2D.Load(TestAssetPath.Root);
        var rider = CreateRider(collision, physics, platform, traversal);
        const float dt = 1f / 120f;
        const int frames = 220;

        StepPerson(rider, physics, dt); // Establish the support contact before motion begins.

        for (var frame = 0; frame < frames; frame++)
        {
            platform.Update(dt);
            StepPerson(rider, physics, dt);
            var gap = rider.Body.WorldObject.WorldBounds.Bottom - platform.WorldObject.WorldBounds.Top;
            Assert.InRange(gap, -0.05f, 0.05f);
        }
    }

    [Fact]
    public void LandingSpeedIsRelativeToDescendingPlatform()
    {
        var collision = new CollisionSystem2D();
        var physics = CreatePhysics(collision);
        var platform = CreatePlatform(
            physics,
            new Vector2(0f, -100f),
            speed: 90f);
        var traversal = TraversalMetricsLoader2D.Load(TestAssetPath.Root);
        var rider = CreateRider(
            collision,
            physics,
            platform,
            traversal,
            gap: 2.5f);
        rider.Body.LinearVelocity = new Vector2(0f, -300f);
        var landingSpeeds = new List<float>();
        rider.Landed += landingSpeeds.Add;
        const float deltaSeconds = 1f / 60f;

        platform.Update(deltaSeconds);
        StepPerson(rider, physics, deltaSeconds);

        var landingSpeed = Assert.Single(landingSpeeds);
        Assert.Equal(300f + platform.Body.LinearVelocity.Y, landingSpeed, 3);
    }

    [Theory]
    [InlineData(60f, 0f)]
    [InlineData(0f, -90f)]
    public void RiderReportsTheVelocityOfTheGroundItStandsOn(float x, float y)
    {
        var collision = new CollisionSystem2D();
        var physics = CreatePhysics(collision);
        physics.Gravity = new Vector2(0f, -1_900f);
        physics.MaxSubstepSeconds = 1f / 120f;
        var velocity = new Vector2(x, y);
        var platform = CreatePlatform(physics, Vector2.Normalize(velocity) * 100f, speed: velocity.Length());
        var traversal = TraversalMetricsLoader2D.Load(TestAssetPath.Root);
        var rider = CreateRider(collision, physics, platform, traversal);
        const float deltaSeconds = 1f / 120f;
        for (var frame = 0; frame < 10; frame++)
        {
            platform.Update(deltaSeconds);
            StepPerson(rider, physics, deltaSeconds);
        }

        var state = rider.CaptureState();
        Assert.True(state.IsGrounded);
        Assert.Equal(platform.Body.LinearVelocity.X, state.GroundVelocity.X, 2);
        Assert.Equal(platform.Body.LinearVelocity.Y, state.GroundVelocity.Y, 2);
    }

    [Fact]
    public void PlatformUsesKinematicOneWayCollision()
    {
        var physics = CreatePhysics();

        var platform = CreatePlatform(physics, Vector2.UnitY, speed: 1f);

        Assert.Equal(BodyMotionType2D.Kinematic, platform.Body.MotionType);
        Assert.True(platform.Body.IsOneWayPlatform);
        Assert.Equal(Vector2.Zero, platform.Start);
        Assert.Equal(Vector2.UnitY, platform.End);
    }

    private static PhysicsWorld2D CreatePhysics() =>
        CreatePhysics(new CollisionSystem2D());

    private static PhysicsWorld2D CreatePhysics(CollisionSystem2D collision) =>
        new(collision)
        {
            Gravity = Vector2.Zero,
            MaxSubstepSeconds = 2f,
            PositionIterations = 3,
            VelocityIterations = 1
        };

    private static Person2D CreateRider(
        CollisionSystem2D collision,
        PhysicsWorld2D physics,
        MovingPlatform2D platform,
        TraversalMetrics2D traversal,
        float gap = 0f)
    {
        var position = new Vector2(
            platform.WorldObject.Transform.Position.X,
            platform.WorldObject.WorldBounds.Top +
            traversal.PlayerColliderSize.Y * 0.5f +
            gap);
        return new Person2D(EntityId2D.Create(),
            collision,
            physics,
            traversal,
            position,
            PlayerLayer,
            WorldLayer,
            CombatFaction2D.Player);
    }

    private static void StepPerson(
        Person2D person,
        PhysicsWorld2D physics,
        float deltaSeconds)
    {
        person.BeginFrame(deltaSeconds);
        person.ApplyCommand(default, deltaSeconds);
        physics.Step(deltaSeconds);
        person.UpdateAfterPhysics(deltaSeconds);
    }

    private static MovingPlatform2D CreatePlatform(
        PhysicsWorld2D physics,
        Vector2 travel,
        float speed) =>
        new(
            EntityId2D.Create(),
            physics,
            Vector2.Zero,
            travel,
            new Size2D(40f, 10f),
            speed,
            collisionLayer: WorldLayer,
            collisionMask: uint.MaxValue);
}
