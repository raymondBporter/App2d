using App2d.Core;
using App2d.Core.Mathematics;
using App2d.Gameplay.Simulation;
using System.Collections.Immutable;
using System.Numerics;

namespace App2d.Gameplay.Persons.Actions;

public sealed partial class PersonArsenal2D
{
    internal sealed record SimulationState(
        int EquipmentIndex,
        SwordPersonWeapon2D.SimulationState Sword,
        GunPersonWeapon2D.SimulationState Gun,
        UnarmedPersonActions2D.SimulationState Unarmed) : SimulationState2D;

    public SimulationState2D CaptureSimulation() => new SimulationState(
        _equipmentIndex, _sword.CaptureSimulation(), _gun.CaptureSimulation(), _unarmed.CaptureSimulation());

    public void RestoreSimulation(SimulationState2D snapshot)
    {
        var state = (SimulationState)snapshot;
        _equipmentIndex = state.EquipmentIndex;
        _sword.RestoreSimulation(state.Sword);
        _gun.RestoreSimulation(state.Gun);
        _unarmed.RestoreSimulation(state.Unarmed);
    }
}
