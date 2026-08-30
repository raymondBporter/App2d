using App2d.Core;
using App2d.Rendering.Textures;
using System.Numerics;

namespace App2d.Gameplay.Persons.Actions;

public interface IPersonWeapon2D
{
    string Name { get; }
    string EquipmentId { get; }
    string Status { get; }
    Texture2D HudTexture { get; }
    IEnumerable<SpatialObject2D> ActiveHitboxes { get; }

    float Use(Vector2? aimTarget, float facing);
    void OnDeselected();
    void BeginFrame(float deltaSeconds);
    void UpdateBeforePhysics(float deltaSeconds);
    void UpdateAfterPhysics(float deltaSeconds, float facing);
    void Reset();
}
