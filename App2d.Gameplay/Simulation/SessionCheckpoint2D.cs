using App2d.Core;
using App2d.Gameplay.Persons;
using App2d.Physics;
using System.Collections.Immutable;

namespace App2d.Gameplay.Simulation;

/// <summary>
/// An immutable, in-process rollback checkpoint of a particular session at a tick boundary.
/// Fixed actor/level definitions belong to that session; this is not a wire or save-game format.
/// </summary>
public sealed class SessionCheckpoint2D
{
    internal SessionCheckpoint2D(Guid owner, long tick, long lastInputSequence, long eventSequence,
        bool paused, RespawnState2D respawn, float restartSeconds, float moveX, bool reachedGoal,
        Person2D.SimulationState player, SimulationState2D actions, WorldSimulationState2D world,
        PhysicsWorldState2D physics, int defeatedEnemies, ImmutableArray<EntityId2D> combatants,
        ImmutableArray<SessionEvent2D> events)
    {
        Owner = owner; Tick = tick; LastInputSequence = lastInputSequence; EventSequence = eventSequence;
        IsPaused = paused; Respawn = respawn; RestartSeconds = restartSeconds; MoveX = moveX;
        ReachedGoal = reachedGoal; Player = player; Actions = actions; World = world; Physics = physics;
        DefeatedEnemies = defeatedEnemies; Combatants = combatants; Events = events;
    }
    public long Tick { get; }
    public long LastInputSequence { get; }
    public bool IsPaused { get; }
    internal Guid Owner { get; }
    internal long EventSequence { get; }
    internal RespawnState2D Respawn { get; }
    internal float RestartSeconds { get; }
    internal float MoveX { get; }
    internal bool ReachedGoal { get; }
    internal Person2D.SimulationState Player { get; }
    internal SimulationState2D Actions { get; }
    internal WorldSimulationState2D World { get; }
    internal PhysicsWorldState2D Physics { get; }
    internal int DefeatedEnemies { get; }
    internal ImmutableArray<EntityId2D> Combatants { get; }
    internal ImmutableArray<SessionEvent2D> Events { get; }
}
