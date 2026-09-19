using App2d.Core;

namespace App2d.Gameplay.Persons.Actions;

public interface IPersonWeapon2D
{
    PersonActionState2D CaptureActionState() => default;
    EquipmentKind2D Kind { get; }
    IEnumerable<SpatialObject2D> ActiveHitboxes { get; }

    float Use(float facing);
    void OnDeselected();
    void BeginFrame(float deltaSeconds);
    void UpdateAfterPhysics(float deltaSeconds, float facing);
    void Reset();
}
