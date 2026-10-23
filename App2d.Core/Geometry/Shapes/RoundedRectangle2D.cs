using App2d.Core.Geometry;
using App2d.Core.Geometry.Operations.Convex;
using App2d.Core.Geometry.Operations.Diameter;
using App2d.Core.Geometry.Operations.Vertices;
using App2d.Core.Geometry.Primitives;
using App2d.Core.Geometry.Shapes;
using System.Numerics;

namespace App2d.Core.Shapes;

/// <summary>
/// A rectangle with circular corners and its own orientation in local space: the inset rectangle at
/// <see cref="InnerHalfSize"/> expanded by <see cref="Radius"/>, in the frame of <see cref="Center"/> and <see cref="Axis"/>.
/// </summary>
public readonly partial record struct RoundedRectangle2D : IConvexShape2D
{
    private readonly Vector2 _axis;

    /// <inheritdoc/>
    public string Kind => ShapeKinds2D.RoundedRectangle;

    /// <summary>Creates a rounded rectangle from its center, outer half extents, corner radius and axis.</summary>
    /// <param name="center">The finite local center.</param>
    /// <param name="halfSize">Finite, positive outer half extents along the shape's own axes.</param>
    /// <param name="radius">The corner radius, at most the smaller half extent.</param>
    /// <param name="axis">The unit direction of the shape's local +X.</param>
    public RoundedRectangle2D(Vector2 center, Vector2 halfSize, float radius, Vector2 axis)
    {
        Center = center;
        HalfSize = halfSize;
        Radius = radius;
        _axis = axis;
    }

    /// <summary>The local center.</summary>
    public Vector2 Center { get; init; }

    /// <summary>Half the outer width and height, measured along the shape's own axes.</summary>
    public Vector2 HalfSize { get; init; }

    /// <summary>The corner radius.</summary>
    public float Radius { get; init; }

    /// <summary>The unit direction of the shape's local +X. An unset axis reads as local +X.</summary>
    public Vector2 Axis
    {
        get => OrientedFrame2D.AxisOrUnitX(_axis);
        init => _axis = value;
    }

    /// <summary>Value equality on the effective axis, so an unset axis equals an explicit +X.</summary>
    public bool Equals(RoundedRectangle2D other) => Center.Equals(other.Center) && HalfSize.Equals(other.HalfSize) && Radius.Equals(other.Radius) && Axis.Equals(other.Axis);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(Center, HalfSize, Radius, Axis);

    /// <summary>Half extents of the inset rectangle whose expansion by <see cref="Radius"/> forms the shape. It may collapse to a segment or point.</summary>
    public Vector2 InnerHalfSize => HalfSize - new Vector2(Radius);

    /// <summary>The full outer width and height along the shape's own axes.</summary>
    public Vector2 Size => HalfSize * 2f;

    /// <summary>The heading of <see cref="Axis"/> in radians, counter-clockwise from local +X.</summary>
    public float AngleRadians => Frame.AngleRadians;

    /// <summary>True when the shape's axes coincide with the local axes.</summary>
    public bool IsAxisAligned => Frame.IsAxisAligned;

    /// <summary>The frame every query projects through.</summary>
    public OrientedFrame2D Frame => new(Center, Axis);

    /// <inheritdoc/>
    public float Area => Area2D.RoundedRectangle(-HalfSize, HalfSize, Radius);

    /// <inheritdoc/>
    public float Diameter => Diameter2D.RoundedRectangle(-InnerHalfSize, InnerHalfSize, Radius);

    /// <inheritdoc/>
    public bool ContainsPoint(Vector2 point) => Distance2D.SignedDistanceToRectangle(Frame.ToFrame(point), -InnerHalfSize, InnerHalfSize) <= Radius;

    /// <inheritdoc/>
    public Vector2 GetSupportPoint(Vector2 direction)
    {
        var frame = Frame;
        var frameDirection = frame.ToFrameDirection(direction);
        return frame.FromFrame(SupportPoint2D.Circle(frameDirection, SupportPoint2D.Rectangle(frameDirection, -InnerHalfSize, InnerHalfSize), Radius));
    }

    /// <summary>
    /// Writes the local-space corners of the inset rectangle: one point when both inner extents are zero, the two ends
    /// when one is, otherwise four corners counter-clockwise from the corner at negative inner extents.
    /// </summary>
    /// <param name="corners">A buffer of at least four entries; only the returned count is written.</param>
    /// <returns>The number of corners written.</returns>
    public int WriteInnerCorners(Span<Vector2> corners)
    {
        var inner = InnerHalfSize;
        int count;
        if (inner == Vector2.Zero)
        {
            corners[0] = Vector2.Zero;
            count = 1;
        }
        else if (inner.X == 0f || inner.Y == 0f)
        {
            corners[0] = -inner;
            corners[1] = inner;
            count = 2;
        }
        else
        {
            VertexGenerator2D.WriteRectangle(corners, -inner, inner);
            count = 4;
        }

        Frame.MapFromFrame(corners[..count]);
        return count;
    }
}
