using App2d.Core.Geometry.Operations.Bounds;
using App2d.Core.Geometry.Operations.Diameter;
using App2d.Core.Geometry.Operations.Distance;
using App2d.Core.Geometry.Primitives;
using App2d.Core.Spatial;

namespace App2d.Core.Geometry.Shapes;

/// <summary>
/// A filled shape in its own coordinate space. Shapes must remain immutable while attached to a <see cref="SpatialObject2D"/>.
/// Bounds, distance, ray and contact queries live in <see cref="ShapeBounds2D"/>, <see cref="ShapeDistance2D"/> and the collision tables.
/// </summary>
public interface IShape2D : IGeometry2D
{
    /// <summary>The stable shape kind; built-in shapes use <see cref="ShapeKinds2D"/> IDs.</summary>
    string Kind
    {
        get;
    }

    /// <summary>The local-space area; infinite shapes report positive infinity.</summary>
    float Area
    {
        get;
    }

    /// <summary>The local-space supremum of |a - b| for points in this shape. Unbounded shapes report positive infinity.</summary>
    float Diameter => ShapeDiameter2D.Calculate(this);
}
