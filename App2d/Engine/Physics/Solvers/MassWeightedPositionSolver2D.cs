namespace App2d.Engine.Physics.Solvers;

public sealed class MassWeightedPositionSolver2D : IPhysicsPositionSolver2D
{
    public void Solve(PhysicsContact2D contact)
    {
        var firstWeight = GetWeight(contact.First, contact.Second);
        var secondWeight = GetWeight(contact.Second, contact.First);
        var totalWeight = firstWeight + secondWeight;
        if (totalWeight <= 0f)
            return;

        var correction = contact.Geometry.MinimumTranslationVector;
        contact.First.WorldObject.Transform.Position += correction * (firstWeight / totalWeight);
        contact.Second.WorldObject.Transform.Position -= correction * (secondWeight / totalWeight);
    }

    private static float GetWeight(PhysicsBody2D body, PhysicsBody2D other)
    {
        return body.MotionType switch
        {
            BodyMotionType2D.Dynamic => body.InverseMass,
            // Controllers move out of static level geometry but are not pushed by dynamics.
            BodyMotionType2D.Kinematic when other.MotionType == BodyMotionType2D.Static => 1f,
            _ => 0f
        };
    }
}
