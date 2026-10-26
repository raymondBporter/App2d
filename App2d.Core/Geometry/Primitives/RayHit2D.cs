using System.Numerics;

namespace App2d.Core.Geometry.Primitives;

/// <summary>The nearest boundary or curve intersection reported by a ray query.</summary>
/// <param name="Point">The hit point, origin + direction * Distance.</param>
/// <param name="Normal">A unit surface normal: outward for solids, counterclockwise from increasing progress for local curve queries.</param>
/// <param name="Distance">The ray parameter of the hit; a distance in input units when the direction is unit length.</param>
public readonly record struct RayHit2D(Vector2 Point, Vector2 Normal, float Distance);
