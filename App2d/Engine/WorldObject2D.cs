using System.Numerics;
using App2d.Engine.Geometry;
using App2d.Engine.Mathematics;
using App2d.Engine.Rendering;

namespace App2d.Engine;

public sealed class WorldObject2D(IShape2D shape, IShader2D shader)
{
    public Transform2D Transform { get; } = new();
    public IShape2D Shape { get; } = shape;
    public IShader2D Shader { get; set; } = shader;
    public bool IsVisible { get; set; } = true;
    public Bounds2D WorldBounds =>
        Shape.LocalBounds.TransformedBy(Transform.LocalToWorldMatrix);

    public bool ContainsWorldPoint(Vector2 worldPoint)
    {
        if (!Matrix3x2.Invert(Transform.LocalToWorldMatrix, out var worldToLocal))
            return false;

        return Shape.ContainsPoint(Vector2.Transform(worldPoint, worldToLocal));
    }
}
