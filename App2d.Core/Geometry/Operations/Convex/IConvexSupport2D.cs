using System.Numerics;

namespace App2d.Core.Geometry;

/// <summary>A finite convex set described by its exact support mapping.</summary>
public interface IConvexSupport2D
{
    /// <summary>The point maximizing the projection onto a local direction; the direction need not be normalized.</summary>
    Vector2 GetSupportPoint(Vector2 localDirection);
}
