using System.Collections.Immutable;

namespace App2d.Gameplay.Simulation;

/// <summary>A world's rollback data, including identities of reconstructible terrain bodies.</summary>
public abstract record WorldSimulationState2D : SimulationState2D
{
    public abstract ImmutableArray<int> TerrainColliderIds { get; }
}
