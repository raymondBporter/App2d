using App2d.Core.Validation;
using System.Numerics;

namespace App2d.Core.Physics.Integration;

public sealed class SemiImplicitEulerIntegrator2D : IPhysicsIntegrator2D
{
    public void Integrate(RigidBody2D body, Vector2 gravity, float deltaSeconds)
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

    private static void IntegrateTransform(RigidBody2D body, float deltaSeconds)
    {
        if (body.LocalCenterOfMass != Vector2.Zero && body.AngularVelocity != 0f)
        {
            var center = body.WorldCenterOfMass + body.LinearVelocity * deltaSeconds;
            body.WorldObject.Transform.Rotation += body.AngularVelocity * deltaSeconds;
            body.WorldObject.Transform.Position += center - body.WorldCenterOfMass;
            return;
        }

        body.WorldObject.Transform.Position += body.LinearVelocity * deltaSeconds;
        body.WorldObject.Transform.Rotation += body.AngularVelocity * deltaSeconds;
    }
}
