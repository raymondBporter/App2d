using App2d.Core;
using App2d.Physics;
using System.Collections.Immutable;

namespace App2d.Gameplay.Simulation;

/// <summary>
/// An immutable, in-process rollback checkpoint of a particular session at a tick boundary.
/// Fixed actor/level definitions belong to that session; this is not a wire or save-game format.
/// </summary>
public sealed class SessionCheckpoint2D
{
    internal SessionCheckpoint2D(Guid owner, long tick, long eventSequence, bool paused,
        ImmutableArray<SideScrollerSession2D.ParticipantState> players,
        WorldSimulationState2D world, PhysicsWorldState2D physics, int defeatedEnemies,
        ImmutableArray<EntityId2D> combatants, ImmutableArray<SessionEvent2D> events)
    {
        Owner = owner; Tick = tick; EventSequence = eventSequence; IsPaused = paused;
        Players = players; World = world; Physics = physics;
        DefeatedEnemies = defeatedEnemies; Combatants = combatants; Events = events;
    }
    public long Tick { get; }
    public bool IsPaused { get; }
    internal Guid Owner { get; }
    internal long EventSequence { get; }
    internal ImmutableArray<SideScrollerSession2D.ParticipantState> Players { get; }
    internal WorldSimulationState2D World { get; }
    internal PhysicsWorldState2D Physics { get; }
    internal int DefeatedEnemies { get; }
    internal ImmutableArray<EntityId2D> Combatants { get; }
    internal ImmutableArray<SessionEvent2D> Events { get; }
}
