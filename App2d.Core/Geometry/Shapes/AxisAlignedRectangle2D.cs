using App2d.Core.Geometry.Operations.Vertices;
using App2d.Core.Geometry.Primitives;
using App2d.Core.Geometry.Shapes;
using System.Numerics;

namespace App2d.Core.Shapes;

/// <summary>A rectangle whose owner intends to keep its world axes aligned.</summary>
public readonly partial record struct AxisAlignedRectangle2D : IConvexShape2D, IRect2D
{
    private readonly Rectangle2D _rectangle;

    public AxisAlignedRectangle2D(Vector2 min, Vector2 max)
    {
        _rectangle = Rectangle2D.Builder.Create(min, max);
    }

    public string Kind => _rectangle.Kind;
    public Vector2 Min => _rectangle.Min;
    public Vector2 Max => _rectangle.Max;
    public float Area => _rectangle.Area;
    public float Diameter => _rectangle.Diameter;
    public bool ContainsPoint(Vector2 localPoint) => _rectangle.ContainsPoint(localPoint);
    public Vector2 GetSupportPoint(Vector2 localDirection) => _rectangle.GetSupportPoint(localDirection);
    public void WriteCorners(Span<Vector2> corners) => VertexGenerator2D.WriteRectangle(corners, Min, Max);
}
