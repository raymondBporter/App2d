using App2d.Contracts.Persons;
using App2d.Contracts.Persons.Actions;
using App2d.Core;
using App2d.Core.Characters.Authored;
using System.Numerics;

namespace App2d.Contracts.Enemies;

public enum EnemyKind2D { Shieldback, GreenDinosaur, BoilerBrute, Rival, TumbleProp, Authored }

/// <summary>Owned client observation; no actor, physics, or presentation references.</summary>
public readonly record struct EnemyState2D(
    EntityId2D Id, EnemyKind2D Kind, Vector2 Position, Vector2 Velocity,
    float Rotation, float Facing, bool IsEnabled, bool IsAlive)
{
    public string? TypeId { get; init; }
    public string? ActionId { get; init; }
    public float ActionSeconds { get; init; }
    public System.Collections.Immutable.ImmutableArray<EntityBoltState2D> Bolts { get; init; } = [];
    public float MoveSpeed { get; init; }
    public bool IsAttacking { get; init; }
    public float AttackElapsedSeconds { get; init; }
    /// <summary>Authoritative duration for attack animation; zero when this state has no timed attack.</summary>
    public float AttackDurationSeconds { get; init; }
    public PersonState2D Person { get; init; }
    public float MoveX { get; init; }
    /// <summary>For enemies compiled from authored entities: the shared compiled entity and this tick's final pose, drawn as is.</summary>
    public ResolvedEntity? AuthoredEntity { get; init; }
    public ActorPose? AuthoredPose { get; init; }
}

public readonly record struct EntityBoltState2D(Vector2 Position, Vector2 Velocity, Vector2 Size, float Lifetime)
{
    public float Gravity { get; init; }
}
public sealed record EntityCue2D(EntityId2D EntityId, Vector2 Position, string Cue) : EnemyEvent2D(EntityId, Position);

public abstract record EnemyEvent2D(EntityId2D EntityId, Vector2 Position);
public sealed record HammerStarted2D(EntityId2D EntityId, Vector2 Position) : EnemyEvent2D(EntityId, Position);
public sealed record HammerStruck2D(EntityId2D EntityId, Vector2 Position) : EnemyEvent2D(EntityId, Position);
public sealed record RivalAttackStarted2D(EntityId2D EntityId, Vector2 Position,
    UnarmedAttackKind2D Kind, float DurationSeconds) : EnemyEvent2D(EntityId, Position);
public sealed record RivalDamaged2D(EntityId2D EntityId, Vector2 Position) : EnemyEvent2D(EntityId, Position);
public sealed record RivalDied2D(EntityId2D EntityId, Vector2 Position) : EnemyEvent2D(EntityId, Position);
