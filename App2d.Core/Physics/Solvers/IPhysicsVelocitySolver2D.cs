namespace App2d.Core.Physics.Solvers;

public interface IPhysicsVelocitySolver2D
{
    void Solve(PhysicsContact2D contact);
}
