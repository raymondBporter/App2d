namespace App2d.Engine.Physics.Solvers;

public interface IPhysicsVelocitySolver2D
{
    void Solve(PhysicsContact2D contact);
}
