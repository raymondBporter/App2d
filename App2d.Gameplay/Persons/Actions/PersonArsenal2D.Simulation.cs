using App2d.Gameplay.Simulation;

namespace App2d.Gameplay.Persons.Actions;

public sealed partial class PersonArsenal2D
{
    internal sealed record SimulationState(
        SwordPersonWeapon2D.SimulationState Sword,
        GunPersonWeapon2D.SimulationState Gun,
        int Energy, double EnergyRecharge, float HealElapsed, bool Healing, bool HealNeedsRelease) : SimulationState2D;

    public SimulationState2D CaptureSimulation() => new SimulationState(
        _sword.CaptureSimulation(), _gun.CaptureSimulation(),
        _energy, _energyRecharge, _healElapsed, _healing, _healNeedsRelease);

    public void RestoreSimulation(SimulationState2D snapshot)
    {
        var state = (SimulationState)snapshot;
        _sword.RestoreSimulation(state.Sword);
        _gun.RestoreSimulation(state.Gun);
        _energy = state.Energy;
        _energyRecharge = state.EnergyRecharge;
        _healElapsed = state.HealElapsed;
        _healing = state.Healing;
        _healNeedsRelease = state.HealNeedsRelease;
    }
}
