namespace App2d.Core.Physics.Solvers;

public interface IPhysicsPositionSolver2D
{
    void Solve(PhysicsContact2D contact);
}
