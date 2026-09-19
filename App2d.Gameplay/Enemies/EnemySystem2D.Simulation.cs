using App2d.Core;
using App2d.Gameplay.Simulation;
using System.Collections.Immutable;

namespace App2d.Gameplay.Enemies;

public sealed partial class EnemySystem2D
{
    internal sealed record EntryState(EntityId2D Id, bool Enabled, SimulationState2D Actor);
    internal ImmutableArray<EntryState> CaptureSimulation() => _registeredEnemies.Select(e =>
        new EntryState(e.Actor.Combatant.Id, e.IsEnabled, e.Actor.CaptureSimulation())).ToImmutableArray();

    internal void ValidateSimulation(ImmutableArray<EntryState> state) => StateGuard.ThrowIf(
        !_registeredEnemies.Select(e => e.Actor.Combatant.Id).SequenceEqual(state.Select(e => e.Id)),
        "Enemy membership changed since capture.");

    internal void RestoreSimulation(ImmutableArray<EntryState> state)
    {
        ValidateSimulation(state);
        for (var i = 0; i < state.Length; i++)
        {
            _registeredEnemies[i].IsEnabled = state[i].Enabled;
            _registeredEnemies[i].Actor.RestoreSimulation(state[i].Actor);
        }
    }
}
