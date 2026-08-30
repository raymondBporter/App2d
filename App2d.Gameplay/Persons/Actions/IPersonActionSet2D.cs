using System.Numerics;

namespace App2d.Gameplay;

/// <summary>Coarse action seam coordinated by <see cref="Person2D"/>.</summary>
public interface IPersonActionSet2D
{
    void BeginFrame(float deltaSeconds);
    void UpdateBeforePhysics(float deltaSeconds);
    void UpdateAfterPhysics(float deltaSeconds, float facing);
    float UsePrimary(Vector2? aimTarget, float facing);
    void SelectNext();
    void Reset();
}
