using System.Numerics;

namespace App2d.Core.Geometry;

/// <summary>
/// Geometry in its own coordinate space, including unbounded lines and rays.
/// No transform, bounds cache, area or convex support mapping is required.
/// </summary>
public interface IGeometry2D
{
    /// <summary>Tests whether a point lies in or on this geometry.</summary>
    /// <param name="point">A point in the geometry's own coordinate space.</param>
    /// <returns>True when the point is inside or on the boundary.</returns>
    bool ContainsPoint(Vector2 point);
}
