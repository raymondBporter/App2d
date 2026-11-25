using App2d.Core.Geometry.Containment;
using App2d.Core.Geometry.Convex;
using App2d.Core.Geometry.Measurements;
using App2d.Core.Geometry.Primitives;

namespace App2d.Core.Geometry.Shapes;

/// <summary>
/// A filled ellipse with its own orientation in local space: radii along its own axes, in the frame of
/// <see cref="Center"/> and <see cref="Axis"/>. Every query projects into that frame and runs the axis-aligned math there.
/// </summary>
public readonly partial record struct Ellipse2D : IConvexShape2D, IInterpolatable<Ellipse2D, float>
{
    public GeometryKind Kind => GeometryKind.Ellipse;

    private readonly Vector2f _axis;

    /// <summary>The parts of an ellipse an interpolator can drive on their own: one radius for a blink, the centre for a glance.</summary>
    public static class Channels
    {
        public static InterpolationChannel<Ellipse2D, Ellipse2D> V => InterpolationChannel<Ellipse2D, Ellipse2D>.Whole;
        public static InterpolationChannel<Ellipse2D, Vector2f> Center { get; } = new(static value => value.Center, static (value, center) => value with { Center = center });
        public static InterpolationChannel<Ellipse2D, Vector2f> Radii { get; } = new(static value => value.Radii, static (value, radii) => value with { Radii = radii });
        /// <summary>The radius along the ellipse's own X axis.</summary>
        public static InterpolationChannel<Ellipse2D, float> RadiusX { get; } = new(static value => value.Radii.X, static (value, radius) => value with { Radii = new(radius, value.Radii.Y) });
        /// <summary>The radius along the ellipse's own Y axis.</summary>
        public static InterpolationChannel<Ellipse2D, float> RadiusY { get; } = new(static value => value.Radii.Y, static (value, radius) => value with { Radii = new(value.Radii.X, radius) });
        /// <summary>The heading of the ellipse's own X axis, in radians.</summary>
        public static InterpolationChannel<Ellipse2D, float> Angle { get; } = new(static value => value.AngleRadians, static (value, angle) => value with { Axis = OrientedFrame2D.AxisFromAngle(angle) });
    }

    /// <summary>
    /// Moves the centre and the radii in straight lines and turns the axis the short way round. The result is not validated:
    /// on the way to or from a closed shape a radius passes through zero, and past either end the amount extrapolates.
    /// </summary>
    public static Ellipse2D Lerp(Ellipse2D start, Ellipse2D end, float amount) => new(Vector2f.Lerp(start.Radii, end.Radii, amount), Vector2f.Lerp(start.Center, end.Center, amount),
        // Two ellipses that already agree keep that axis exactly, so no rounding enters an ellipse that only moves or resizes.
        start.Axis == end.Axis ? start.Axis : OrientedFrame2D.AxisFromAngle(Interpolation.ShortestAngle<float>().Evaluate(start.AngleRadians, end.AngleRadians, amount)));

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
