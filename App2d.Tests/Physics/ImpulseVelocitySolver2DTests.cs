using System.Numerics;
using App2d.Core.Geometry;
using App2d.Core;
using App2d.Collision.Contacts;
using App2d.Physics;
using App2d.Physics.Solvers;

namespace App2d.Tests.Physics;

public sealed class ImpulseVelocitySolver2DTests
{
    private static PhysicsBody2D DynamicBody(PhysicsWorld2D world, Vector2 position)
    {
        var worldObject = new SpatialObject2D(Rectangle2D.FromSize(new Vector2(2f, 2f)));
        worldObject.Transform.Position = position;
        return world.AddBody(worldObject, BodyMotionType2D.Dynamic);
    }

    private static PhysicsBody2D StaticGround(PhysicsWorld2D world)
    {
        var worldObject = new SpatialObject2D(Rectangle2D.FromSize(new Vector2(100f, 2f), new Vector2(0f, -1f)));
        return world.AddBody(worldObject, BodyMotionType2D.Static);
    }

    [Fact]
    public void OffCenterContactSpinsAnUnfrozenBody()
    {
        var world = new PhysicsWorld2D();
        var body = DynamicBody(world, new Vector2(0f, 1f));
        body.FreezeRotation = false;
        body.MomentOfInertia = 1f;
        body.Restitution = 0f;
        body.LinearVelocity = new Vector2(0f, -5f);
        var ground = StaticGround(world);

        // Contact at the body's right corner, normal up.
        var contact = new PhysicsContact2D(body, ground, new CollisionContact2D(new Vector2(1f, 0f), Vector2.UnitY, 0.1f));
        new ImpulseVelocitySolver2D().Solve(contact);

        Assert.True(body.LinearVelocity.Y > -5f); // normal impulse applied
        Assert.NotEqual(0f, body.AngularVelocity); // and it spun
    }

    [Fact]
    public void FrozenBodyGetsNoSpinAndFullLinearImpulse()
    {
        var world = new PhysicsWorld2D();
        var body = DynamicBody(world, new Vector2(0f, 1f));
        body.Restitution = 0f;
        body.LinearVelocity = new Vector2(0f, -5f);
        var ground = StaticGround(world);

        var contact = new PhysicsContact2D(body, ground, new CollisionContact2D(new Vector2(1f, 0f), Vector2.UnitY, 0.1f));
        new ImpulseVelocitySolver2D().Solve(contact);

        Assert.Equal(0f, body.AngularVelocity);
        Assert.Equal(0f, body.LinearVelocity.Y, 3); // exactly killed, matching prior behavior
    }

    [Fact]
    public void FrictionRemovesTangentialSpeedUpToTheCoulombLimit()
    {
        var world = new PhysicsWorld2D();
        var body = DynamicBody(world, new Vector2(0f, 1f));
        body.Friction = 1f;
        body.Restitution = 0f;
        body.LinearVelocity = new Vector2(3f, -5f);
        var ground = StaticGround(world);
        ground.Friction = 1f;

        var contact = new PhysicsContact2D(body, ground, new CollisionContact2D(new Vector2(0f, 0f), Vector2.UnitY, 0.1f));
        new ImpulseVelocitySolver2D().Solve(contact);

        Assert.True(MathF.Abs(body.LinearVelocity.X) < 3f); // tangential speed reduced
    }

    [Fact]
    public void ZeroFrictionLeavesTangentialSpeedAlone()
    {
        var world = new PhysicsWorld2D();
        var body = DynamicBody(world, new Vector2(0f, 1f));
        body.Restitution = 0f;
        body.LinearVelocity = new Vector2(3f, -5f);
        var ground = StaticGround(world);

        var contact = new PhysicsContact2D(body, ground, new CollisionContact2D(new Vector2(0f, 0f), Vector2.UnitY, 0.1f));
        new ImpulseVelocitySolver2D().Solve(contact);

        Assert.Equal(3f, body.LinearVelocity.X, 3);
    }
}
