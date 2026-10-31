using App2d.Core.Geometry;
using App2d.Core.Geometry.Operations.Containment;
using App2d.Core.Geometry.Operations.Convex;
using App2d.Core.Geometry.Operations.Diameter;
using App2d.Core.Geometry.Operations.Vertices;
using App2d.Core.Geometry.Primitives;
using App2d.Core.Geometry.Shapes;
using System.Numerics;

namespace App2d.Core.Shapes;

/// <summary>A rectangle whose owner keeps its world axes aligned: the one shape that never rotates, in local space or through a pose.</summary>
public readonly partial record struct AxisAlignedRectangle2D : IConvexShape2D, IRect2D
{
    /// <summary>The rectangle from (0, 0) to (1, 1), covering a whole image in normalized coordinates.</summary>
    public static AxisAlignedRectangle2D UnitRect { get; } = Builder.Create(Vector2.Zero, Vector2.One);

    public AxisAlignedRectangle2D(Vector2 min, Vector2 max)
    {
        Min = min;
        Max = max;
    }

    public Vector2 Min { get; init; }
    public Vector2 Max { get; init; }

    /// <summary>Checks a normalized image region measured from the top left.</summary>
    public void ValidateUvRect(string field = "UV region")
    {
        if (!this.IsFinite || this.Width < .00001f || this.Height < .00001f || !UnitRect.Contains(this))
            throw new InvalidDataException(field + ": region must have positive width and height and lie inside the unit image rectangle.");
    }

    public float Area => Area2D.Rectangle(Min, Max);
    public float Diameter => Diameter2D.Rectangle(Min, Max);
    public bool ContainsPoint(Vector2 localPoint) => Containment2D.Rectangle(localPoint, Min, Max);
    public Vector2 GetSupportPoint(Vector2 localDirection) => SupportPoint2D.Rectangle(localDirection, Min, Max);
    public void WriteCorners(Span<Vector2> corners) => VertexGenerator2D.WriteRectangle(corners, Min, Max);
}
