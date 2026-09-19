using App2d.Core;
using App2d.Core.Mathematics;
using App2d.Gameplay.Simulation;
using System.Collections.Immutable;
using System.Numerics;

namespace App2d.Gameplay.Persons.Actions;

internal sealed partial class SwordPersonWeapon2D
{
    internal new sealed record SimulationState(
        MeleePersonWeapon2D.SimulationState Swing,
        MeleePersonWeapon2D.SimulationState DownSwing,
        (bool HasBounced, bool BouncePending) Bounce) : SimulationState2D;

    internal new SimulationState CaptureSimulation() => new SimulationState(
        base.CaptureSimulation(), _downAttack.CaptureSimulation(), _downAttack.CaptureBounce());

    internal void RestoreSimulation(SimulationState snapshot)
    {
        var state = snapshot;
        base.RestoreSimulation(state.Swing);
        _downAttack.RestoreSimulation(state.DownSwing);
        _downAttack.RestoreBounce(state.Bounce.HasBounced, state.Bounce.BouncePending);
    }
}
