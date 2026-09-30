using System.Numerics;

namespace App2d.Core.Shapes;

/// <summary>A finite convex shape. The support mapping powers half-space contacts and generic convex bounds.</summary>
public interface IConvexShape2D : IShape2D
{
    /// <summary>The farthest local-space point in a direction.</summary>
    /// <param name="localDirection">The query direction in local space; need not be unit length.</param>
    /// <returns>A boundary point maximizing the projection onto the direction.</returns>
    Vector2 GetSupportPoint(Vector2 localDirection);
}
