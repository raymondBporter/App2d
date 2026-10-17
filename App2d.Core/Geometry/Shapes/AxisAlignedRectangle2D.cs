using App2d.Core.Geometry.Operations.Vertices;
using App2d.Core.Geometry.Primitives;
using App2d.Core.Geometry.Shapes;
using System.Numerics;

namespace App2d.Core.Shapes;

/// <summary>A rectangle whose owner intends to keep its world axes aligned.</summary>
public readonly partial record struct AxisAlignedRectangle2D : IConvexShape2D, IRect2D
{
    public AxisAlignedRectangle2D(Vector2 min, Vector2 max)
    {
        Min = min;
        Max = max;
    }

    // Delegating queries does not construct validated geometry.
#pragma warning disable APP2D001
    private Rectangle2D Rectangle => new(Min, Max);
#pragma warning restore APP2D001

    public string Kind => ShapeKinds2D.AxisAlignedRectangle;
    public Vector2 Min { get; init; }
    public Vector2 Max { get; init; }
    public float Area => Rectangle.Area;
    public float Diameter => Rectangle.Diameter;
    public bool ContainsPoint(Vector2 localPoint) => Rectangle.ContainsPoint(localPoint);
    public Vector2 GetSupportPoint(Vector2 localDirection) => Rectangle.GetSupportPoint(localDirection);
    public void WriteCorners(Span<Vector2> corners) => VertexGenerator2D.WriteRectangle(corners, Min, Max);
}
