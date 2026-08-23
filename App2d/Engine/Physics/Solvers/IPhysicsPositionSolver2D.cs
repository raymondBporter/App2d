namespace App2d.Engine.Physics.Solvers;

public interface IPhysicsPositionSolver2D
{
    void Solve(PhysicsContact2D contact);
}
