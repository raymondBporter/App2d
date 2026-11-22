using App2d.Core.Geometry.Primitives;

namespace App2d.Core.Collision.Queries;

/// <summary>A ray hit on an item from a collection query.</summary>
/// <param name="Item">The item that was hit.</param>
/// <param name="Point">The world-space hit point.</param>
/// <param name="Normal">The world-space unit surface normal at the hit.</param>
/// <param name="Distance">The world distance from the ray origin to the hit.</param>
public readonly record struct RaycastHit2D<T>(T Item, Vector2f Point, Vector2f Normal, float Distance) where T : class
{
    internal RaycastHit2D(T item, RayHit2D hit) : this(item, hit.Point, hit.Normal, hit.Distance) { }
}

/// <summary>Decides whether a collection query tests an item at all.</summary>
/// <param name="item">The candidate item.</param>
/// <returns>True to test the item.</returns>
public delegate bool RayQueryFilter2D<in T>(T item) where T : class;
