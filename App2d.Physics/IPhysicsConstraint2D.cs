namespace App2d.Physics;

public interface IPhysicsConstraint2D
{
    bool IsEnabled { get; }

    void SolveVelocity(float deltaSeconds);

    // Return true when the constraint moved state during this relaxation pass.
    bool SolvePosition(float deltaSeconds);
}
