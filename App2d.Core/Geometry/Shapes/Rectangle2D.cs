using App2d.Core.Geometry;
using App2d.Core.Geometry.Operations.Containment;
using App2d.Core.Geometry.Operations.Convex;
using App2d.Core.Geometry.Operations.Diameter;
using App2d.Core.Geometry.Operations.Vertices;
using App2d.Core.Geometry.Primitives;
using App2d.Core.Geometry.Shapes;
using System.Numerics;

namespace App2d.Core.Shapes;

/// <summary>A filled rectangle, axis-aligned in local space; the owning pose may rotate it in world space.</summary>
public partial class Rectangle2D : IConvexShape2D, IRect2D
{
    /// <inheritdoc/>
    public string Kind => ShapeKinds2D.Rectangle;

    /// <summary>Creates a rectangle from ordered corners with positive extents.</summary>
    /// <param name="min">The finite lower-left corner.</param>
    /// <param name="max">The finite upper-right corner, component-wise greater than <paramref name="min"/>.</param>
    private protected Rectangle2D(Vector2 min, Vector2 max)
    {
        Min = min;
        Max = max;
    }

    /// <inheritdoc/>
    public Vector2 Min
    {
        get;
    }

    /// <inheritdoc/>
    public Vector2 Max
    {
        get;
    }

    /// <inheritdoc/>
    public float Area => Area2D.Rectangle(Min, Max);

    /// <inheritdoc/>
    public float Diameter => Diameter2D.Rectangle(Min, Max);

    /// <inheritdoc/>
    public bool ContainsPoint(Vector2 localPoint) => Containment2D.Rectangle(localPoint, Min, Max);

    /// <inheritdoc/>
    public Vector2 GetSupportPoint(Vector2 localDirection) => SupportPoint2D.Rectangle(localDirection, Min, Max);

    /// <summary>Writes the four local-space corners counter-clockwise from Min.</summary>
    /// <param name="corners">A buffer of at least four entries.</param>
    public void WriteCorners(Span<Vector2> corners) => VertexGenerator2D.WriteRectangle(corners, Min, Max);

}
