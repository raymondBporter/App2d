namespace App2d.Core.Shapes;

/// <summary>Shape geometry. Geometry must remain immutable while attached to a SpatialObject2D.</summary>
public interface IShape2D : IGeometry2D
{
    /// <summary>Local-space area; infinite shapes report positive infinity.</summary>
    float Area { get; }
}
