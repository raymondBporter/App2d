using System.Numerics;

namespace App2d.Core.Physics.Motion;

/// <summary>An immutable, deterministic translation path in world space.</summary>
public interface IKinematicMotion2D
{
    Vector2f Position(double seconds);
    Vector2f Velocity(double seconds);
}
