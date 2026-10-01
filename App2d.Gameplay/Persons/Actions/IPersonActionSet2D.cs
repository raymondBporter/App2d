using App2d.Contracts.Persons;
namespace App2d.Gameplay.Persons.Actions;

/// <summary>Coarse action seam coordinated by <see cref="Person2D"/>.</summary>
public interface IPersonActionSet2D
{
    PersonActionState2D CaptureActionState() => default;
    bool IsChargingPrimary => false;
    SpellState2D CaptureSpellState() => default;
    void SetSpellInput(bool castHeld, bool castPressed, bool healHeld, bool canHeal, bool canCast, float facing) { }
    void CancelHealing() { }
    void SetPrimaryInput(bool held, bool canCharge, bool released = false) { }
    void InterruptPrimary() { }
    bool ConsumeDownAttackBounce() => false;
    void BeginFrame(float deltaSeconds);
    void UpdateAfterPhysics(float deltaSeconds, float facing);
    float UsePrimary(float facing);
    float UseDownwardPrimary(float facing) => UsePrimary(facing);
    float UseSecondary(float facing);
    void Reset();
}
