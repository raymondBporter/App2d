using App2d.Core.Geometry;

namespace App2d.Core.Shapes;

/// <summary>A finite convex shape. Its support mapping powers bounds, distances and contacts without sampling curves.</summary>
public interface IConvexShape2D : IShape2D, IConvexSupport2D
{
}
