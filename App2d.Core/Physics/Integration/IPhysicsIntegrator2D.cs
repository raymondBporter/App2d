namespace App2d.Core.Physics.Integration;

public interface IPhysicsIntegrator2D
{
    void Integrate(RigidBody2D body, Vector2f gravity, float deltaSeconds);
}
