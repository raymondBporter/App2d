using App2d.Core;
using System.Numerics;

namespace App2d.Contracts.Combat;

public readonly record struct CombatDamage2D(
    EntityId2D TargetId, CombatFaction2D Faction, Vector2 Position, bool WasKilled)
{
    public CombatContact2D? Contact { get; init; }
}

public enum CombatImpactKind2D { Generic, Sword }

/// <summary>A confirmed strike. Position is inside the overlapping hurt region, before the reaction changes its pose.</summary>
public readonly record struct CombatContact2D(EntityId2D SourceId, int AttackId, Vector2 Position,
    Vector2 Direction, CombatImpactKind2D Kind)
{
    /// <summary>The actor wielding the attack; SourceId can identify a separate weapon for hit deduplication.</summary>
    public EntityId2D AttackerId { get; init; } = SourceId;
}
