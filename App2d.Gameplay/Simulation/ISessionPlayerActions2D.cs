using App2d.Gameplay.Persons.Actions;

namespace App2d.Gameplay.Simulation;

/// <summary>The existing arsenal's simulation-facing surface; no textures or HUD objects.</summary>
public interface ISessionPlayerActions2D : IPersonActionSet2D
{
    SimulationState2D CaptureSimulation() => throw new NotSupportedException("This participant does not support rollback.");
    void RestoreSimulation(SimulationState2D state) => throw new NotSupportedException("This participant does not support rollback.");
    WeaponState2D CaptureWeaponState();
    event Action<WeaponEvent2D>? WeaponOccurred;
    EquipmentKind2D Equipment { get; }
    bool IsMeleeAttackActive { get; }
    event Action<EquipmentKind2D>? EquipmentChanged;
    event Action<float>? MeleeAttackStarted;
    event Action<float>? DownAttackStarted;
    event Action? ShotStarted;
    event Action<UnarmedAttackKind2D, float>? UnarmedAttackStarted;
}
