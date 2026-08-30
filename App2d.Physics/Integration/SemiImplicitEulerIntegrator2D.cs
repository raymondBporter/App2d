using App2d.Core;
using System.Numerics;

namespace App2d.Physics.Integration;

public sealed class SemiImplicitEulerIntegrator2D : IPhysicsIntegrator2D
{
    public void Integrate(PhysicsBody2D body, Vector2 gravity, float deltaSeconds)
    {
        switch (body.MotionType)
        {
            case BodyMotionType2D.Static:
                return;
            case BodyMotionType2D.Kinematic:
                IntegrateTransform(body, deltaSeconds);
                return;
            case BodyMotionType2D.Dynamic:
                var acceleration = gravity * body.GravityScale + body.AccumulatedForce * body.InverseMass;
                body.LinearVelocity += acceleration * deltaSeconds;
                body.AngularVelocity += body.AccumulatedTorque * body.InverseInertia * deltaSeconds;
                IntegrateTransform(body, deltaSeconds);
                return;
            default:
                throw ArgGuard.CreateOutOfRange(
                    body.MotionType,
                    "Unknown body motion type.");
        }
    }

    private static void IntegrateTransform(PhysicsBody2D body, float deltaSeconds)
    {
        body.WorldObject.Transform.Position += body.LinearVelocity * deltaSeconds;
        body.WorldObject.Transform.Rotation += body.AngularVelocity * deltaSeconds;
    }
}
