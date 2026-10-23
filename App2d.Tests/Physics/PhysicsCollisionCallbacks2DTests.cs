using App2d.Core.Collision;
using App2d.Core.Collision.Filtering;
using App2d.Core.Physics;
using App2d.Core.Physics.Filtering;
using App2d.Core.Shapes;
using App2d.Core.Spatial;
using System.Numerics;

namespace App2d.Tests.Physics;

public sealed class PhysicsCollisionCallbacks2DTests
{
    [Fact]
    public void BeforeCallbackCanRejectResponseAndContactReporting()
    {
        var (world, actor, _) = CreateWorld();
        var callbacks = new Callbacks { Accept = false };
        world.CollisionCallbacks = callbacks;
        actor.LinearVelocity = new(0f, -6f);
        var before = actor.WorldObject.Transform.Position;
        world.Step(1f / 60f);
        Assert.NotEmpty(callbacks.Before);
        Assert.True(callbacks.Before[0].Geometry.PenetrationDepth > 0f);
        Assert.Equal(before.Y - 0.1f, actor.WorldObject.Transform.Position.Y, 4);
        Assert.Equal(-6f, actor.LinearVelocity.Y);
        Assert.Empty(world.LastContacts);
        Assert.Empty(callbacks.After);
    }

    [Fact]
    public void AfterCallbackRunsOncePerAcceptedPairAfterAllSubstepsAndVelocityResponse()
    {
        var (world, actor, _) = CreateWorld();
        world.Gravity = new(0f, -100f);
        world.MaxSubstepSeconds = 1f / 120f;
        var callbacks = new Callbacks
        {
            AfterAction = contact =>
            {
                Assert.Equal(0f, actor.LinearVelocity.Y, 4);
                Assert.Single(world.LastContacts);
            }
        };
        world.CollisionCallbacks = callbacks;
        world.Step(1f / 30f);
        Assert.True(callbacks.Before.Count > 1);
        Assert.Single(callbacks.After);
        Assert.Equal("after", callbacks.Stages[^1]);
    }

    [Fact]
    public void SensorContactsNotifyWithoutPhysicalResponse()
    {
        var (world, actor, _) = CreateWorld();
        actor.IsSensor = true;
        actor.LinearVelocity = new(0f, -6f);
        var callbacks = new Callbacks();
        world.CollisionCallbacks = callbacks;
        world.Step(1f / 60f);
        Assert.Single(callbacks.After);
        Assert.Single(world.LastContacts);
        Assert.Equal(-6f, actor.LinearVelocity.Y);
        Assert.Equal(0.7f, actor.WorldObject.Transform.Position.Y, 4);
    }

    [Fact]
    public void AfterCallbacksCanRemoveBodiesWithoutInvalidatingNotificationsAndCannotRecursivelyStep()
    {
        var (world, actor, _) = CreateWorld();
        actor.WorldObject.Transform.Position = new(-4f, 0.8f);
        var second = AddActor(world, new(4f, 0.8f));
        var callbacks = new Callbacks
        {
            AfterAction = contact =>
            {
                Assert.Throws<InvalidOperationException>(() => world.Step(1f / 60f));
                var dynamicBody = contact.First.MotionType == BodyMotionType2D.Dynamic ? contact.First : contact.Second;
                Assert.True(world.RemoveBody(dynamicBody));
            }
        };
        world.CollisionCallbacks = callbacks;
        world.Step(1f / 60f);
        Assert.Equal(2, callbacks.After.Count);
        Assert.DoesNotContain(actor, world.Bodies);
        Assert.DoesNotContain(second, world.Bodies);
        Assert.Single(world.Bodies);
        Assert.Empty(world.LastContacts);
    }

    [Fact]
    public void AThrowingCallbackDoesNotLeaveTheWorldMarkedAsStepping()
    {
        var (world, _, _) = CreateWorld();
        world.CollisionCallbacks = new Callbacks { BeforeAction = _ => throw new ApplicationException("test") };
        Assert.Throws<ApplicationException>(() => world.Step(1f / 60f));
        world.CollisionCallbacks = null;
        world.Step(1f / 60f);
        Assert.Single(world.LastContacts);
    }

    [Fact]
    public void WorldPairQueryAndDetectionHonorBothFilterStagesAndMaskFlags()
    {
        var (world, actor, ground) = CreateWorld();
        var callbacks = new Callbacks();
        world.CollisionCallbacks = callbacks;
        world.CollisionSystem.PairFilter = new RejectAll<Collider2D>();
        Assert.False(world.CanCollide(actor, ground));
        Assert.False(world.CanCollide(ground, actor));
        world.Step(1f / 60f);
        Assert.Empty(callbacks.Before);

        world.CollisionSystem.PairFilter = new DefaultColliderPairFilter2D();
        world.PairFilter = new RejectAll<RigidBody2D>();
        Assert.False(world.CanCollide(actor, ground));
        world.Step(1f / 60f);
        Assert.Empty(callbacks.Before);

        world.PairFilter = new DefaultPhysicsPairFilter2D();
        actor.CollisionMask = 0u;
        Assert.False(world.CanCollide(actor, ground));
        world.Step(1f / 60f);
        Assert.Empty(callbacks.Before);
        Assert.Equal(0, world.CollisionSystem.LastNarrowPhaseTestCount);
    }

    private static (PhysicsWorld2D World, RigidBody2D Actor, RigidBody2D Ground) CreateWorld()
    {
        var world = new PhysicsWorld2D();
        var actor = AddActor(world, new(0f, 0.8f));
        var floor = new SpatialObject2D(Rectangle2D.Builder.CreateFromSize(100f, 2f));
        floor.Transform.Position = new(0f, -1f);
        var ground = world.AddBody(floor, BodyMotionType2D.Static);
        ground.Restitution = 0f;
        return (world, actor, ground);
    }

    private static RigidBody2D AddActor(PhysicsWorld2D world, Vector2 position)
    {
        var spatial = new SpatialObject2D(Circle2D.Builder.Create(1f));
        spatial.Transform.Position = position;
        var actor = world.AddBody(spatial, BodyMotionType2D.Dynamic);
        actor.Restitution = 0f;
        return actor;
    }

    private sealed class RejectAll<T> : IPairFilter2D<T> where T : class
    {
        public bool ShouldTest(T first, T second) => false;
    }

    private sealed class Callbacks : IPhysicsCollisionCallbacks2D
    {
        public bool Accept { get; set; } = true;
        public Action<PhysicsContact2D>? BeforeAction { get; init; }
        public Action<PhysicsContact2D>? AfterAction { get; set; }
        public List<PhysicsContact2D> Before { get; } = [];
        public List<PhysicsContact2D> After { get; } = [];
        public List<string> Stages { get; } = [];
        public bool OnBeforeCollision(PhysicsContact2D contact)
        {
            Before.Add(contact);
            Stages.Add("before");
            BeforeAction?.Invoke(contact);
            return Accept;
        }
        public void OnAfterCollision(PhysicsContact2D contact)
        {
            After.Add(contact);
            Stages.Add("after");
            AfterAction?.Invoke(contact);
        }
    }
}
