using App2d.Core.Geometry;
using App2d.Core.Geometry.Operations.Containment;
using App2d.Core.Geometry.Operations.Convex;
using App2d.Core.Geometry.Operations.Diameter;
using App2d.Core.Geometry.Operations.Vertices;
using App2d.Core.Geometry.Primitives;
using App2d.Core.Geometry.Shapes;
using System.Numerics;

namespace App2d.Core.Shapes;

/// <summary>
/// A filled rectangle with its own orientation in local space: a center, half extents along its own axes, and a unit
/// <see cref="Axis"/> for its local +X. Every query projects into that frame and runs the axis-aligned math there.
/// A rectangle that must stay aligned with the world axes is an <see cref="AxisAlignedRectangle2D"/>.
/// </summary>
public readonly partial record struct Rectangle2D : IConvexShape2D
{
    private readonly Vector2 _axis;

    /// <inheritdoc/>
    public string Kind => ShapeKinds2D.Rectangle;

    /// <summary>Creates a rectangle from its center, half extents and axis.</summary>
    /// <param name="center">The finite local center.</param>
    /// <param name="halfSize">Finite, positive half extents along the rectangle's own axes.</param>
    /// <param name="axis">The unit direction of the rectangle's local +X.</param>
    public Rectangle2D(Vector2 center, Vector2 halfSize, Vector2 axis)
    {
        Center = center;
        HalfSize = halfSize;
        _axis = axis;
    }

    /// <summary>The local center.</summary>
    public Vector2 Center { get; init; }

    /// <summary>Half the width and height, measured along the rectangle's own axes.</summary>
    public Vector2 HalfSize { get; init; }

    /// <summary>The unit direction of the rectangle's local +X. An unset axis reads as local +X.</summary>
    public Vector2 Axis
    {
        get => OrientedFrame2D.AxisOrUnitX(_axis);
        init => _axis = value;
    }

    /// <summary>The full width and height along the rectangle's own axes.</summary>
    public Vector2 Size => HalfSize * 2f;

    /// <summary>The extent along the rectangle's own +X.</summary>
    public float Width => Size.X;

    /// <summary>The extent along the rectangle's own +Y.</summary>
    public float Height => Size.Y;

    /// <summary>The heading of <see cref="Axis"/> in radians, counter-clockwise from local +X.</summary>
    public float AngleRadians => Frame.AngleRadians;

    /// <summary>True when the rectangle's axes coincide with the local axes.</summary>
    public bool IsAxisAligned => Frame.IsAxisAligned;

    /// <summary>The frame every query projects through.</summary>
    public OrientedFrame2D Frame => new(Center, Axis);

    /// <inheritdoc/>
    public float Area => Area2D.Rectangle(-HalfSize, HalfSize);

    /// <inheritdoc/>
    public float Diameter => Diameter2D.Rectangle(-HalfSize, HalfSize);

    /// <inheritdoc/>
    public bool ContainsPoint(Vector2 localPoint) => Containment2D.Rectangle(Frame.ToFrame(localPoint), -HalfSize, HalfSize);

    /// <inheritdoc/>
    public Vector2 GetSupportPoint(Vector2 localDirection)
    {
        var frame = Frame;
        return frame.FromFrame(SupportPoint2D.Rectangle(frame.ToFrameDirection(localDirection), -HalfSize, HalfSize));
    }

    /// <summary>Writes the four local-space corners counter-clockwise from the corner at negative half extents.</summary>
    /// <param name="corners">A buffer of at least four entries.</param>
    public void WriteCorners(Span<Vector2> corners)
    {
        VertexGenerator2D.WriteRectangle(corners, -HalfSize, HalfSize);
        Frame.MapFromFrame(corners[..4]);
    }
}
