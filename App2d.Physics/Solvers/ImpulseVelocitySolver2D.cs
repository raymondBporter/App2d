using System.Numerics;

namespace App2d.Engine.Physics.Solvers;

public sealed class ImpulseVelocitySolver2D : IPhysicsVelocitySolver2D
{
    public void Solve(PhysicsContact2D contact)
    {
        var firstInverseMass = contact.First.InverseMass;
        var secondInverseMass = contact.Second.InverseMass;
        var totalInverseMass = firstInverseMass + secondInverseMass;
        if (totalInverseMass <= 0f)
            return;

        var relativeVelocity = contact.First.LinearVelocity - contact.Second.LinearVelocity;
        var normalSpeed = Vector2.Dot(relativeVelocity, contact.Geometry.Normal);
        if (normalSpeed >= 0f)
            return;

        var restitution = Math.Min(contact.First.Restitution, contact.Second.Restitution);
        var impulseMagnitude = -(1f + restitution) * normalSpeed / totalInverseMass;
        var impulse = contact.Geometry.Normal * impulseMagnitude;
        contact.First.LinearVelocity += impulse * firstInverseMass;
        contact.Second.LinearVelocity -= impulse * secondInverseMass;
    }
}
