using App2d.Core.Collision;
using App2d.Core.Geometry.Primitives;
using App2d.Core.Spatial;
using App2d.Core.Validation;
using System.Numerics;

namespace App2d.Core.Characters.Authored;

/// <summary>
/// One attack instance's hit group reaching one target once (P9b): the window (with its strike), the attack instance
/// (the attacker's action sequence), the target's owner id, where the contact is (the centre of the overlap of the attack
/// collider's bounds with the target collider's), and whether the target's guard took it instead of its hurt geometry.
/// </summary>
public readonly record struct AcceptedContact(ResolvedHit Hit, int Sequence, EntityId2D Target, Vector2 Position, string? TargetCollider, bool Blocked)
{
    public string Group => Hit.Group;
    public StrikeDef Strike => Hit.Strike;
}

/// <summary>
/// Per-attacker deduplication: one attack instance (sequence) damages or is blocked by each target at most once per hit
/// group, however many hurt colliders it crosses and however many steps the overlap lasts. Intended repeated pulses are
/// different hit groups. Keys of other sequences are dropped as a new one is seen.
/// </summary>
public sealed class HitLedger
{
    private readonly HashSet<(int Sequence, string Group, EntityId2D Target)> _hits = [];

    public bool TryHit(int sequence, string group, EntityId2D target)
    {
        _hits.RemoveWhere(h => h.Sequence != sequence);
        return _hits.Add((sequence, group, target));
    }

    public void Clear() => _hits.Clear();
}

/// <summary>
/// The one contact resolution over the shared collision queries, for the arena, the game's authored enemies and the
/// player's attacks alike: an attacker's active windows against the targets their attack colliders overlap, found through
/// the ordinary broad phase, deduplicated by attack instance, hit group and target, with the target's enabled guard
/// collider taking a front contact as a block. Gameplay applies the accepted contacts (damage, sound, reactions); a
/// rejected contact (owner, ally, dead target, repeated group) produces nothing here.
/// </summary>
public static class CombatContacts
{
    /// <summary>
    /// Resolves one attacker. <paramref name="targetOf"/> says which overlapped collider belongs to a live target (its owner
    /// id) and whether that collider is the target's guard; <paramref name="extraTargets"/> may add targets found outside
    /// the collision system (the legacy hurt scan) for a window. Contacts are reported in the order targets are first seen.
    /// </summary>
    public static int Resolve(ActorColliders attacker, int sequence, IEnumerable<ResolvedHit> active, uint targetMask, HitLedger ledger,
        Func<AttachedCollider, Collider2D, (EntityId2D Target, bool Guard)?> targetOf, List<AcceptedContact> results,
        List<CollisionOverlap2D> scratch, Action<ResolvedHit, AttachedCollider, List<(EntityId2D Target, Vector2 Position)>>? extraTargets = null)
    {
        ArgGuard.ThrowIfNull(attacker);
        ArgGuard.ThrowIfNull(ledger);
        ArgGuard.ThrowIfNull(results);
        ArgGuard.ThrowIfNull(scratch);
        results.Clear();
        var seen = new List<(EntityId2D Target, Vector2 Position, string? Collider, bool Blocked)>();
        var extra = new List<(EntityId2D Target, Vector2 Position)>();
        foreach (var hit in active)
        {
            var attack = attacker.Find(hit.Collider.Id);
            if (attack is null || !attack.Enabled)
            {
                continue;
            }

            seen.Clear();
            var bounds = attack.Bounds;
            // One-sided by the attack collider's own mask; an attached target's mask (an authored filter) must accept the attack's
            // layer too, while a plain body's mask is a physics setting and does not keep a strike off it.
            attack.Collider.System.Overlap(attack.Object, scratch, targetMask & attack.Collider.CollisionMask, includeSensors: true, attack.Collider);
            foreach (var overlap in scratch)
            {
                if (overlap.Collider.EntityId == attacker.Owner || targetOf(attack, overlap.Collider) is not { } found
                    || overlap.Collider.UserData is AttachedCollider && (overlap.Collider.CollisionMask & attack.Collider.CollisionLayer) == 0u)
                {
                    continue;
                }

                var other = overlap.Collider.WorldObject.WorldBounds;
                var position = (Vector2.Max(bounds.Min, other.Min) + Vector2.Min(bounds.Max, other.Max)) / 2;
                var id = overlap.Collider.UserData is AttachedCollider attached ? attached.Id : null;
                var index = seen.FindIndex(s => s.Target == found.Target);
                if (index < 0)
                {
                    seen.Add((found.Target, position, id, found.Guard));
                }
                else if (found.Guard && !seen[index].Blocked)
                {
                    // The guard wins over the hurt geometry it covers: a blocked contact, where the guard is.
                    seen[index] = (found.Target, position, id, true);
                }
            }
            if (extraTargets is not null)
            {
                extra.Clear();
                extraTargets(hit, attack, extra);
                foreach (var (target, position) in extra)
                {
                    if (seen.All(s => s.Target != target))
                    {
                        seen.Add((target, position, null, false));
                    }
                }
            }
            foreach (var (target, position, collider, blocked) in seen)
            {
                if (ledger.TryHit(sequence, hit.Group, target))
                {
                    results.Add(new(hit, sequence, target, position, collider, blocked));
                }
            }
        }

        return results.Count;
    }

    /// <summary>
    /// A projectile or other placed probe against the registered targets: the first live target by <paramref name="targetOf"/>,
    /// with the contact position, or null. Projectiles deduplicate by being consumed, not by a ledger.
    /// </summary>
    public static (EntityId2D Target, Vector2 Position, bool Blocked)? Probe(CollisionSystem2D system, SpatialObject2D probe, uint targetMask, uint queryLayer,
        Func<Collider2D, (EntityId2D Target, bool Guard)?> targetOf, List<CollisionOverlap2D> scratch, Func<EntityId2D, int>? order = null)
    {
        ArgGuard.ThrowIfNull(system);
        ArgGuard.ThrowIfNull(probe);
        ArgGuard.ThrowIfNull(scratch);
        system.Overlap(probe, scratch, targetMask, includeSensors: true);
        (EntityId2D Target, Vector2 Position, bool Blocked)? best = null;
        var bestOrder = int.MaxValue;
        foreach (var overlap in scratch)
        {
            if (targetOf(overlap.Collider) is not { } found || overlap.Collider.UserData is AttachedCollider && (overlap.Collider.CollisionMask & queryLayer) == 0u)
            {
                continue;
            }

            var rank = order?.Invoke(found.Target) ?? 0;
            if (best is { } current && current.Target == found.Target)
            {
                if (found.Guard && !current.Blocked)
                {
                    best = (found.Target, Centre(probe.WorldBounds, overlap.Collider.WorldObject.WorldBounds), true);
                }

                continue;
            }

            if (best is null || rank < bestOrder)
            {
                best = (found.Target, Centre(probe.WorldBounds, overlap.Collider.WorldObject.WorldBounds), found.Guard);
                bestOrder = rank;
            }
        }

        return best;
    }

    private static Vector2 Centre(Rect2D a, Rect2D b) => (Vector2.Max(a.Min, b.Min) + Vector2.Min(a.Max, b.Max)) / 2;

    /// <summary>Whether an attack from <paramref name="attackerX"/> meets a guard facing <paramref name="facing"/> from the front.</summary>
    public static bool FromFront(float attackerX, float targetX, int facing) => (attackerX - targetX) * facing > 0;
}
