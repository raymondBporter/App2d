using System.Numerics;

namespace App2d.Core.Geometry.Shapes;

/// <summary>
/// Geometry in its own coordinate space, including unbounded lines and rays.
/// No transform, bounds cache, area, or convex support mapping is required.
/// </summary>
public interface IGeometry2D
{
    bool ContainsPoint(Vector2 point);
}
