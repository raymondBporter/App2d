namespace App2d.Core.Physics;

public interface IPhysicsConstraint2D
{
    bool IsEnabled { get; }

    /// <summary>Called once per substep, after contact response and before velocity iterations.</summary>
    void PrepareVelocity(float deltaSeconds) { }

    void SolveVelocity(float deltaSeconds);

    // Return true when the constraint moved state during this relaxation pass.
    bool SolvePosition(float deltaSeconds);
}
