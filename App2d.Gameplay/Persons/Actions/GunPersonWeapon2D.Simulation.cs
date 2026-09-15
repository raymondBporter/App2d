using App2d.Core;
using App2d.Core.Mathematics;
using App2d.Gameplay.Simulation;
using System.Collections.Immutable;
using System.Numerics;

namespace App2d.Gameplay.Persons.Actions;

internal sealed partial class GunPersonWeapon2D
{
    internal sealed record SimulationState(
        float ChargeTime,
        float Direction,
        float RecoverySeconds,
        float? SecondsSinceShot,
        bool CanCharge,
        bool NeedsRelease,
        bool IsCharging,
        int CreationSequence,
        ImmutableArray<Projectile2D.SimulationState> Projectiles) : SimulationState2D;

    internal SimulationState CaptureSimulation() => new SimulationState(
        _chargeTime, _direction, _recoverySeconds, _secondsSinceShot, _canCharge, _needsRelease, IsCharging, _projectileIds.Position, _bullets.Select(b => b.CaptureSimulation()).ToImmutableArray());

    internal void RestoreSimulation(SimulationState snapshot)
    {
        var state = snapshot;
        _chargeTime = state.ChargeTime;
        _direction = state.Direction;
        _recoverySeconds = state.RecoverySeconds;
        _secondsSinceShot = state.SecondsSinceShot;
        _canCharge = state.CanCharge;
        _needsRelease = state.NeedsRelease;
        IsCharging = state.IsCharging;
        _projectileIds.Restore(state.CreationSequence);
        for (var i = 0; i < _bullets.Count; i++) _bullets[i].RestoreSimulation(state.Projectiles[i]);
    }
}
