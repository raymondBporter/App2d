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

/// <summary>Why a session refused an input. The session state is unchanged after a rejection.</summary>
public enum InputRejection2D
{
    None,
    UnknownPlayer,
    WrongTick,
    StaleSequence,
    InvalidMovement,
    DuplicatePlayer,
}

/// <summary>Why a client endpoint refused a frame. The endpoint state is unchanged after a rejection.</summary>
public enum FrameRejection2D
{
    None,
    /// <summary>The frame is not newer than the current observation. Not an error.</summary>
    Stale,
    Gap,
    MissingPlayer,
    Incomplete,
    AcknowledgementMovedBackwards,
}

/// <summary>One player's observation, including the newest input sequence the session has applied.</summary>
public readonly record struct PlayerState2D(
    PersonState2D Person, long LastInputSequence, float MoveX, EquipmentKind2D Equipment,
    bool IsMeleeAttackActive,
    float RespawnSeconds, long? CheckpointId, bool ReachedGoal, WeaponState2D Weapons = default)
{
    public EntityId2D Id => Person.Id;
}

/// <summary>A complete starting observation. Historical events are intentionally absent.</summary>
public sealed record SessionSnapshot2D(
    long Tick, ImmutableArray<PlayerState2D> Players,
    LevelContent2D Content, WorldState2D World, ImmutableArray<EnemyState2D> Enemies)
{
    public PlayerState2D? FindPlayer(EntityId2D id) => Players.FindPlayer(id);
}

/// <summary>Owned value data; advancing the session cannot change an earlier frame.</summary>
public sealed record SessionFrame2D(
    long Tick, ImmutableArray<PlayerState2D> Players,
    ImmutableArray<SessionEvent2D> Events)
{
    /// <summary>Content the receiver may already hold; compare <see cref="LevelContent2D.Revision"/>.</summary>
    public LevelContent2D Content { get; init; } = LevelContent2D.Empty;
    public WorldState2D World { get; init; } = WorldState2D.Empty;
    public ImmutableArray<EnemyState2D> Enemies { get; init; } = [];
    public PlayerState2D? FindPlayer(EntityId2D id) => Players.FindPlayer(id);
}

public static class PlayerStateCollections2D
{
    public static PlayerState2D? FindPlayer(this ImmutableArray<PlayerState2D> players, EntityId2D id)
    {
        if (players.IsDefault) return null;
        foreach (var player in players)
            if (player.Id == id) return player;
        return null;
    }
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
public sealed record EquipmentChanged2D(SessionEventStamp2D Stamp, EquipmentKind2D Equipment) : SessionEvent2D(Stamp);
public enum PlayerAttackKind2D { Melee, Downward, Shot, Punch, Kick }
public sealed record AttackStarted2D(
    SessionEventStamp2D Stamp, PlayerAttackKind2D Kind,
    float DurationSeconds, bool IsWallGripping) : SessionEvent2D(Stamp);

public readonly record struct RespawnState2D(Vector2 Position, int HitPoints, long? CheckpointId = null);
