using System.Numerics;

namespace App2d.Core.Geometry;

/// <summary>Where a ray first enters a primitive.</summary>
/// <param name="Point">The hit point, origin + direction * Distance.</param>
/// <param name="Normal">The unit outward surface normal at the hit.</param>
/// <param name="Distance">The ray parameter of the hit; a distance in input units when the direction is unit length.</param>
public readonly record struct RayHit2D(Vector2 Point, Vector2 Normal, float Distance);
