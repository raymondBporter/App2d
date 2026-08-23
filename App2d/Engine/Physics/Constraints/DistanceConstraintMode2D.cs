namespace App2d.Engine.Physics.Constraints;

public enum DistanceConstraintMode2D
{
    // Enforces the rest length in both directions, like a rigid rod.
    Rod,

    // Only prevents stretching past the rest length; a slack rope applies no forces.
    Rope
}
