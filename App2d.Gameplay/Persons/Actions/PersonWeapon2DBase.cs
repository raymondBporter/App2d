using App2d.Core;

namespace App2d.Gameplay.Persons.Actions;

internal abstract class PersonWeapon2DBase(EquipmentKind2D kind) : IPersonWeapon2D
{
    public EquipmentKind2D Kind { get; } = kind;
    public virtual IEnumerable<SpatialObject2D> ActiveHitboxes => [];
    public virtual PersonActionState2D CaptureActionState() => default;

    public abstract float Use(float facing);
    public virtual void OnDeselected() { }
    public virtual void BeginFrame(float deltaSeconds) { }
    public virtual void UpdateAfterPhysics(float deltaSeconds, float facing) { }
    public virtual void Reset() { }
}
