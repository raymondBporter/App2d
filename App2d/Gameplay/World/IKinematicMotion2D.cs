using System.Numerics;

namespace App2d.Gameplay.World;

/// <summary>An immutable, deterministic translation path in world space.</summary>
public interface IKinematicMotion2D
{
    Vector2 Position(double seconds);
    Vector2 Velocity(double seconds);
}
