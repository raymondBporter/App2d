using App2d.Core;
using App2d.Rendering.Textures;
using System.Numerics;

namespace App2d.Gameplay.Persons.Actions;

internal abstract class PersonWeapon2DBase(
    string name,
    string equipmentId,
    Texture2D hudTexture) : IPersonWeapon2D
{
    public string Name { get; } = ArgGuard.RequireNotNull(name);
    public string EquipmentId { get; } = ArgGuard.RequireNotNull(equipmentId);
    public virtual string Status => Name;
    public Texture2D HudTexture { get; } = ArgGuard.RequireNotNull(hudTexture);
    public virtual IEnumerable<SpatialObject2D> ActiveHitboxes => [];

    public abstract float Use(Vector2? aimTarget, float facing);
    public virtual void OnDeselected() { }
    public virtual void BeginFrame(float deltaSeconds) { }
    public virtual void UpdateBeforePhysics(float deltaSeconds) { }
    public virtual void UpdateAfterPhysics(float deltaSeconds, float facing) { }
    public virtual void Reset() { }
}
