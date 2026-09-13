using System.Numerics;

namespace App2d.Gameplay.Persons.Actions;

/// <summary>Coarse action seam coordinated by <see cref="Person2D"/>.</summary>
public interface IPersonActionSet2D
{
    bool IsChargingPrimary => false;
    void SetPrimaryInput(bool held, bool canCharge, bool released = false) { }
    void InterruptPrimary() { }
    bool ConsumeDownAttackBounce() => false;
    void BeginFrame(float deltaSeconds);
    void UpdateBeforePhysics(float deltaSeconds);
    void UpdateAfterPhysics(float deltaSeconds, float facing);
    float UsePrimary(Vector2? aimTarget, float facing);
    float UseDownwardPrimary(Vector2? aimTarget, float facing) => UsePrimary(aimTarget, facing);
    float UseSecondary(Vector2? aimTarget, float facing);
    void SelectNext();
    void Reset();
}
