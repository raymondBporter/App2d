namespace App2d.Physics.Solvers;

public interface IPhysicsPositionSolver2D
{
    void Solve(PhysicsContact2D contact);
}
