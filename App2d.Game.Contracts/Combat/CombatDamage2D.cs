using App2d.Core;
using System.Numerics;

namespace App2d.Gameplay.Combat;

public readonly record struct CombatDamage2D(
    EntityId2D TargetId, CombatFaction2D Faction, Vector2 Position, bool WasKilled);
