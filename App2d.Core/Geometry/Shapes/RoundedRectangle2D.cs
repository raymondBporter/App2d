using App2d.Core.Geometry;
using App2d.Core.Geometry.Operations.Convex;
using App2d.Core.Geometry.Operations.Diameter;
using App2d.Core.Geometry.Primitives;
using App2d.Core.Geometry.Shapes;
using System.Numerics;

namespace App2d.Core.Shapes;

/// <summary>A rectangle with circular corners: its inset rectangle expanded by <see cref="Radius"/>.</summary>
public sealed partial class RoundedRectangle2D : IConvexShape2D
{
    /// <inheritdoc/>
    public string Kind => ShapeKinds2D.RoundedRectangle;

    private RoundedRectangle2D(Vector2 min, Vector2 max, float radius)
    {
        OuterRect = new(min, max);
        Radius = radius;
    }

    /// <summary>The outer axis-aligned rectangle containing the rounded shape.</summary>
    public Rect2D OuterRect
    {
        get;
    }
    /// <summary>The inset rectangle expanded by Radius to form the rounded shape. It may collapse to a segment or point.</summary>
    public Rect2D InnerRect => new(Min + new Vector2(Radius), Max - new Vector2(Radius));
    public Vector2 Min => OuterRect.Min;
    public Vector2 Max => OuterRect.Max;
    public float Radius
    {
        get;
    }
    public float Area => Area2D.RoundedRectangle(Min, Max, Radius);

    /// <inheritdoc/>
    public float Diameter => Diameter2D.RoundedRectangle(InnerRect.Min, InnerRect.Max, Radius);
    public bool ContainsPoint(Vector2 point) => Distance2D.SignedDistanceToRectangle(point, InnerRect.Min, InnerRect.Max) <= Radius;
    public Vector2 GetSupportPoint(Vector2 direction) => SupportPoint2D.Circle(direction, SupportPoint2D.Rectangle(direction, InnerRect.Min, InnerRect.Max), Radius);

}
