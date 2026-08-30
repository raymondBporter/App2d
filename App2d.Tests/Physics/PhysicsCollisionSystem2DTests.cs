using System.Numerics;
using App2d.Core;
using App2d.Core.Geometry;
using App2d.Collision;
using App2d.Physics;

namespace App2d.Tests.Physics;

public sealed class PhysicsCollisionSystem2DTests
{
    [Fact]
    public void BodiesRegisterWithTheInjectedCollisionSystem()
    {
        var collision = new CollisionSystem2D();
        var physics = new PhysicsWorld2D(collision);
        var body = physics.AddBody(
            new SpatialObject2D(new Circle2D(5f)),
            BodyMotionType2D.Dynamic);

        Assert.Same(body.Collider, Assert.Single(collision.Colliders));
        Assert.Same(body, body.Collider.UserData);

        Assert.True(physics.RemoveBody(body));
        Assert.Empty(collision.Colliders);
    }

    [Fact]
    public void PhysicsConsumesContactsProducedByCollision()
    {
        var collision = new CollisionSystem2D();
        var physics = new PhysicsWorld2D(collision)
        {
            Gravity = Vector2.Zero,
            PositionIterations = 1,
            VelocityIterations = 1
        };
        physics.AddBody(
            new SpatialObject2D(new Circle2D(10f)),
            BodyMotionType2D.Static);
        var dynamicObject = new SpatialObject2D(new Circle2D(10f));
        dynamicObject.Transform.Position = new Vector2(15f, 0f);
        physics.AddBody(dynamicObject, BodyMotionType2D.Dynamic);

        physics.Step(1f / 60f);

        Assert.Single(physics.LastContacts);
        Assert.Equal(1, collision.LastCandidatePairCount);
        Assert.Equal(1, collision.LastNarrowPhaseTestCount);
    }
}
