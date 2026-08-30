using App2d.Core.Mathematics;
using System.Numerics;

namespace App2d.Physics.Solvers;

public sealed class ImpulseVelocitySolver2D : IPhysicsVelocitySolver2D
{
    public void Solve(PhysicsContact2D contact)
    {
        var first = contact.First;
        var second = contact.Second;
        var firstInverseMass = first.InverseMass;
        var secondInverseMass = second.InverseMass;
        var firstInverseInertia = first.EffectiveInverseInertia;
        var secondInverseInertia = second.EffectiveInverseInertia;
        if (firstInverseMass + secondInverseMass + firstInverseInertia + secondInverseInertia <= 0f)
            return;

        var normal = contact.Geometry.Normal;
        // Centers of mass are approximated by the transform positions.
        var firstArm = contact.Geometry.Point - first.WorldObject.Transform.Position;
        var secondArm = contact.Geometry.Point - second.WorldObject.Transform.Position;

        var relativeVelocity = PointVelocity(first, firstArm) - PointVelocity(second, secondArm);
        var normalSpeed = Vector2.Dot(relativeVelocity, normal);
        if (normalSpeed >= 0f)
            return;

        var firstArmCrossNormal = firstArm.Cross(normal);
        var secondArmCrossNormal = secondArm.Cross(normal);
        var normalEffectiveMass = firstInverseMass + secondInverseMass +
            firstInverseInertia * firstArmCrossNormal * firstArmCrossNormal +
            secondInverseInertia * secondArmCrossNormal * secondArmCrossNormal;
        if (normalEffectiveMass <= 0f)
            return;

        var restitution = Math.Min(first.Restitution, second.Restitution);
        var normalImpulse = -(1f + restitution) * normalSpeed / normalEffectiveMass;
        Apply(first, second, firstArm, secondArm, normal * normalImpulse);

        var friction = MathF.Sqrt(first.Friction * second.Friction);
        if (friction <= 0f)
            return;

        var tangent = normal.PerpCcw();
        relativeVelocity = PointVelocity(first, firstArm) - PointVelocity(second, secondArm);
        var tangentSpeed = Vector2.Dot(relativeVelocity, tangent);
        var firstArmCrossTangent = firstArm.Cross(tangent);
        var secondArmCrossTangent = secondArm.Cross(tangent);
        var tangentEffectiveMass = firstInverseMass + secondInverseMass +
            firstInverseInertia * firstArmCrossTangent * firstArmCrossTangent +
            secondInverseInertia * secondArmCrossTangent * secondArmCrossTangent;
        if (tangentEffectiveMass <= 0f)
            return;

        var tangentImpulse = Math.Clamp(-tangentSpeed / tangentEffectiveMass, -friction * normalImpulse, friction * normalImpulse);
        Apply(first, second, firstArm, secondArm, tangent * tangentImpulse);
    }

    // ω × r in 2D is ω * PerpCcw(r).
    private static Vector2 PointVelocity(PhysicsBody2D body, Vector2 arm) =>
        body.LinearVelocity + body.AngularVelocity * arm.PerpCcw();

    private static void Apply(PhysicsBody2D first, PhysicsBody2D second, Vector2 firstArm, Vector2 secondArm, Vector2 impulse)
    {
        first.LinearVelocity += impulse * first.InverseMass;
        first.AngularVelocity += first.EffectiveInverseInertia * firstArm.Cross(impulse);
        second.LinearVelocity -= impulse * second.InverseMass;
        second.AngularVelocity -= second.EffectiveInverseInertia * secondArm.Cross(impulse);
    }
}
