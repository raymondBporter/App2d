namespace App2d.Core.Geometry.Operations.Convex;

/// <summary>A finite convex set described by its exact support mapping.</summary>
public interface IConvexSupport2D
{
    /// <summary>The point maximizing the projection onto a local direction; the direction need not be normalized.</summary>
    Vector2f GetSupportPoint(Vector2f localDirection);
}
