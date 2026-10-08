using App2d.Contracts.Persons.Actions;
using App2d.Gameplay.Persons.Actions;

namespace App2d.Gameplay.Simulation;

/// <summary>The existing arsenal's simulation-facing surface; no textures or HUD objects.</summary>
public interface ISessionPlayerActions2D : IPersonActionSet2D
{
    WeaponState2D CaptureWeaponState();
    event Action<WeaponEvent2D>? WeaponOccurred;
    EquipmentKind2D Equipment { get; }
    bool IsMeleeAttackActive { get; }
    event Action<float>? MeleeAttackStarted;
    event Action<float>? DownAttackStarted;
    event Action? ShotStarted;
    event Action<UnarmedAttackKind2D, float>? UnarmedAttackStarted;
}
