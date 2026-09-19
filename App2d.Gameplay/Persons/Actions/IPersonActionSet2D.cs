namespace App2d.Gameplay.Persons.Actions;

/// <summary>Coarse action seam coordinated by <see cref="Person2D"/>.</summary>
public interface IPersonActionSet2D
{
    PersonActionState2D CaptureActionState() => default;
    bool IsChargingPrimary => false;
    void SetPrimaryInput(bool held, bool canCharge, bool released = false) { }
    void InterruptPrimary() { }
    bool ConsumeDownAttackBounce() => false;
    void BeginFrame(float deltaSeconds);
    void UpdateAfterPhysics(float deltaSeconds, float facing);
    float UsePrimary(float facing);
    float UseDownwardPrimary(float facing) => UsePrimary(facing);
    float UseSecondary(float facing);
    void SelectNext();
    void Reset();
}
