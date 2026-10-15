using App2d.Core.Geometry;
using App2d.Core.Validation;
using System.Numerics;

namespace App2d.Core.Shapes;

/// <summary>A rectangle with circular corners: its inset rectangle expanded by <see cref="Radius"/>.</summary>
public sealed partial class RoundedRectangle2D : IConvexShape2D
{
    /// <inheritdoc/>
    public string Kind => ShapeKinds2D.RoundedRectangle;

    private RoundedRectangle2D(Vector2 min, Vector2 max, float radius)
    {
        Min = min;
        Max = max;
        Radius = radius;
    }

    public Vector2 Min { get; }
    public Vector2 Max { get; }
    public float Radius { get; }
    public Vector2 CoreMin => Min + new Vector2(Radius);
    public Vector2 CoreMax => Max - new Vector2(Radius);
    public float Area => Area2D.RoundedRectangle(Min, Max, Radius);
    public bool ContainsPoint(Vector2 point) =>
        Distance2D.SignedDistanceToRectangle(point, CoreMin, CoreMax) <= Radius;
    public Vector2 GetSupportPoint(Vector2 direction) =>
        SupportPoint2D.Circle(direction, SupportPoint2D.Rectangle(direction, CoreMin, CoreMax), Radius);


}
