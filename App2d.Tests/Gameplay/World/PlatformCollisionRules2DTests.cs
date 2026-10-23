using App2d.Core.Collision.Contacts;
using App2d.Core.Collision.Filtering;
using App2d.Core.Physics;
using App2d.Core.Shapes;
using App2d.Core.Spatial;
using App2d.Gameplay.World;
using System.Numerics;

namespace App2d.Tests.Gameplay.World;

public sealed class PlatformCollisionRules2DTests
{
    [Fact]
    public void DroppingExcludesOnlyThatPairInBothOrdersAndCancelsMovingSurfaceCarry()
    {
        var world = new PhysicsWorld2D { Gravity = new(0f, -100f), MaxSubstepSeconds = 1f / 120f };
        var rules = PlatformCollisionRules2D.Attach(world);
        var platform = AddPlatform(world, BodyMotionType2D.Kinematic);
        platform.TransfersContactMotion = true;
        var dropping = AddActor(world, -5f);
        var staying = AddActor(world, 5f);
        world.Step(1f / 60f);
        rules.IgnorePlatform(dropping, platform);
        Assert.False(world.CanCollide(dropping, platform));
        Assert.False(world.CanCollide(platform, dropping));
        Assert.True(world.CanCollide(staying, platform));
        Assert.True(world.CanCollide(platform, staying));
        var originalMask = dropping.CollisionMask;
        dropping.LinearVelocity = new(0f, -20f);
        platform.LinearVelocity = new(30f, 0f);
        world.Step(1f / 60f);
        Assert.Equal(-5f, dropping.WorldObject.Transform.Position.X, 4);
        Assert.Equal(5.5f, staying.WorldObject.Transform.Position.X, 4);
        Assert.True(dropping.WorldObject.Transform.Position.Y < 1f);
        Assert.Equal(originalMask, dropping.CollisionMask);
        Assert.DoesNotContain(world.LastContacts, c => c.First == dropping || c.Second == dropping);
    }

    [Fact]
    public void IgnoredPairsSkipNarrowPhaseAndRemovalCleansTheirState()
    {
        var world = new PhysicsWorld2D();
        var rules = PlatformCollisionRules2D.Attach(world);
        var platform = AddPlatform(world);
        var actor = AddActor(world, 0f);
        rules.IgnorePlatform(actor, platform);
        world.Step(1f / 60f);
        Assert.Equal(0, world.CollisionSystem.LastNarrowPhaseTestCount);
        Assert.Empty(world.LastContacts);
        Assert.Equal(1, rules.IgnoredPlatformCount(actor));
        world.RemoveBody(platform);
        Assert.Equal(0, rules.IgnoredPlatformCount(actor));
        var replacement = AddPlatform(world);
        Assert.True(world.CanCollide(actor, replacement));
        rules.IgnorePlatform(actor, replacement);
        world.RemoveBody(actor);
        Assert.Equal(0, rules.IgnoredPlatformCount(actor));
    }

    [Fact]
    public void OneWayDecisionUsesTheSameNormalConventionInEitherBodyOrder()
    {
        var world = new PhysicsWorld2D();
        var rules = PlatformCollisionRules2D.Attach(world);
        var platform = AddPlatform(world);
        var actor = AddActor(world, 0f);
        actor.LinearVelocity = -Vector2.UnitY;
        var geometry = new CollisionContact2D(Vector2.Zero, Vector2.UnitY, 0f);
        Assert.True(rules.OnBeforeCollision(new(actor, platform, geometry)));
        Assert.True(rules.OnBeforeCollision(new(platform, actor,
            new CollisionContact2D(geometry.Point, -geometry.Normal, geometry.PenetrationDepth))));
        actor.LinearVelocity = Vector2.UnitY;
        Assert.False(rules.OnBeforeCollision(new(actor, platform, geometry)));
        Assert.False(rules.OnBeforeCollision(new(platform, actor,
            new CollisionContact2D(geometry.Point, -geometry.Normal, geometry.PenetrationDepth))));
    }

    [Fact]
    public void InstallingGameRulesPreservesExistingPairPolicyAndBothCallbacks()
    {
        var world = new PhysicsWorld2D();
        var policy = new ExistingPolicy();
        world.PairFilter = policy;
        world.CollisionCallbacks = policy;
        var rules = PlatformCollisionRules2D.Attach(world);
        Assert.Same(rules, PlatformCollisionRules2D.Attach(world));
        var platform = AddPlatform(world);
        var actor = AddActor(world, 0f);
        actor.WorldObject.Transform.Position = new(0f, 0.8f);
        policy.TestPairs = false;
        Assert.False(world.CanCollide(actor, platform));
        world.Step(1f / 60f);
        Assert.Equal(0, policy.BeforeCount);
        policy.TestPairs = true;
        policy.AcceptContacts = false;
        world.Step(1f / 60f);
        Assert.True(policy.BeforeCount > 0);
        Assert.Equal(0, policy.AfterCount);
        Assert.Empty(world.LastContacts);
        policy.AcceptContacts = true;
        world.Step(1f / 60f);
        Assert.Equal(1, policy.AfterCount);
    }

    private static RigidBody2D AddPlatform(PhysicsWorld2D world, BodyMotionType2D motion = BodyMotionType2D.Static)
    {
        var spatial = new SpatialObject2D(Rectangle2D.Builder.CreateFromSize(40f, 2f));
        spatial.Transform.Position = new(0f, -1f);
        var platform = world.AddBody(spatial, motion);
        platform.Restitution = 0f;
        PlatformCollisionRules2D.Attach(world).Surface(platform).OneWaySurfaceNormal = Vector2.UnitY;
        return platform;
    }

    private static RigidBody2D AddActor(PhysicsWorld2D world, float x)
    {
        var spatial = new SpatialObject2D(Circle2D.Builder.Create(1f));
        spatial.Transform.Position = new(x, 1f);
        var actor = world.AddBody(spatial, BodyMotionType2D.Dynamic);
        actor.Restitution = 0f;
        return actor;
    }

    private sealed class ExistingPolicy : IPairFilter2D<RigidBody2D>, IPhysicsCollisionCallbacks2D
    {
        public bool TestPairs { get; set; } = true;
        public bool AcceptContacts { get; set; } = true;
        public int BeforeCount { get; private set; }
        public int AfterCount { get; private set; }
        public bool ShouldTest(RigidBody2D first, RigidBody2D second) => TestPairs && first.CanCollideWith(second);
        public bool OnBeforeCollision(PhysicsContact2D contact) { BeforeCount++; return AcceptContacts; }
        public void OnAfterCollision(PhysicsContact2D contact) => AfterCount++;
    }
}
