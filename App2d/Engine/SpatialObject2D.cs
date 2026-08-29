using System.Numerics;
using App2d.Engine.Geometry;
using App2d.Engine.Mathematics;

namespace App2d.Engine;

/// <summary>
/// A render-agnostic shape placed in world space.
/// </summary>
public class SpatialObject2D(IShape2D shape)
{
    public Transform2D Transform { get; } = new();
    public IShape2D Shape { get; } = ArgGuard.RequireNotNull(shape);

    public Bounds2D WorldBounds =>
        Shape.LocalBounds.TransformedBy(Transform.LocalToWorldMatrix);

    public bool ContainsWorldPoint(Vector2 worldPoint)
    {
        if (!Matrix3x2.Invert(Transform.LocalToWorldMatrix, out var worldToLocal))
            return false;

        return Shape.ContainsPoint(Vector2.Transform(worldPoint, worldToLocal));
    }
}
