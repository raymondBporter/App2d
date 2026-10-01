using App2d.Core.Geometry;

namespace App2d.Core.Shapes;

/// <summary>
/// A filled shape in its own coordinate space. Shapes must remain immutable while attached to a <see cref="SpatialObject2D"/>.
/// Bounds, distance, ray and contact queries live in <see cref="ShapeBounds2D"/>, <see cref="ShapeDistance2D"/> and the collision tables.
/// </summary>
public interface IShape2D : IGeometry2D
{
    /// <summary>The local-space area; infinite shapes report positive infinity.</summary>
    float Area { get; }
}
