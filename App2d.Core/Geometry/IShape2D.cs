using System.Numerics;

namespace App2d.Engine.Geometry;

public interface IShape2D
{
    Bounds2D LocalBounds { get; }
    bool ContainsPoint(Vector2 localPoint);
}
