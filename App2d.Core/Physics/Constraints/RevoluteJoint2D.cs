using System.Numerics;

namespace App2d.Core.Physics.Constraints;

/// <summary>A firm bearing: two local anchors coincide, while relative rotation is unrestricted.</summary>
public sealed class RevoluteJoint2D : IPhysicsConstraint2D
{
    public RevoluteJoint2D(RigidBody2D first, RigidBody2D second, Vector2 firstLocalAnchor, Vector2 secondLocalAnchor)
    {
        JointMath2D.Validate(first, second, firstLocalAnchor, secondLocalAnchor);
        First = first;
        Second = second;
        FirstLocalAnchor = firstLocalAnchor;
        SecondLocalAnchor = secondLocalAnchor;
    }

    public RigidBody2D First { get; }
    public RigidBody2D Second { get; }
    public Vector2 FirstLocalAnchor { get; }
    public Vector2 SecondLocalAnchor { get; }
    public Vector2 FirstWorldAnchor => JointMath2D.Anchor(First, FirstLocalAnchor);
    public Vector2 SecondWorldAnchor => JointMath2D.Anchor(Second, SecondLocalAnchor);
    public bool IsEnabled { get; set; } = true;

    public void SolveVelocity(float deltaSeconds)
    {
        if (!IsEnabled) return;
        var firstArm = FirstWorldAnchor - First.WorldCenterOfMass;
        var secondArm = SecondWorldAnchor - Second.WorldCenterOfMass;
        var relativeVelocity = JointMath2D.PointVelocity(Second, secondArm) - JointMath2D.PointVelocity(First, firstArm);
        var impulse = -JointMath2D.SolvePointMass(First, Second, firstArm, secondArm, relativeVelocity);
        JointMath2D.ApplyVelocity(First, Second, impulse, JointMath2D.Cross(firstArm, impulse), JointMath2D.Cross(secondArm, impulse));
    }

    public bool SolvePosition(float deltaSeconds)
    {
        if (!IsEnabled) return false;
        var firstAnchor = FirstWorldAnchor;
        var secondAnchor = SecondWorldAnchor;
        var error = secondAnchor - firstAnchor;
        if (error.LengthSquared() <= JointMath2D.PositionTolerance * JointMath2D.PositionTolerance) return false;
        if (error.LengthSquared() > 100f) error = Vector2.Normalize(error) * 10f;
        var firstArm = firstAnchor - First.WorldCenterOfMass;
        var secondArm = secondAnchor - Second.WorldCenterOfMass;
        var impulse = -JointMath2D.SolvePointMass(First, Second, firstArm, secondArm, error);
        return JointMath2D.ApplyPosition(First, Second, impulse, JointMath2D.Cross(firstArm, impulse), JointMath2D.Cross(secondArm, impulse));
    }
}
