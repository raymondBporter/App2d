using App2d.Core.Geometry.Containment;
using App2d.Core.Geometry.Convex;
using App2d.Core.Geometry.Measurements;
using App2d.Core.Geometry.Primitives;

namespace App2d.Core.Geometry.Shapes;

/// <summary>A rectangle whose owner keeps its world axes aligned: the one shape that never rotates, in local space or through a pose.</summary>
public readonly partial record struct AxisAlignedRectangle2D : IConvexShape2D, IRect2f
{
    /// <summary>The rectangle from (0, 0) to (1, 1), covering a whole image in normalized coordinates.</summary>
    public static AxisAlignedRectangle2D UnitRect { get; } = Builder.Create(Vector2f.Zero, Vector2f.One);

    public AxisAlignedRectangle2D(Vector2f min, Vector2f max)
    {
        Min = min;
        Max = max;
    }

    public Vector2f Min { get; init; }
    public Vector2f Max { get; init; }

    /// <summary>Checks a normalized image region measured from the top left.</summary>
    public void ValidateUvRect(string field = "UV region")
    {
        if (!this.IsFinite || this.Width < .00001f || this.Height < .00001f || !UnitRect.Contains(this))
            throw new InvalidDataException(field + ": region must have positive width and height and lie inside the unit image rectangle.");
    }

    public float Area => Area2D.Rectangle(Min, Max);
    public float Diameter => Diameter2D.Rectangle(Min, Max);
    public bool ContainsPoint(Vector2f localPoint) => Containment2D.Rectangle(localPoint, Min, Max);
    public Vector2f GetSupportPoint(Vector2f localDirection) => SupportPoint2D.Rectangle(localDirection, Min, Max);
    public void WriteCorners(Span<Vector2f> corners) => VertexGenerator2D.WriteRectangle(corners, Min, Max);
}
