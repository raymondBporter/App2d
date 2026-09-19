using App2d.Core;
using System.Collections.Immutable;
using System.Numerics;

namespace App2d.Gameplay.Persons.Actions;

/// <summary>Client observations, not a complete simulation restore point.</summary>
public readonly record struct ProjectileState2D(
    EntityId2D Id, Vector2 Position, Vector2 Velocity, Vector2 Origin);

public readonly record struct WeaponState2D(
    bool IsCharging, float ChargeProgress, Vector2 MuzzlePosition,
    ImmutableArray<ProjectileState2D> Projectiles)
{
    public static WeaponState2D Empty => new(false, 0f, Vector2.Zero, []);
}

// Facts carry their occurrence position, even when the source moves later in the tick.
public abstract record WeaponEvent2D(Vector2 Position);
public sealed record ChargeStarted2D(Vector2 Position) : WeaponEvent2D(Position);
public sealed record ChargeCancelled2D(Vector2 Position, float Progress) : WeaponEvent2D(Position);
public sealed record GunFired2D(Vector2 Position) : WeaponEvent2D(Position);
public sealed record ProjectileImpact2D(Vector2 Position, EntityId2D ProjectileId) : WeaponEvent2D(Position);
public sealed record SwordImpact2D(Vector2 Position) : WeaponEvent2D(Position);
