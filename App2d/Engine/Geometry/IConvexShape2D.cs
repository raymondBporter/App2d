using System.Numerics;

namespace App2d.Engine.Geometry;

public interface IConvexShape2D : IShape2D
{
    // Farthest local-space point in a direction. This powers generic convex queries.
    Vector2 GetSupportPoint(Vector2 localDirection);
}
