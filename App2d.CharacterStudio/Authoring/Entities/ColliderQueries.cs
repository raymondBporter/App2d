using App2d.Core;
using App2d.Core.Characters.Authored;
using App2d.Core.Collision;
using App2d.Core.Spatial;
using App2d.Core.Validation;

namespace App2d.CharacterStudio.Authoring;

/// <summary>
/// Overlap queries over attached colliders through the ordinary broad phase. The filter is reconciled with collider-pair
/// collection: both masks count (the target's layer must be in the query's mask and the target's mask must accept the
/// query's layer, as <see cref="Collider2D.CanCollideWith"/> requires of a pair), the owner's own colliders are excluded
/// and the use is the category; gameplay rules (faction, alive, hit groups) stay with the caller.
/// </summary>
public static class ColliderQueries
{
    /// <summary>
    /// The enabled attached colliders of <paramref name="use"/> on other owners that overlap the placed query shape, in
    /// broad-phase order. <paramref name="owner"/>'s own colliders are excluded (self filtering); <paramref name="layerMask"/>
    /// selects the owners' gameplay layers (ally filtering) and <paramref name="queryLayer"/> is the layer the query itself
    /// is on, which the targets' masks must accept (an undeclared query is one-sided); <paramref name="accept"/> may refuse further.
    /// </summary>
    public static int Overlap(CollisionSystem2D system, SpatialObject2D query, EntityId2D owner, string use, List<AttachedCollider> results, List<CollisionOverlap2D> scratch, uint layerMask = uint.MaxValue, Func<AttachedCollider, bool>? accept = null, Collider2D? excluded = null, uint? queryLayer = null)
    {
        ArgGuard.ThrowIfNull(system);
        ArgGuard.ThrowIfNull(results);
        ArgGuard.ThrowIfNull(scratch);
        results.Clear();
        system.Overlap(query, scratch, layerMask, includeSensors: true, excluded, queryLayer);
        foreach (var overlap in scratch)
        {
            if (overlap.Collider.UserData is AttachedCollider attached && attached.Use == use && attached.Owner != owner && (accept?.Invoke(attached) ?? true))
            {
                results.Add(attached);
            }
        }

        return results.Count;
    }

    /// <summary>The same query from a registered collider: it is excluded itself, its owner's colliders are skipped, its mask and layer are the query's.</summary>
    public static int Overlap(AttachedCollider query, string use, List<AttachedCollider> results, List<CollisionOverlap2D> scratch, uint layerMask = uint.MaxValue, Func<AttachedCollider, bool>? accept = null)
    {
        ArgGuard.ThrowIfNull(query);
        return Overlap(query.Collider.System, query.Object, query.Owner, use, results, scratch, layerMask & query.Collider.CollisionMask, accept, query.Collider, query.Collider.CollisionLayer);
    }
}
