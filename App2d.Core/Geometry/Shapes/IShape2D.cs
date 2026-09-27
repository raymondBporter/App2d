using System.Numerics;

namespace App2d.Core.Geometry.Shapes;

/// <summary>Shape geometry. Geometry must remain immutable while attached to a SpatialObject2D.</summary>
public interface IShape2D
{
    /// <summary>Local-space area; infinite shapes report positive infinity.</summary>
    float Area { get; }

    bool ContainsPoint(Vector2 localPoint);
}
