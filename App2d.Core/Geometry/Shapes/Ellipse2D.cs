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
/// A filled ellipse with its own orientation in local space: radii along its own axes, in the frame of
/// <see cref="Center"/> and <see cref="Axis"/>. Every query projects into that frame and runs the axis-aligned math there.
/// </summary>
public readonly partial record struct Ellipse2D : IConvexShape2D
{
    private readonly Vector2f _axis;

    /// <summary>Default samples for callers requesting a drawable perimeter; collision queries do not use these.</summary>
    public const int DefaultOutlineSegments = 64;

    /// <summary>Creates an ellipse.</summary>
    /// <param name="radii">The finite, positive half-extents along the ellipse's own axes.</param>
    /// <param name="center">The finite local center.</param>
    /// <param name="axis">The unit direction of the ellipse's local +X.</param>
    public Ellipse2D(Vector2f radii, Vector2f center, Vector2f axis)
    {
        Radii = radii;
        Center = center;
        _axis = axis;
    }

    /// <summary>The half-extents along the ellipse's own axes.</summary>
    public Vector2f Radii { get; init; }

    /// <summary>The local center.</summary>
    public Vector2f Center { get; init; }

    /// <summary>The unit direction of the ellipse's local +X. An unset axis reads as local +X.</summary>
    public Vector2f Axis
    {
        get => OrientedFrame2D.AxisOrUnitX(_axis);
        init => _axis = value;
    }

    /// <summary>Value equality on the effective axis, so an unset axis equals an explicit +X.</summary>
    public bool Equals(Ellipse2D other) => Radii.Equals(other.Radii) && Center.Equals(other.Center) && Axis.Equals(other.Axis);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(Radii, Center, Axis);

    /// <summary>The heading of <see cref="Axis"/> in radians, counter-clockwise from local +X.</summary>
    public float AngleRadians => Frame.AngleRadians;

    /// <summary>True when the ellipse's axes coincide with the local axes.</summary>
    public bool IsAxisAligned => Frame.IsAxisAligned;

    /// <summary>The frame every query projects through.</summary>
    public OrientedFrame2D Frame => new(Center, Axis);

    /// <inheritdoc/>
    public float Area => Area2D.Ellipse(Radii);

    /// <inheritdoc/>
    public float Diameter => Diameter2D.Ellipse(Radii);

    /// <inheritdoc/>
    public bool ContainsPoint(Vector2f localPoint) => Containment2D.Ellipse(Frame.ToFrame(localPoint), Vector2f.Zero, Radii);

    /// <inheritdoc/>
    public Vector2f GetSupportPoint(Vector2f localDirection)
    {
        var frame = Frame;
        return frame.FromFrame(SupportPoint2D.Ellipse(frame.ToFrameDirection(localDirection), Vector2f.Zero, Radii));
    }

    /// <summary>Writes a counter-clockwise perimeter; the buffer length chooses the segment count.</summary>
    /// <param name="vertices">A buffer of at least three entries; every entry is written.</param>
    /// <returns>The number of vertices written.</returns>
    public int WriteVertices(Span<Vector2f> vertices)
    {
        var count = VertexGenerator2D.WriteEllipse(vertices, Vector2f.Zero, Radii);
        Frame.MapFromFrame(vertices[..count]);
        return count;
    }
}
