namespace App2d.Physics.Solvers;

public interface IPhysicsVelocitySolver2D
{
    void Solve(PhysicsContact2D contact);
}
