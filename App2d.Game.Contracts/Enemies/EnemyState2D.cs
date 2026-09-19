using App2d.Core;
using App2d.Gameplay.Persons;
using App2d.Gameplay.Persons.Actions;
using System.Numerics;

namespace App2d.Gameplay.Enemies;

public enum EnemyKind2D { Shieldback, GreenDinosaur, BoilerBrute, Rival, TumbleProp }

/// <summary>Owned client observation; no actor, physics, or presentation references.</summary>
public readonly record struct EnemyState2D(
    EntityId2D Id, EnemyKind2D Kind, Vector2 Position, Vector2 Velocity,
    float Rotation, float Facing, bool IsEnabled, bool IsAlive)
{
    public float MoveSpeed { get; init; }
    public bool IsAttacking { get; init; }
    public float AttackElapsedSeconds { get; init; }
    public PersonState2D Person { get; init; }
    public float MoveX { get; init; }
}

public abstract record EnemyEvent2D(EntityId2D EntityId, Vector2 Position);
public sealed record HammerStarted2D(EntityId2D EntityId, Vector2 Position) : EnemyEvent2D(EntityId, Position);
public sealed record HammerStruck2D(EntityId2D EntityId, Vector2 Position) : EnemyEvent2D(EntityId, Position);
public sealed record RivalAttackStarted2D(EntityId2D EntityId, Vector2 Position,
    UnarmedAttackKind2D Kind, float DurationSeconds) : EnemyEvent2D(EntityId, Position);
public sealed record RivalDamaged2D(EntityId2D EntityId, Vector2 Position) : EnemyEvent2D(EntityId, Position);
public sealed record RivalDied2D(EntityId2D EntityId, Vector2 Position) : EnemyEvent2D(EntityId, Position);
