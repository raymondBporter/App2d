using System.Numerics;
using App2d.Core;
using App2d.Rendering.Textures;

namespace App2d.Gameplay;

public interface IPlayerWeapon2D
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
    void ReleasePending(float facing);
    void Reset();
}
