using System.Numerics;

namespace App2d.Physics.Integration;

public interface IPhysicsIntegrator2D
{
    void Integrate(PhysicsBody2D body, Vector2 gravity, float deltaSeconds);
}
