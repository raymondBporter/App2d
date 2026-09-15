using App2d.Core;
using App2d.Gameplay.World;
using App2d.Gameplay.Enemies;
using App2d.Gameplay.Combat;
using App2d.Gameplay.Persons;
using App2d.Gameplay.Persons.Actions;
using System.Collections.Immutable;
using System.Numerics;

namespace App2d.Gameplay.Simulation;

public readonly record struct PlayerInput2D(
    EntityId2D EntityId, long Tick, long Sequence, PersonCommand2D Command);

public readonly record struct PlayerState2D(
    PersonState2D Person, float MoveX, string EquipmentId,
    bool IsMeleeAttackActive,
    float RespawnSeconds, long? CheckpointId, bool ReachedGoal, WeaponState2D Weapons = default);

/// <summary>A complete starting observation. Historical events are intentionally absent.</summary>
public sealed record SessionSnapshot2D(
    long Tick, long LastInputSequence, PlayerState2D Player,
    WorldState2D World, ImmutableArray<EnemyState2D> Enemies);

/// <summary>Owned value data; advancing the session cannot change an earlier frame.</summary>
public sealed record SessionFrame2D(
    long Tick, long LastInputSequence, PlayerState2D Player,
    ImmutableArray<SessionEvent2D> Events)
{
    public WorldState2D World { get; init; } = WorldState2D.Empty;
    public ImmutableArray<EnemyState2D> Enemies { get; init; } = [];
}

public readonly record struct SessionEventStamp2D(long Tick, long Sequence, EntityId2D EntityId);
public abstract record SessionEvent2D(SessionEventStamp2D Stamp);
public sealed record EnemyOccurred2D(SessionEventStamp2D Stamp, EnemyEvent2D Occurrence) : SessionEvent2D(Stamp);
public sealed record CombatDamageOccurred2D(SessionEventStamp2D Stamp, CombatDamage2D Damage) : SessionEvent2D(Stamp);
public sealed record WeaponOccurred2D(SessionEventStamp2D Stamp, WeaponEvent2D Occurrence) : SessionEvent2D(Stamp);
public sealed record JumpStarted2D(SessionEventStamp2D Stamp) : SessionEvent2D(Stamp);
public sealed record Landed2D(SessionEventStamp2D Stamp, float ImpactSpeed) : SessionEvent2D(Stamp);
public sealed record Footstep2D(SessionEventStamp2D Stamp) : SessionEvent2D(Stamp);
public sealed record Damaged2D(SessionEventStamp2D Stamp) : SessionEvent2D(Stamp);
public sealed record Died2D(SessionEventStamp2D Stamp) : SessionEvent2D(Stamp);
public sealed record Respawned2D(SessionEventStamp2D Stamp, Vector2 Position) : SessionEvent2D(Stamp);
public sealed record GoalReached2D(SessionEventStamp2D Stamp) : SessionEvent2D(Stamp);
public sealed record CheckpointActivated2D(
    SessionEventStamp2D Stamp, long CheckpointId, int HitPoints, Vector2 Position) : SessionEvent2D(Stamp);
public sealed record EquipmentChanged2D(SessionEventStamp2D Stamp, string EquipmentId) : SessionEvent2D(Stamp);
public enum PlayerAttackKind2D { Melee, Downward, Shot, Punch, Kick }
public sealed record AttackStarted2D(
    SessionEventStamp2D Stamp, PlayerAttackKind2D Kind,
    float DurationSeconds, bool IsWallGripping) : SessionEvent2D(Stamp);

public readonly record struct RespawnState2D(Vector2 Position, int HitPoints, long? CheckpointId = null);
