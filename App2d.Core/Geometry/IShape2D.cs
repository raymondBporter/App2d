using System.Numerics;

namespace App2d.Engine.Geometry;

public interface IShape2D
{
    Bounds2D LocalBounds { get; }

    /// <summary>Local-space area; infinite shapes report positive infinity.</summary>
    float Area { get; }

    bool ContainsPoint(Vector2 localPoint);
}
